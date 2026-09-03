using System.Text.Json.Nodes;
using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Locks the Engineering backbone into the complete headless first-contact scenario.</summary>
public sealed class Milestone4EngineeringScenarioTests
{
    private readonly Milestone3ProofFixture _fixture = new();

    /// <summary>Proves Engineering, contact, repair, AI, and travel state survive and continue together.</summary>
    [Fact]
    public void ConstrainedPathfinderEncounterRoundTripsAndContinuesDeterministically()
    {
        (GameSimulation game, SensorContactId contactId) = ReachSensorPriorityContact();
        ScenarioPair pair = RoundTripDuringActiveScan(game, contactId);

        CompleteScanAndHail(pair);
        ProvePropulsionAllocationInvariantAndReacquire(pair);
        CompleteRepairAndStrategicTravel(pair);
    }

    private (GameSimulation Game, SensorContactId ContactId) ReachSensorPriorityContact()
    {
        GameSimulation uninterrupted = _fixture.CreateDefault();
        PlayerProjection initial = uninterrupted.GetPlayerProjection();
        EngineeringProjection initialEngineering = initial.Ship.Engineering;

        Assert.Equal(new PowerUnits(75), initialEngineering.AvailablePower);
        Assert.Equal(new PowerUnits(44), initialEngineering.SensorAllocation);
        Assert.Equal(new PowerUnits(31), initialEngineering.ImpulseAllocation);
        Assert.Equal(0.4, initialEngineering.SensorCondition.Value);
        Assert.Equal(30 * (44d / 70) * 0.4, initialEngineering.EffectivePassiveSensorRange.Value, 12);
        Assert.Equal(6.2, initialEngineering.EffectiveMaximumTacticalSpeed.Value, 12);
        Assert.Equal(new LocationId("dawn-anchor"), initial.Strategic.CurrentLocation!.Id);
        Assert.Equal(
            new LocationId("meridian-drift"),
            Assert
                .IsType<TravelingState>(
                    uninterrupted.CaptureState().GetRequiredShip(new ShipInstanceId(3)).StrategicState
                )
                .Travel.Destination
        );

        PowerAllocationResult sensorPriority = uninterrupted.ApplyPowerAllocationPreset(
            PowerAllocationPreset.PrioritizeSensors
        );
        Assert.Equal(PowerAllocationOutcome.Accepted, sensorPriority.Outcome);
        Assert.Empty(sensorPriority.ResolvedEvents);
        Assert.Equal(new PowerUnits(70), uninterrupted.GetPlayerProjection().Ship.Engineering.SensorAllocation);
        Assert.Equal(new PowerUnits(5), uninterrupted.GetPlayerProjection().Ship.Engineering.ImpulseAllocation);
        Assert.Equal(12, uninterrupted.GetPlayerProjection().Ship.Engineering.EffectivePassiveSensorRange.Value, 12);
        Assert.Equal(1, uninterrupted.GetPlayerProjection().Ship.Engineering.EffectiveMaximumTacticalSpeed.Value, 12);

        AdvanceUntilResult acquisition = uninterrupted.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(new SimulationTime(3_500), acquisition.StoppedAt);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactDetected, Assert.Single(acquisition.ResolvedEvents).Kind);
        SensorContactSnapshot acquiredKestrel = Assert.Single(acquisition.Projection.Ship.Sensors.Contacts);
        Assert.Equal(new SensorContactId(1), acquiredKestrel.Id);
        Assert.Equal(new SimulationTime(3_500), acquiredKestrel.LastObservedAt);
        Assert.Equal(SensorContactIdentification.Detected, acquiredKestrel.Identification);
        Assert.Equal(0.6625, acquisition.Projection.Ship.Engineering.SensorCondition.Value, 12);
        Assert.Equal(19.875, acquisition.Projection.Ship.Engineering.EffectivePassiveSensorRange.Value, 12);
        Assert.DoesNotContain(
            typeof(SensorContactSnapshot).GetProperties(),
            property => property.Name.Contains("Target", StringComparison.Ordinal)
        );

        return (uninterrupted, acquiredKestrel.Id);
    }

    private ScenarioPair RoundTripDuringActiveScan(GameSimulation uninterrupted, SensorContactId contactId)
    {
        Assert.Equal(ActiveSensorScanOutcome.Accepted, uninterrupted.RequestActiveSensorScan(contactId).Outcome);
        SimulationAdvanceResult midScan = uninterrupted.AdvanceFixedSteps(10);
        Assert.Equal(new SimulationTime(4_500), midScan.FinalTime);
        Assert.Equal(0.7375, midScan.Projection.Ship.Engineering.SensorCondition.Value, 12);
        Assert.Equal(0.5625, midScan.Projection.Ship.Engineering.ActiveRepair!.Progress, 12);
        Assert.Equal(0.5, midScan.Projection.Ship.Sensors.ActiveScanProgress);

        byte[] save = GamePersistence.Serialize(uninterrupted, Milestone3ProofFixture.Metadata);
        AssertPersistedCombinedState(save, uninterrupted, contactId);
        LoadedGameSave loaded = GamePersistence.Deserialize(save, _fixture.Catalog, "milestone-4-mid-scan.json");
        GameSimulation resumed = loaded.Simulation;
        AssertEquivalentState(uninterrupted, resumed);
        EngineeringProjection loadedEngineering = resumed.GetPlayerProjection().Ship.Engineering;
        Assert.Equal(0.625, loadedEngineering.GenerationCondition.Value);
        Assert.Equal(0.7375, loadedEngineering.SensorCondition.Value, 12);
        Assert.Equal(1, loadedEngineering.ImpulseCondition.Value);
        Assert.Equal(new PowerUnits(70), loadedEngineering.SensorAllocation);
        Assert.Equal(new PowerUnits(5), loadedEngineering.ImpulseAllocation);
        Assert.Equal(contactId, Assert.Single(resumed.GetPlayerProjection().Ship.Sensors.Contacts).Id);
        AssertActiveWorkCorrelations(resumed, contactId);

        return new ScenarioPair(uninterrupted, resumed, contactId);
    }

    private static void CompleteScanAndHail(ScenarioPair pair)
    {
        SimulationAdvanceResult uninterruptedScan = pair.Uninterrupted.AdvanceFixedSteps(10);
        SimulationAdvanceResult resumedScan = pair.Resumed.AdvanceFixedSteps(10);
        Assert.Equal(uninterruptedScan, resumedScan);
        Assert.Equal(new SimulationTime(5_500), uninterruptedScan.FinalTime);
        Assert.Equal(
            PlayerAdvanceEventKind.ActiveSensorScanCompleted,
            Assert.Single(uninterruptedScan.ResolvedEvents).Kind
        );
        SensorContactSnapshot identifiedKestrel = Assert.Single(uninterruptedScan.Projection.Ship.Sensors.Contacts);
        Assert.Equal(pair.ContactId, identifiedKestrel.Id);
        Assert.Equal(SensorContactIdentification.Identified, identifiedKestrel.Identification);
        Assert.Equal("Survey Vessel Kestrel", identifiedKestrel.KnownVesselDisplayName);
        AssertEquivalentState(pair.Uninterrupted, pair.Resumed);

        Assert.Equal(HailOutcome.Acknowledged, pair.Uninterrupted.RequestHail(pair.ContactId).Outcome);
        Assert.Equal(HailOutcome.Acknowledged, pair.Resumed.RequestHail(pair.ContactId).Outcome);
        Assert.Equal(ShipContactDecisionAction.Hold, pair.Uninterrupted.LastContactDecisionExplanation!.SelectedAction);
        AssertEquivalentState(pair.Uninterrupted, pair.Resumed);
    }

    private static void ProvePropulsionAllocationInvariantAndReacquire(ScenarioPair pair)
    {
        PowerAllocationResult propulsionPriority = pair.Uninterrupted.ApplyPowerAllocationPreset(
            PowerAllocationPreset.PrioritizePropulsion
        );
        PowerAllocationResult resumedPropulsionPriority = pair.Resumed.ApplyPowerAllocationPreset(
            PowerAllocationPreset.PrioritizePropulsion
        );
        Assert.Equal(propulsionPriority, resumedPropulsionPriority);
        Assert.Equal(PowerAllocationOutcome.Accepted, propulsionPriority.Outcome);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactStale, Assert.Single(propulsionPriority.ResolvedEvents).Kind);
        EngineeringProjection propulsionEngineering = pair.Uninterrupted.GetPlayerProjection().Ship.Engineering;
        Assert.Equal(new PowerUnits(25), propulsionEngineering.SensorAllocation);
        Assert.Equal(new PowerUnits(50), propulsionEngineering.ImpulseAllocation);
        Assert.Equal(10, propulsionEngineering.EffectiveMaximumTacticalSpeed.Value, 12);

        var flankCourse = new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(10));
        Assert.Equal(SetTacticalCourseOutcome.Accepted, pair.Uninterrupted.SetTacticalCourse(flankCourse).Outcome);
        Assert.Equal(SetTacticalCourseOutcome.Accepted, pair.Resumed.SetTacticalCourse(flankCourse).Outcome);
        PlayerProjection beforeRejectedAllocation = pair.Uninterrupted.GetPlayerProjection();
        PowerAllocationResult rejectedSensors = pair.Uninterrupted.ApplyPowerAllocationPreset(
            PowerAllocationPreset.PrioritizeSensors
        );
        PowerAllocationResult resumedRejectedSensors = pair.Resumed.ApplyPowerAllocationPreset(
            PowerAllocationPreset.PrioritizeSensors
        );
        Assert.Equal(PowerAllocationOutcome.CurrentSpeedExceedsResultingMaximum, rejectedSensors.Outcome);
        Assert.Equal(rejectedSensors, resumedRejectedSensors);
        Assert.Equal(beforeRejectedAllocation, pair.Uninterrupted.GetPlayerProjection());

        var stop = new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(0));
        Assert.Equal(SetTacticalCourseOutcome.Accepted, pair.Uninterrupted.SetTacticalCourse(stop).Outcome);
        Assert.Equal(SetTacticalCourseOutcome.Accepted, pair.Resumed.SetTacticalCourse(stop).Outcome);
        PowerAllocationResult reacquisition = pair.Uninterrupted.ApplyPowerAllocationPreset(
            PowerAllocationPreset.PrioritizeSensors
        );
        PowerAllocationResult resumedReacquisition = pair.Resumed.ApplyPowerAllocationPreset(
            PowerAllocationPreset.PrioritizeSensors
        );
        Assert.Equal(reacquisition, resumedReacquisition);
        Assert.Equal(PowerAllocationOutcome.Accepted, reacquisition.Outcome);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactReacquired, Assert.Single(reacquisition.ResolvedEvents).Kind);
        SensorContactSnapshot reacquiredKestrel = Assert.Single(
            pair.Uninterrupted.GetPlayerProjection().Ship.Sensors.Contacts
        );
        Assert.Equal(pair.ContactId, reacquiredKestrel.Id);
        Assert.Equal(new SimulationTime(5_500), reacquiredKestrel.LastObservedAt);
        Assert.Equal(SensorContactStatus.Current, reacquiredKestrel.Status);
        Assert.Equal(SensorContactIdentification.Identified, reacquiredKestrel.Identification);
        AssertEquivalentState(pair.Uninterrupted, pair.Resumed);
    }

    private static void CompleteRepairAndStrategicTravel(ScenarioPair pair)
    {
        SimulationAdvanceResult repairCompletion = pair.Uninterrupted.AdvanceFixedSteps(25);
        SimulationAdvanceResult resumedRepairCompletion = pair.Resumed.AdvanceFixedSteps(25);
        Assert.Equal(repairCompletion, resumedRepairCompletion);
        Assert.Equal(new SimulationTime(8_000), repairCompletion.FinalTime);
        PlayerAdvanceEvent repairEvent = Assert.Single(repairCompletion.ResolvedEvents);
        Assert.Equal(PlayerAdvanceEventKind.SystemRepairCompleted, repairEvent.Kind);
        Assert.Equal(ShipSystemId.Sensors, repairEvent.ShipSystemId);
        Assert.Equal(1, repairCompletion.Projection.Ship.Engineering.SensorCondition.Value);
        Assert.Null(repairCompletion.Projection.Ship.Engineering.ActiveRepair);
        AssertEquivalentState(pair.Uninterrupted, pair.Resumed);

        SimulationAdvanceResult strategicContinuation = pair.Uninterrupted.AdvanceFixedSteps(60);
        SimulationAdvanceResult resumedStrategicContinuation = pair.Resumed.AdvanceFixedSteps(60);
        Assert.Equal(strategicContinuation, resumedStrategicContinuation);
        Assert.Equal(new SimulationTime(14_000), strategicContinuation.FinalTime);
        Assert.Equal(
            new LocationId("meridian-drift"),
            Assert
                .IsType<AtLocationState>(
                    pair.Uninterrupted.CaptureState().GetRequiredShip(new ShipInstanceId(3)).StrategicState
                )
                .LocationId
        );
        Assert.Equal(
            new LocationId("dawn-anchor"),
            pair.Uninterrupted.GetPlayerProjection().Strategic.CurrentLocation!.Id
        );
        AssertEquivalentState(pair.Uninterrupted, pair.Resumed);
    }

    private static void AssertPersistedCombinedState(byte[] save, GameSimulation simulation, SensorContactId contactId)
    {
        SimulationState state = simulation.CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        ShipState kestrel = Milestone3ProofFixture.Kestrel(simulation);
        SystemRepairState repair = Assert.IsType<SystemRepairState>(player.Engineering.ActiveRepair);
        ActiveSensorScanState scan = Assert.IsType<ActiveSensorScanState>(player.SensorKnowledge.ActiveScan);
        JsonObject root = JsonNode.Parse(save)!.AsObject();
        JsonNode persistedSimulation = root["simulation"]!;
        JsonNode persistedPlayer = persistedSimulation["ships"]![0]!;
        JsonNode persistedKestrel = persistedSimulation["ships"]![3]!;

        Assert.Equal(5, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal(4_500, persistedSimulation["timeMilliseconds"]!.GetValue<long>());
        Assert.Equal(70, persistedPlayer["engineering"]!["sensorAllocation"]!.GetValue<int>());
        Assert.Equal(5, persistedPlayer["engineering"]!["impulseAllocation"]!.GetValue<int>());
        Assert.Equal(0.7375, persistedPlayer["engineering"]!["sensorCondition"]!.GetValue<double>(), 12);
        Assert.Equal(contactId.Value, persistedPlayer["sensorKnowledge"]!["contacts"]![0]!["id"]!.GetValue<long>());
        Assert.Equal(
            scan.ScheduledCompletionId.Value,
            persistedPlayer["sensorKnowledge"]!["activeScan"]!["scheduledCompletionId"]!.GetValue<long>()
        );
        Assert.Equal(
            repair.ScheduledCompletionId.Value,
            persistedPlayer["engineering"]!["activeRepair"]!["scheduledCompletionId"]!.GetValue<long>()
        );
        Assert.Equal("cautiousContact", persistedKestrel["autonomousState"]!["contactPosture"]!.GetValue<string>());
        Assert.Null(persistedKestrel["autonomousState"]!["pendingContactDecisionWake"]);

        Assert.Equal(ShipContactPosture.CautiousContact, kestrel.AutonomousState.ContactPosture);
        Assert.Null(kestrel.AutonomousState.PendingContactDecisionWake);
        AssertActiveWorkCorrelations(simulation, contactId);
    }

    private static void AssertActiveWorkCorrelations(GameSimulation simulation, SensorContactId contactId)
    {
        SimulationState state = simulation.CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        SystemRepairState repair = Assert.IsType<SystemRepairState>(player.Engineering.ActiveRepair);
        ActiveSensorScanState scan = Assert.IsType<ActiveSensorScanState>(player.SensorKnowledge.ActiveScan);

        Assert.Equal(contactId, scan.TargetContactId);
        Assert.Equal(new SimulationTime(5_500), scan.ExpectedCompletion);
        Assert.Equal(new SimulationTime(8_000), repair.ExpectedCompletion);
        Assert.Contains(
            state.Scheduler.OutstandingWork,
            work =>
                work.Id == scan.ScheduledCompletionId
                && work.TargetShipId == player.InstanceId
                && work.Kind == ScheduledWorkKind.ActiveSensorScanCompletion
                && work.DueTime == scan.ExpectedCompletion
        );
        Assert.Contains(
            state.Scheduler.OutstandingWork,
            work =>
                work.Id == repair.ScheduledCompletionId
                && work.TargetShipId == player.InstanceId
                && work.Kind == ScheduledWorkKind.SystemRepairCompletion
                && work.DueTime == repair.ExpectedCompletion
        );
    }

    private static void AssertEquivalentState(GameSimulation expected, GameSimulation actual)
    {
        SimulationState expectedState = expected.CaptureState();
        SimulationState actualState = actual.CaptureState();

        Assert.Equal(expected.GetPlayerProjection(), actual.GetPlayerProjection());
        Assert.Equal(expectedState.Time, actualState.Time);
        Assert.Equal(expectedState.PlayerShipId, actualState.PlayerShipId);
        Assert.Equal(expectedState.ShipIdAllocator.NextId, actualState.ShipIdAllocator.NextId);
        Assert.Equal(expectedState.OrderIdAllocator.NextId, actualState.OrderIdAllocator.NextId);
        Assert.Equal(expectedState.Scheduler.NextWorkId, actualState.Scheduler.NextWorkId);
        Assert.Equal(expectedState.Scheduler.NextSequence, actualState.Scheduler.NextSequence);
        Assert.Equal(
            expectedState.Scheduler.OutstandingWork.Select(work =>
                (work.Id, work.DueTime, work.Sequence, work.TargetShipId, work.Kind)
            ),
            actualState.Scheduler.OutstandingWork.Select(work =>
                (work.Id, work.DueTime, work.Sequence, work.TargetShipId, work.Kind)
            )
        );
        Assert.Equal(expectedState.Ships.Length, actualState.Ships.Length);
        foreach (ShipState expectedShip in expectedState.Ships)
        {
            ShipState actualShip = actualState.GetRequiredShip(expectedShip.InstanceId);
            Assert.Equal(expectedShip.DefinitionId, actualShip.DefinitionId);
            Assert.Equal(expectedShip.VesselDisplayName, actualShip.VesselDisplayName);
            Assert.Equal(expectedShip.TacticalPosition, actualShip.TacticalPosition);
            Assert.Equal(expectedShip.TacticalMotion, actualShip.TacticalMotion);
            Assert.Equal(expectedShip.Engineering, actualShip.Engineering);
            Assert.Equal(expectedShip.StrategicState, actualShip.StrategicState);
            Assert.Equal(expectedShip.ActiveOrder, actualShip.ActiveOrder);
            Assert.Equal(expectedShip.SensorKnowledge.NextContactId, actualShip.SensorKnowledge.NextContactId);
            Assert.True(expectedShip.SensorKnowledge.Contacts.SequenceEqual(actualShip.SensorKnowledge.Contacts));
            Assert.Equal(expectedShip.SensorKnowledge.ActiveScan, actualShip.SensorKnowledge.ActiveScan);
            Assert.Equal(expectedShip.AutonomousState, actualShip.AutonomousState);
        }
    }

    private sealed record ScenarioPair(GameSimulation Uninterrupted, GameSimulation Resumed, SensorContactId ContactId);
}
