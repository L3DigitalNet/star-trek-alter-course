using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies deterministic observation, contact lifecycle, scans, and shared tactical commands.</summary>
public sealed class SensorObservationCommandTests
{
    private static readonly LocationId Local = new("local");
    private static readonly LocationId Remote = new("remote");
    private static readonly ShipDefinitionId ObserverDefinitionId = new("observer");
    private static readonly ShipDefinitionId TargetDefinitionId = new("target");

    /// <summary>Confirms public sensor commands and events expose only actor-local identities.</summary>
    [Fact]
    public void PublicSensorContractsUseLockedTypedVocabulary()
    {
        Assert.Equal(
            [
                "ContactNotFound",
                "ContactNotCurrent",
                "AlreadyIdentified",
                "SensorsUnavailable",
                "ScanAlreadyActive",
                "Accepted",
            ],
            Enum.GetNames<ActiveSensorScanOutcome>()
        );
        Assert.Equal(
            [nameof(PlayerAdvanceEvent.Kind), nameof(PlayerAdvanceEvent.SensorContactId)],
            typeof(PlayerAdvanceEvent).GetProperties().Select(property => property.Name),
            StringComparer.Ordinal
        );
        Assert.DoesNotContain(
            typeof(PlayerAdvanceEvent).GetProperties(),
            property => property.PropertyType == typeof(ShipInstanceId)
        );
    }

    /// <summary>Confirms passive range is observer-specific and excludes other strategic contexts.</summary>
    [Fact]
    public void PassiveObservationUsesLocationRangeAndObserverIntegrity()
    {
        GameSimulation game = CreateGame(
            Ship(1, ObserverDefinitionId, default, integrity: 0.5),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0)),
            Ship(3, TargetDefinitionId, new TacticalPosition(4, 0), Remote),
            Ship(4, TargetDefinitionId, new TacticalPosition(6, 0))
        );

        SimulationAdvanceResult result = game.AdvanceFixedSteps(1);

        Assert.Equal([new SensorContactId(1)], result.Projection.Ship.Sensors.Contacts.Select(contact => contact.Id));
        Assert.Equal(new TacticalPosition(5, 0), result.Projection.Ship.Sensors.Contacts[0].LastObservedPosition);
        Assert.Equal(
            [new PlayerAdvanceEvent(PlayerAdvanceEventKind.SensorContactDetected, new SensorContactId(1))],
            result.ResolvedEvents
        );
        Assert.DoesNotContain(
            game.CaptureState().GetRequiredShip(new ShipInstanceId(2)).SensorKnowledge.Contacts,
            contact => contact.TargetShipId == new ShipInstanceId(1)
        );
        Assert.Contains(PlayerAction.ActiveSensorScan, result.Projection.AvailableActions);
    }

    /// <summary>Confirms unseen targets are admitted by truth identity until the no-eviction cap.</summary>
    [Fact]
    public void ContactAllocationIsInsertionIndependentAndNeverEvicts()
    {
        ShipStart observer = Ship(1, ObserverDefinitionId, default);
        ShipStart[] targets =
        [
            .. Enumerable.Range(2, 14).Select(id => Ship(id, TargetDefinitionId, new TacticalPosition(id / 10.0, 0))),
        ];
        GameSimulation ascending = CreateGame([observer, .. targets]);
        GameSimulation descending = CreateGame([observer, .. targets.Reverse()]);

        ascending.AdvanceFixedSteps(1);
        descending.AdvanceFixedSteps(1);

        SensorKnowledge first = ascending.CaptureState().GetRequiredShip(new ShipInstanceId(1)).SensorKnowledge;
        SensorKnowledge second = descending.CaptureState().GetRequiredShip(new ShipInstanceId(1)).SensorKnowledge;
        Assert.Equal(first.NextContactId, second.NextContactId);
        Assert.Equal(
            first.Contacts.Select(contact => (contact.Id, contact.TargetShipId, contact.LastObservedPosition)),
            second.Contacts.Select(contact => (contact.Id, contact.TargetShipId, contact.LastObservedPosition))
        );
        Assert.Equal(12, first.Contacts.Length);
        Assert.Equal(Enumerable.Range(2, 12), first.Contacts.Select(contact => (int)contact.TargetShipId.Value));

        ascending.SetTacticalCourse(
            new SetTacticalCourseIntent(new HeadingDegrees(270), new SpeedKilometersPerSecond(100))
        );
        ascending.AdvanceFixedSteps(51);
        Assert.Equal(12, ascending.GetPlayerProjection().Ship.Sensors.Contacts.Count);
    }

    /// <summary>Confirms exact stale, reacquire, and loss transitions retain one local identity.</summary>
    [Fact]
    public void ContactLifecycleReacquiresBeforeExactLossAndOtherwiseBecomesLost()
    {
        GameSimulation reacquired = CreateGame(
            Ship(1, ObserverDefinitionId, default),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0))
        );
        reacquired.AdvanceFixedSteps(1);
        reacquired.SetTacticalCourse(
            new SetTacticalCourseIntent(new HeadingDegrees(270), new SpeedKilometersPerSecond(10))
        );
        SimulationAdvanceResult stale = reacquired.AdvanceFixedSteps(6);
        reacquired.SetTacticalCourse(
            new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(10))
        );
        SimulationAdvanceResult current = reacquired.AdvanceFixedSteps(2);

        Assert.Equal(PlayerAdvanceEventKind.SensorContactStale, Assert.Single(stale.ResolvedEvents).Kind);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactReacquired, Assert.Single(current.ResolvedEvents).Kind);
        SensorContactSnapshot reacquiredContact = Assert.Single(current.Projection.Ship.Sensors.Contacts);
        Assert.Equal(new SensorContactId(1), reacquiredContact.Id);
        Assert.Equal(SensorContactStatus.Current, reacquiredContact.Status);

        GameSimulation lost = CreateGame(
            Ship(1, ObserverDefinitionId, default),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0))
        );
        lost.AdvanceFixedSteps(1);
        lost.SetTacticalCourse(new SetTacticalCourseIntent(new HeadingDegrees(270), new SpeedKilometersPerSecond(10)));
        SimulationAdvanceResult loss = lost.AdvanceFixedSteps(56);

        Assert.Equal(5_700, loss.FinalTime.Milliseconds);
        Assert.Equal(
            [PlayerAdvanceEventKind.SensorContactStale, PlayerAdvanceEventKind.SensorContactLost],
            loss.ResolvedEvents.Select(playerEvent => playerEvent.Kind)
        );
        Assert.Equal(SensorContactStatus.Lost, Assert.Single(loss.Projection.Ship.Sensors.Contacts).Status);
    }

    /// <summary>Confirms scan admission precedence, completion, identification, and action legality.</summary>
    [Fact]
    public void ActiveScanUsesLockedOutcomesAndCompletesAgainstLocalContact()
    {
        GameSimulation game = CreateGame(
            Ship(1, ObserverDefinitionId, default),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0), vesselName: "USS Known"),
            Ship(3, TargetDefinitionId, new TacticalPosition(6, 0))
        );
        game.AdvanceFixedSteps(1);

        Assert.Equal(
            ActiveSensorScanOutcome.ContactNotFound,
            game.RequestActiveSensorScan(new SensorContactId(99)).Outcome
        );
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(new SensorContactId(1)).Outcome);
        Assert.Equal(
            ActiveSensorScanOutcome.ScanAlreadyActive,
            game.RequestActiveSensorScan(new SensorContactId(2)).Outcome
        );
        Assert.DoesNotContain(PlayerAction.ActiveSensorScan, game.GetPlayerProjection().AvailableActions);

        SimulationAdvanceResult completion = game.AdvanceFixedSteps(20);
        PlayerAdvanceEvent completed = Assert.Single(completion.ResolvedEvents);
        SensorContactSnapshot identified = completion.Projection.Ship.Sensors.Contacts[0];
        Assert.Equal(PlayerAdvanceEventKind.ActiveSensorScanCompleted, completed.Kind);
        Assert.Equal(new SensorContactId(1), completed.SensorContactId);
        Assert.Equal(SensorContactIdentification.Identified, identified.Identification);
        Assert.Equal("USS Known", identified.KnownVesselDisplayName);
        Assert.Equal("Target design", identified.KnownDesignDisplayName);
        Assert.Equal(
            ActiveSensorScanOutcome.AlreadyIdentified,
            game.RequestActiveSensorScan(new SensorContactId(1)).Outcome
        );
        game.SetTacticalCourse(new SetTacticalCourseIntent(new HeadingDegrees(270), new SpeedKilometersPerSecond(10)));
        game.AdvanceFixedSteps(6);
        Assert.Equal(
            ActiveSensorScanOutcome.ContactNotCurrent,
            game.RequestActiveSensorScan(new SensorContactId(1)).Outcome
        );
    }

    /// <summary>Confirms unavailable sensors are checked after valid local contact state.</summary>
    [Fact]
    public void ActiveScanRejectsUnavailableSensorsWithoutMutation()
    {
        GameSimulation observed = CreateGame(
            Ship(1, ObserverDefinitionId, default),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0))
        );
        observed.AdvanceFixedSteps(1);
        SimulationState state = observed.CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        state = state.ReplaceShip(player.InstanceId, player with { SensorIntegrity = new SensorIntegrity(0) });
        var unavailable = GameSimulation.RestoreState(state, CreateCatalog());
        PlayerProjection before = unavailable.GetPlayerProjection();

        ActiveSensorScanResult result = unavailable.RequestActiveSensorScan(new SensorContactId(1));

        Assert.Equal(ActiveSensorScanOutcome.SensorsUnavailable, result.Outcome);
        Assert.Equal(before, unavailable.GetPlayerProjection());
    }

    /// <summary>Confirms a contact leaving range interrupts a scan and stale precedence remains stable.</summary>
    [Fact]
    public void ActiveScanIsInterruptedWhenContactCeasesToBeCurrent()
    {
        GameSimulation game = CreateGame(
            Ship(1, ObserverDefinitionId, default),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0))
        );
        game.AdvanceFixedSteps(1);
        game.RequestActiveSensorScan(new SensorContactId(1));
        game.SetTacticalCourse(new SetTacticalCourseIntent(new HeadingDegrees(270), new SpeedKilometersPerSecond(10)));

        SimulationAdvanceResult result = game.AdvanceFixedSteps(6);

        Assert.Equal(
            [PlayerAdvanceEventKind.SensorContactStale, PlayerAdvanceEventKind.ActiveSensorScanInterrupted],
            result.ResolvedEvents.Select(playerEvent => playerEvent.Kind)
        );
        Assert.Null(result.Projection.Ship.Sensors.ActiveScanContactId);
        Assert.Equal(
            ActiveSensorScanOutcome.ContactNotCurrent,
            game.RequestActiveSensorScan(new SensorContactId(1)).Outcome
        );
    }

    /// <summary>Confirms one large advancement and one-hundred-millisecond calls produce identical state.</summary>
    [Fact]
    public void ContactMaterializationIsBatchEquivalent()
    {
        GameSimulation large = CreateGame(
            Ship(1, ObserverDefinitionId, default),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0))
        );
        GameSimulation singles = CreateGame(
            Ship(1, ObserverDefinitionId, default),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0))
        );
        var course = new SetTacticalCourseIntent(new HeadingDegrees(270), new SpeedKilometersPerSecond(10));
        large.SetTacticalCourse(course);
        singles.SetTacticalCourse(course);

        SimulationAdvanceResult batched = large.AdvanceFixedSteps(60);
        var singleEvents = new List<PlayerAdvanceEvent>();
        for (int index = 0; index < 60; index++)
        {
            singleEvents.AddRange(singles.AdvanceFixedSteps(1).ResolvedEvents);
        }

        Assert.Equal(singleEvents, batched.ResolvedEvents);
        Assert.Equal(singles.GetPlayerProjection(), large.GetPlayerProjection());
        Assert.Equal(singles.CaptureState().Scheduler.OutstandingWork, large.CaptureState().Scheduler.OutstandingWork);
    }

    /// <summary>Confirms advance-until stops early only when an existing player-work horizon permits it.</summary>
    [Fact]
    public void AdvanceUntilUsesExistingPlayerHorizonForObservationEvents()
    {
        GameSimulation withoutHorizon = CreateGame(
            Ship(1, ObserverDefinitionId, default),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0))
        );
        AdvanceUntilResult noOp = withoutHorizon.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(AdvanceUntilOutcome.NoPlayerEvent, noOp.Outcome);
        Assert.Equal(0, noOp.StoppedAt.Milliseconds);
        Assert.Empty(noOp.Projection.Ship.Sensors.Contacts);

        GameSimulation withHorizon = CreateGame(
            Ship(
                1,
                ObserverDefinitionId,
                default,
                integrity: 0.5,
                repair: new SensorRepairStart(new SensorIntegrity(0.5), new SensorIntegrity(1), new SimulationTime(0))
            ),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0))
        );
        AdvanceUntilResult detected = withHorizon.AdvanceUntilNextPlayerRelevantEvent();

        Assert.Equal(AdvanceUntilOutcome.PlayerEventResolved, detected.Outcome);
        Assert.Equal(100, detected.StoppedAt.Milliseconds);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactDetected, Assert.Single(detected.ResolvedEvents).Kind);
        Assert.True(detected.Projection.Ship.Sensors.IsRepairing);
    }

    /// <summary>Confirms the pure targetable seam applies NPC courses without changing player authority.</summary>
    [Fact]
    public void TargetableTacticalCourseApplicationPreservesPlayerWrapperBehavior()
    {
        ShipDefinitionCatalog catalog = CreateCatalog();
        GameSimulation game = CreateGame(
            Ship(1, ObserverDefinitionId, default),
            Ship(2, TargetDefinitionId, new TacticalPosition(5, 0))
        );
        SimulationState initial = game.CaptureState();

        TacticalCourseApplicationResult accepted = GameSimulation.ApplyTacticalCourse(
            initial,
            catalog,
            new TargetableTacticalCourseCommand(
                new ShipInstanceId(2),
                new HeadingDegrees(90),
                new SpeedKilometersPerSecond(20)
            )
        );
        TacticalCourseApplicationResult rejected = GameSimulation.ApplyTacticalCourse(
            initial,
            catalog,
            new TargetableTacticalCourseCommand(
                new ShipInstanceId(2),
                new HeadingDegrees(90),
                new SpeedKilometersPerSecond(21)
            )
        );

        Assert.Equal(SetTacticalCourseOutcome.Accepted, accepted.Outcome);
        Assert.Equal(20, accepted.CandidateState.GetRequiredShip(new ShipInstanceId(2)).TacticalMotion.Speed.Value);
        Assert.Equal(default, accepted.CandidateState.GetRequiredShip(new ShipInstanceId(1)).TacticalMotion);
        Assert.Equal(SetTacticalCourseOutcome.SpeedExceedsMaximum, rejected.Outcome);
        Assert.Same(initial, rejected.CandidateState);
    }

    private static GameSimulation CreateGame(params ShipStart[] starts) => CreateGame((IEnumerable<ShipStart>)starts);

    private static GameSimulation CreateGame(IEnumerable<ShipStart> starts)
    {
        var map = new StrategicMap(
            [new StrategicLocation(Local, "Local", default), new StrategicLocation(Remote, "Remote", default)],
            []
        );
        return new GameBootstrap(new SimulationTime(0), map, new ShipInstanceId(1), starts).CreateSimulation(
            CreateCatalog()
        );
    }

    private static ShipStart Ship(
        long id,
        ShipDefinitionId definitionId,
        TacticalPosition position,
        LocationId? location = null,
        double integrity = 1,
        string? vesselName = null,
        SensorRepairStart? repair = null
    ) =>
        new(
            new ShipInstanceId(id),
            definitionId,
            vesselName ?? $"Ship {id}",
            position,
            default,
            new SensorIntegrity(integrity),
            new AtLocationStart(location ?? Local),
            repair
        );

    private static ShipDefinitionCatalog CreateCatalog() =>
        new(
            new Dictionary<ShipDefinitionId, ShipDefinition>
            {
                [ObserverDefinitionId] = Definition(ObserverDefinitionId, "Observer design", 10, 100),
                [TargetDefinitionId] = Definition(TargetDefinitionId, "Target design", 4, 20),
            }
        );

    private static ShipDefinition Definition(
        ShipDefinitionId id,
        string name,
        double passiveRange,
        double maximumSpeed
    ) =>
        new(
            id,
            name,
            new SpeedKilometersPerSecond(maximumSpeed),
            new DistanceKilometers(passiveRange),
            new SimulationDuration(2_000),
            new SimulationDuration(8_000)
        );
}
