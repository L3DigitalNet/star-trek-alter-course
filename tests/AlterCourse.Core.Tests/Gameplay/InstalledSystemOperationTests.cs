using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies repair, scan, and their scheduled work are keyed by installed identity.</summary>
public sealed class InstalledSystemOperationTests
{
    private static readonly ShipDefinitionCatalog Catalog = TestShipContent.Pathfinder();
    private static readonly ShipInstanceId Player = new(1);

    /// <summary>A repair completes on the addressed installation and reports its kind and installed identity.</summary>
    [Fact]
    public void RepairCompletesOnItsInstalledIdentity()
    {
        GameSimulation game = Create(sensors: 1, impulse: 0.5);
        game.AdvanceFixedSteps(1);

        Assert.Equal(
            SystemRepairOutcome.Accepted,
            game.BeginSystemRepair(TestShipContent.Impulse, new SystemCondition(1)).Outcome
        );
        AdvanceUntilResult result = game.AdvanceUntilNextPlayerRelevantEvent();

        PlayerAdvanceEvent completed = Assert.Single(
            result.ResolvedEvents,
            item => item.Kind == PlayerAdvanceEventKind.SystemRepairCompleted
        );
        Assert.Equal(ShipSystemKind.ImpulsePropulsion, completed.SystemKind);
        Assert.Equal(TestShipContent.Impulse, completed.InstalledSystemId);
        Assert.Equal(6100, result.StoppedAt.Milliseconds);
        ShipEngineeringState engineering = game.CaptureState().GetRequiredShip(Player).Engineering;
        Assert.Equal(1, TestEngineering.ConditionOf(engineering, ShipSystemKind.ImpulsePropulsion));
        Assert.Null(engineering.ActiveRepair);
    }

    /// <summary>
    /// A stationary observer's sensor repair widens passive range at every step, so a contact appears before the
    /// repair completes; an impulse repair changes no observation and stops only at its completion.
    /// </summary>
    [Fact]
    public void SensorRepairExpandsObservationEachStepButImpulseRepairDoesNot()
    {
        GameSimulation sensorRepair = Create(sensors: 0.3, impulse: 1);
        Assert.Equal(
            SystemRepairOutcome.Accepted,
            sensorRepair.BeginSystemRepair(TestShipContent.Sensors, new SystemCondition(1)).Outcome
        );
        AdvanceUntilResult detected = sensorRepair.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Contains(detected.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.SensorContactDetected);
        Assert.InRange(detected.StoppedAt.Milliseconds, 1, 7999);

        GameSimulation impulseRepair = Create(sensors: 0.3, impulse: 0.5);
        Assert.Equal(
            SystemRepairOutcome.Accepted,
            impulseRepair.BeginSystemRepair(TestShipContent.Impulse, new SystemCondition(1)).Outcome
        );
        AdvanceUntilResult completed = impulseRepair.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(6000, completed.StoppedAt.Milliseconds);
        Assert.Empty(impulseRepair.GetPlayerProjection().Ship.Sensors.Contacts);
    }

    /// <summary>Validation rejects a repair whose target identity the ship does not have.</summary>
    [Fact]
    public void ValidationRejectsRepairOfMissingInstallation()
    {
        GameSimulation game = Create(sensors: 0.5, impulse: 1);
        game.BeginSystemRepair(TestShipContent.Sensors, new SystemCondition(1));
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(Player);
        SystemRepairState active = player.Engineering.ActiveRepair!;
        var repair = new SystemRepairState(
            new InstalledSystemId(77),
            active.StartingCondition,
            active.TargetCondition,
            active.StartedAt,
            active.ExpectedCompletion,
            active.ScheduledCompletionId
        );

        SimulationState invalid = state.ReplaceShip(
            Player,
            player with
            {
                Engineering = player.Engineering.WithRepair(repair),
            }
        );

        Assert.ThrowsAny<Exception>(() => invalid.Validate(Catalog));
    }

    /// <summary>Validation accepts a scan sourced from the installed sensor and rejects one sourced from impulse.</summary>
    [Fact]
    public void ValidationRequiresScanSourceToBeTheInstalledSensor()
    {
        GameSimulation game = Create(sensors: 1, impulse: 1);
        game.AdvanceFixedSteps(1);
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(Player);
        SensorContactTrack contact = player.SensorKnowledge.Contacts.Single();
        (SimulationScheduler scheduler, ScheduledWork work) = state.Scheduler.Schedule(
            state.Time.AdvanceBy(new SimulationDuration(2000)),
            Player,
            ScheduledWorkKind.ActiveSensorScanCompletion
        );
        SimulationState Scan(InstalledSystemId source) =>
            state.ReplaceShip(
                Player,
                player with
                {
                    SensorKnowledge = player.SensorKnowledge with
                    {
                        ActiveScan = new ActiveSensorScanState(contact.Id, source, state.Time, work.DueTime, work.Id),
                    },
                }
            ) with
            {
                Scheduler = scheduler,
            };

        Scan(TestShipContent.Sensors).Validate(Catalog);
        Assert.ThrowsAny<Exception>(() => Scan(TestShipContent.Impulse).Validate(Catalog));
        Assert.ThrowsAny<Exception>(() => Scan(new InstalledSystemId(77)).Validate(Catalog));
    }

    /// <summary>A repair completion with no active repair to own it is an invalid orphan.</summary>
    [Fact]
    public void OrphanRepairCompletionIsRejected()
    {
        SimulationState state = Create(sensors: 1, impulse: 1).CaptureState();
        (SimulationScheduler scheduler, _) = state.Scheduler.Schedule(
            state.Time.AdvanceBy(new SimulationDuration(1000)),
            Player,
            ScheduledWorkKind.SystemRepairCompletion
        );

        Assert.Throws<InvalidOperationException>(() => (state with { Scheduler = scheduler }).Validate(Catalog));
    }

    private static GameSimulation Create(double sensors, double impulse)
    {
        var location = new LocationId("operations");
        var map = new StrategicMap([new StrategicLocation(location, "Operations", default)], []);
        ShipStart Start(long id, double x, ShipSystemsStart systems) =>
            new(
                new ShipInstanceId(id),
                new ShipDefinitionId("pathfinder"),
                "Ship " + id,
                new TacticalPosition(x, 0),
                default,
                new AtLocationStart(location),
                systems
            );
        return new GameBootstrap(
            new SimulationTime(0),
            map,
            Player,
            [
                Start(1, 0, TestShipStarts.Pathfinder(sensors: sensors, impulse: impulse)),
                Start(2, 20, TestShipStarts.Pathfinder()),
            ]
        ).CreateSimulation(Catalog);
    }
}
