using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies bounded hail resolution and scheduled cautious-contact decisions.</summary>
public sealed class HailAndContactDecisionTests
{
    private static readonly ShipInstanceId PlayerId = new(1);
    private static readonly ShipInstanceId NpcId = new(2);
    private static readonly ShipDefinitionId PlayerDefinitionId = new("player");
    private static readonly ShipDefinitionId NpcDefinitionId = new("npc");
    private static readonly LocationId Local = new("local");

    /// <summary>Confirms hail rejection follows the locked local-contact precedence without mutation.</summary>
    [Fact]
    public void HailRejectionsUseLockedPrecedenceAndDoNotMutate()
    {
        Assert.Equal(
            ["ContactNotFound", "ContactNotCurrent", "ContactNotIdentified", "NoResponse", "Acknowledged"],
            Enum.GetNames<HailOutcome>()
        );

        GameSimulation unobserved = CreateGame(cautiousNpc: false);
        Assert.Equal(HailOutcome.ContactNotFound, unobserved.RequestHail(new SensorContactId(1)).Outcome);

        GameSimulation detected = CreateGame(cautiousNpc: false);
        detected.AdvanceFixedSteps(1);
        Assert.Equal(HailOutcome.ContactNotIdentified, detected.RequestHail(new SensorContactId(1)).Outcome);

        GameSimulation stale = WithPlayerContact(
            detected,
            contact =>
                contact with
                {
                    Status = SensorContactStatus.Lost,
                    Identification = SensorContactIdentification.Identified,
                    KnownVesselDisplayName = "IKS Watcher",
                    KnownDesignDisplayName = "Scout",
                }
        );
        SimulationState beforeStale = stale.CaptureState();
        Assert.Equal(HailOutcome.ContactNotCurrent, stale.RequestHail(new SensorContactId(1)).Outcome);
        Assert.Same(beforeStale, stale.CaptureState());

        GameSimulation noResponse = IdentifyNpc(CreateGame(cautiousNpc: false));
        SimulationState beforeNoResponse = noResponse.CaptureState();
        Assert.Equal(HailOutcome.NoResponse, noResponse.RequestHail(new SensorContactId(1)).Outcome);
        Assert.Same(beforeNoResponse, noResponse.CaptureState());
    }

    /// <summary>Confirms a response requires a reciprocal current contact correlated to the player.</summary>
    [Fact]
    public void HailRequiresReciprocalCurrentPlayerContact()
    {
        GameSimulation game = IdentifyNpc(CreateGame(cautiousNpc: true));
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(NpcId);
        game = GameSimulation.RestoreState(
            state.ReplaceShip(npc.InstanceId, npc with { SensorKnowledge = SensorKnowledge.Empty }),
            CreateCatalog()
        );
        SimulationState before = game.CaptureState();

        HailResult result = game.RequestHail(new SensorContactId(1));

        Assert.Equal(HailOutcome.NoResponse, result.Outcome);
        Assert.Same(before, game.CaptureState());
    }

    /// <summary>Confirms acknowledgement transmits only display identity and synchronously applies a hold.</summary>
    [Fact]
    public void AcknowledgedHailIdentifiesPlayerAndAppliesSharedCourseCommand()
    {
        GameSimulation game = IdentifyNpc(CreateGame(cautiousNpc: true));
        ShipState movingNpc = game.CaptureState().GetRequiredShip(NpcId);
        ShipState playerBefore = game.CaptureState().GetRequiredShip(PlayerId);
        Assert.Equal(0.5, movingNpc.TacticalMotion.Speed.Value);

        HailResult result = game.RequestHail(new SensorContactId(1));

        Assert.Equal(HailOutcome.Acknowledged, result.Outcome);
        SimulationState after = game.CaptureState();
        ShipState npc = after.GetRequiredShip(NpcId);
        SensorContactTrack playerContact = Assert.Single(npc.SensorKnowledge.Contacts);
        Assert.Equal(PlayerId, playerContact.TargetShipId);
        Assert.Equal(SensorContactIdentification.Identified, playerContact.Identification);
        Assert.Equal("USS Pathfinder", playerContact.KnownVesselDisplayName);
        Assert.Equal("Explorer", playerContact.KnownDesignDisplayName);
        Assert.Equal(0, npc.TacticalMotion.Speed.Value);
        Assert.Equal(playerBefore, after.GetRequiredShip(PlayerId));

        ShipContactDecisionExplanation explanation = Assert.IsType<ShipContactDecisionExplanation>(
            game.LastContactDecisionExplanation
        );
        Assert.Equal(NpcId, explanation.DecidingShipId);
        Assert.Equal(playerContact.Id, explanation.PrimaryContactId);
        Assert.Equal(ShipContactDecisionAction.Hold, explanation.SelectedAction);
        Assert.Equal(ShipContactDecisionPolicyReason.IdentifiedHailHold, explanation.Candidates[0].PolicyReason);
        Assert.False(explanation.RandomnessUsed);
    }

    /// <summary>Confirms one appended wake resolves once, stays hidden, and is not recreated by refresh.</summary>
    [Fact]
    public void ContactWakeIsDeduplicatedHiddenAndAppliesObservedPositionCourse()
    {
        GameSimulation game = CreateGame(cautiousNpc: true);
        ShipDefinitionCatalog catalog = CreateCatalog();

        SimulationAdvanceTraceResult first = GameSimulation.AdvanceTo(
            game.CaptureState(),
            new SimulationTime(100),
            catalog
        );

        ScheduledConsequenceTrace wake = Assert.Single(
            first.Traces,
            trace => trace.WorkKind == ScheduledWorkKind.ShipContactDecisionWake
        );
        Assert.True(wake.Completed);
        Assert.False(wake.RandomnessUsed);
        Assert.NotNull(wake.ContactDecision);
        Assert.Equal(
            [PlayerAdvanceEventKind.SensorContactDetected],
            first.PlayerEvents.Select(playerEvent => playerEvent.Kind)
        );
        ShipState npc = first.State.GetRequiredShip(NpcId);
        Assert.Null(npc.AutonomousState.PendingContactDecisionWake);
        Assert.Equal(90, npc.TacticalMotion.Heading.Value, 10);
        Assert.Equal(0.5, npc.TacticalMotion.Speed.Value);

        SimulationAdvanceTraceResult refreshed = GameSimulation.AdvanceTo(
            first.State,
            new SimulationTime(200),
            catalog
        );

        Assert.DoesNotContain(refreshed.Traces, trace => trace.WorkKind == ScheduledWorkKind.ShipContactDecisionWake);
        Assert.DoesNotContain(
            refreshed.State.Scheduler.OutstandingWork,
            work => work.Kind == ScheduledWorkKind.ShipContactDecisionWake
        );
    }

    private static GameSimulation IdentifyNpc(GameSimulation game)
    {
        game.AdvanceFixedSteps(1);
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(new SensorContactId(1)).Outcome);
        game.AdvanceFixedSteps(20);
        return game;
    }

    private static GameSimulation WithPlayerContact(
        GameSimulation game,
        Func<SensorContactTrack, SensorContactTrack> transform
    )
    {
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(PlayerId);
        SensorContactTrack contact = Assert.Single(player.SensorKnowledge.Contacts);
        SensorKnowledge knowledge = new(
            player.SensorKnowledge.NextContactId,
            [transform(contact)],
            player.SensorKnowledge.ActiveScan
        );
        return GameSimulation.RestoreState(
            state.ReplaceShip(player.InstanceId, player with { SensorKnowledge = knowledge }),
            CreateCatalog()
        );
    }

    private static GameSimulation CreateGame(bool cautiousNpc)
    {
        var map = new StrategicMap([new StrategicLocation(Local, "Local", default)], []);
        ShipStart[] starts =
        [
            Ship(PlayerId, PlayerDefinitionId, "USS Pathfinder", default),
            Ship(NpcId, NpcDefinitionId, "IKS Watcher", new TacticalPosition(5, 0)),
        ];
        GameSimulation game = new GameBootstrap(new SimulationTime(0), map, PlayerId, starts).CreateSimulation(
            CreateCatalog()
        );
        if (!cautiousNpc)
        {
            return game;
        }

        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(NpcId);
        return GameSimulation.RestoreState(
            state.ReplaceShip(
                npc.InstanceId,
                npc with
                {
                    AutonomousState = new ShipAutonomousState(ShipContactPosture.CautiousContact),
                }
            ),
            CreateCatalog()
        );
    }

    private static ShipStart Ship(
        ShipInstanceId id,
        ShipDefinitionId definitionId,
        string vesselName,
        TacticalPosition position
    ) => new(id, definitionId, vesselName, position, default, new SensorIntegrity(1), new AtLocationStart(Local));

    private static ShipDefinitionCatalog CreateCatalog() =>
        new(
            new Dictionary<ShipDefinitionId, ShipDefinition>
            {
                [PlayerDefinitionId] = Definition(PlayerDefinitionId, "Explorer", 100, 5),
                [NpcDefinitionId] = Definition(NpcDefinitionId, "Scout", 20, 2),
            }
        );

    private static ShipDefinition Definition(
        ShipDefinitionId id,
        string designName,
        double passiveRange,
        double maximumSpeed
    ) =>
        new(
            id,
            designName,
            new SpeedKilometersPerSecond(maximumSpeed),
            new DistanceKilometers(passiveRange),
            new SimulationDuration(2_000),
            new SimulationDuration(8_000)
        );
}
