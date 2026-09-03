using System.Text.Json.Nodes;
using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Locks the default first-contact encounter through deterministic headless scenarios.</summary>
public sealed class Milestone3ProofScenarioTests
{
    private readonly Milestone3ProofFixture _fixture = new();

    /// <summary>Confirms the fourth ship extends the prior world without changing its three established roles.</summary>
    [Fact]
    public void DefaultSetupHasLockedFourShipBootstrapSignatureAndHiddenInitialObservation()
    {
        GameSimulation game = _fixture.CreateDefault();
        SimulationState state = game.CaptureState();
        ShipState player = Milestone3ProofFixture.Player(game);
        ShipState wayfarer = state.GetRequiredShip(new ShipInstanceId(2));
        ShipState horizon = state.GetRequiredShip(new ShipInstanceId(3));
        ShipState kestrel = Milestone3ProofFixture.Kestrel(game);

        Assert.Equal([1L, 2L, 3L, 4L], state.Ships.Select(ship => ship.InstanceId.Value));
        Assert.True(
            state
                .Ships.Select(ship => ship.VesselDisplayName)
                .SequenceEqual(
                    ["USS Pathfinder", "USS Wayfarer", "USS Horizon", "Survey Vessel Kestrel"],
                    StringComparer.Ordinal
                )
        );
        Assert.Equal(5, state.ShipIdAllocator.NextId);
        Assert.Equal(new LocationId("dawn-anchor"), Assert.IsType<AtLocationState>(player.StrategicState).LocationId);
        Assert.NotNull(player.SensorRepair);
        Assert.Equal(
            new LocationId("vesper-reach"),
            Assert.IsType<AtLocationState>(wayfarer.StrategicState).LocationId
        );
        Assert.NotNull(wayfarer.SensorRepair);
        TravelingState horizonTravel = Assert.IsType<TravelingState>(horizon.StrategicState);
        Assert.Equal(new LocationId("vesper-reach"), horizonTravel.Travel.Origin);
        Assert.Equal(new LocationId("meridian-drift"), horizonTravel.Travel.Destination);

        Assert.Equal(new TacticalPosition(21.25, -7.5), kestrel.TacticalPosition);
        Assert.Equal(default, kestrel.TacticalMotion);
        Assert.Equal(1, kestrel.SensorIntegrity.Value);
        Assert.Null(kestrel.SensorRepair);
        Assert.Null(kestrel.ActiveOrder);
        Assert.Equal(new LocationId("dawn-anchor"), Assert.IsType<AtLocationState>(kestrel.StrategicState).LocationId);
        Assert.Equal(ShipContactPosture.CautiousContact, kestrel.AutonomousState.ContactPosture);
        SensorContactTrack npcContact = Assert.Single(kestrel.SensorKnowledge.Contacts);
        Assert.Equal(SensorContactStatus.Current, npcContact.Status);
        Assert.Equal(SensorContactIdentification.Detected, npcContact.Identification);
        Assert.Equal(player.TacticalPosition, npcContact.LastObservedPosition);
        Assert.Equal(new SimulationTime(0), npcContact.LastObservedAt);
        Assert.Empty(game.GetPlayerProjection().Ship.Sensors.Contacts);
        Assert.Equal(
            [
                (4L, 0L, "ShipContactDecisionWake"),
                (1L, 8000L, "SensorRepairCompletion"),
                (2L, 8000L, "SensorRepairCompletion"),
                (3L, 14000L, "TravelArrival"),
            ],
            state.Scheduler.OutstandingWork.Select(work =>
                (work.TargetShipId.Value, work.DueTime.Milliseconds, work.Kind.ToString())
            )
        );

        JsonObject root = JsonNode.Parse(GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata))!.AsObject();
        Assert.Equal(4, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal(5, root["simulation"]!["shipAllocatorNextId"]!.GetValue<long>());
        Assert.Equal(4, root["simulation"]!["ships"]!.AsArray().Count);
    }

    /// <summary>Confirms acquisition, scan persistence, hail, repair, and held knowledge form one proof path.</summary>
    [Fact]
    public void PrimaryScenarioPersistsMidScanAndAfterAcknowledgedHail()
    {
        GameSimulation uninterrupted = _fixture.CreateDefault();
        AdvanceUntilResult acquisition = uninterrupted.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(3500, acquisition.StoppedAt.Milliseconds);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactDetected, Assert.Single(acquisition.ResolvedEvents).Kind);
        SensorContactSnapshot detected = Assert.Single(acquisition.Projection.Ship.Sensors.Contacts);
        Assert.Equal(SensorContactStatus.Current, detected.Status);
        Assert.Equal(SensorContactIdentification.Detected, detected.Identification);
        AssertInitialWithdrawal(uninterrupted);

        Assert.Equal(ActiveSensorScanOutcome.Accepted, uninterrupted.RequestActiveSensorScan(detected.Id).Outcome);
        uninterrupted.AdvanceFixedSteps(10);
        GameSimulation resumed = _fixture.RoundTrip(uninterrupted, "milestone-3-mid-scan.json");

        SimulationAdvanceResult uninterruptedScan = uninterrupted.AdvanceFixedSteps(10);
        SimulationAdvanceResult resumedScan = resumed.AdvanceFixedSteps(10);

        Assert.Equal(5500, uninterruptedScan.FinalTime.Milliseconds);
        Assert.Equal(uninterruptedScan, resumedScan);
        Assert.Equal(
            PlayerAdvanceEventKind.ActiveSensorScanCompleted,
            Assert.Single(uninterruptedScan.ResolvedEvents).Kind
        );
        SensorContactSnapshot identified = Assert.Single(uninterruptedScan.Projection.Ship.Sensors.Contacts);
        Assert.Equal(SensorContactIdentification.Identified, identified.Identification);
        Assert.Equal("Survey Vessel Kestrel", identified.KnownVesselDisplayName);
        Assert.Equal("Pathfinder class", identified.KnownDesignDisplayName);
        AssertSameSave(uninterrupted, resumed);

        Assert.Equal(HailOutcome.Acknowledged, uninterrupted.RequestHail(identified.Id).Outcome);
        Assert.Equal(ShipContactDecisionAction.Hold, uninterrupted.LastContactDecisionExplanation!.SelectedAction);
        Assert.Equal(0, Milestone3ProofFixture.Kestrel(uninterrupted).TacticalMotion.Speed.Value);
        Assert.Equal(
            SensorContactIdentification.Identified,
            Assert.Single(Milestone3ProofFixture.Kestrel(uninterrupted).SensorKnowledge.Contacts).Identification
        );

        GameSimulation postHail = _fixture.RoundTrip(uninterrupted, "milestone-3-post-hail.json");
        Assert.Equal(0, Milestone3ProofFixture.Kestrel(postHail).TacticalMotion.Speed.Value);
        Assert.Equal(
            uninterrupted.GetPlayerProjection().Ship.Sensors.Contacts,
            postHail.GetPlayerProjection().Ship.Sensors.Contacts
        );
        AssertSameSave(uninterrupted, postHail);

        SimulationAdvanceResult repair = postHail.AdvanceFixedSteps(25);
        Assert.Equal(8000, repair.FinalTime.Milliseconds);
        Assert.Equal(PlayerAdvanceEventKind.SensorRepairCompleted, Assert.Single(repair.ResolvedEvents).Kind);
        Assert.Equal(1, repair.Projection.Ship.Sensors.Integrity);
        Assert.False(repair.Projection.Ship.Sensors.IsRepairing);
        Assert.Equal(0, Milestone3ProofFixture.Kestrel(postHail).TacticalMotion.Speed.Value);
    }

    /// <summary>Confirms the autonomous no-interaction path acquires, stales, persists, and loses exactly on-grid.</summary>
    [Fact]
    public void NoInteractionScenarioPersistsDuringStaleAndLosesAt29100Milliseconds()
    {
        GameSimulation uninterrupted = _fixture.CreateDefault();

        SimulationAdvanceResult beforeAcquisition = uninterrupted.AdvanceFixedSteps(34);
        Assert.Empty(beforeAcquisition.Projection.Ship.Sensors.Contacts);
        Assert.Equal(0.5, Milestone3ProofFixture.Kestrel(uninterrupted).TacticalMotion.Speed.Value);

        SimulationAdvanceResult acquisition = uninterrupted.AdvanceFixedSteps(1);
        Assert.Equal(3500, acquisition.FinalTime.Milliseconds);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactDetected, Assert.Single(acquisition.ResolvedEvents).Kind);
        Assert.Equal(SensorContactStatus.Current, Assert.Single(acquisition.Projection.Ship.Sensors.Contacts).Status);

        SimulationAdvanceResult lastCurrent = uninterrupted.AdvanceFixedSteps(205);
        Assert.Equal(24000, lastCurrent.FinalTime.Milliseconds);
        Assert.Equal(
            [PlayerAdvanceEventKind.SensorRepairCompleted],
            lastCurrent.ResolvedEvents.Select(@event => @event.Kind)
        );
        Assert.Equal(SensorContactStatus.Current, Assert.Single(lastCurrent.Projection.Ship.Sensors.Contacts).Status);

        SimulationAdvanceResult stale = uninterrupted.AdvanceFixedSteps(1);
        Assert.Equal(24100, stale.FinalTime.Milliseconds);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactStale, Assert.Single(stale.ResolvedEvents).Kind);
        Assert.Equal(SensorContactStatus.Stale, Assert.Single(stale.Projection.Ship.Sensors.Contacts).Status);
        GameSimulation resumed = _fixture.RoundTrip(uninterrupted, "milestone-3-mid-stale.json");

        SimulationAdvanceResult beforeLoss = uninterrupted.AdvanceFixedSteps(49);
        SimulationAdvanceResult resumedBeforeLoss = resumed.AdvanceFixedSteps(49);
        Assert.Equal(29000, beforeLoss.FinalTime.Milliseconds);
        Assert.Equal(beforeLoss, resumedBeforeLoss);
        Assert.Empty(beforeLoss.ResolvedEvents);
        Assert.Equal(SensorContactStatus.Stale, Assert.Single(beforeLoss.Projection.Ship.Sensors.Contacts).Status);

        SimulationAdvanceResult loss = uninterrupted.AdvanceFixedSteps(1);
        SimulationAdvanceResult resumedLoss = resumed.AdvanceFixedSteps(1);
        Assert.Equal(29100, loss.FinalTime.Milliseconds);
        Assert.Equal(loss, resumedLoss);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactLost, Assert.Single(loss.ResolvedEvents).Kind);
        Assert.Empty(loss.Projection.Ship.Sensors.Contacts);
        Assert.Equal(
            SensorContactStatus.Lost,
            Assert.Single(Milestone3ProofFixture.Player(uninterrupted).SensorKnowledge.Contacts).Status
        );
        AssertSameSave(uninterrupted, resumed);
    }

    /// <summary>Confirms declaration order and advancement chunking cannot alter the complete proof state.</summary>
    [Fact]
    public void BootstrapOrderAndStepChunkingAreEquivalent()
    {
        GameSimulation forward = _fixture.CreateWithBootstrapOrder(reversed: false);
        GameSimulation reversed = _fixture.CreateWithBootstrapOrder(reversed: true);
        AssertSameSave(forward, reversed);

        GameSimulation large = _fixture.CreateDefault();
        GameSimulation singles = _fixture.CreateDefault();
        large.AdvanceFixedSteps(291);
        for (int index = 0; index < 291; index++)
        {
            singles.AdvanceFixedSteps(1);
        }

        AssertSameSave(large, singles);

        forward.AdvanceFixedSteps(291);
        reversed.AdvanceFixedSteps(291);
        AssertSameSave(forward, reversed);
    }

    private static void AssertSameSave(GameSimulation expected, GameSimulation actual) =>
        Assert.Equal(
            GamePersistence.Serialize(expected, Milestone3ProofFixture.Metadata),
            GamePersistence.Serialize(actual, Milestone3ProofFixture.Metadata)
        );

    private static void AssertInitialWithdrawal(GameSimulation simulation)
    {
        ShipContactDecisionExplanation explanation = simulation.LastContactDecisionExplanation!;
        ShipState kestrel = Milestone3ProofFixture.Kestrel(simulation);
        Assert.Equal(new SimulationTime(0), explanation.DecisionTime);
        Assert.Equal(ShipContactDecisionAction.Withdraw, explanation.SelectedAction);
        Assert.Equal(90, kestrel.TacticalMotion.Heading.Value, 10);
        Assert.Equal(0.5, kestrel.TacticalMotion.Speed.Value);
        Assert.Equal(23, kestrel.TacticalPosition.XKilometers, 10);
        Assert.Equal(-7.5, kestrel.TacticalPosition.YKilometers, 10);
    }
}
