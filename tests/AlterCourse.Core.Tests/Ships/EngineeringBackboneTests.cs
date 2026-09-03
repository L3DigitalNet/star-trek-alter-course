using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Ships;

/// <summary>Verifies the concrete Milestone 4 Engineering authority and its gameplay consequences.</summary>
public sealed class EngineeringBackboneTests
{
    private static readonly ShipEngineeringDefinition PathfinderEngineering = new(
        new PowerUnits(120),
        new PowerUnits(70),
        new PowerUnits(50),
        new SimulationDuration(8000),
        new SimulationDuration(6000)
    );

    /// <summary>Confirms the abstract quantity includes both declared endpoints.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1_000_000)]
    public void PowerUnitsAcceptInclusiveBounds(int value) => Assert.Equal(value, new PowerUnits(value).Value);

    /// <summary>Confirms the abstract quantity rejects values outside its persisted range.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    public void PowerUnitsRejectOutsideBounds(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PowerUnits(value));

    /// <summary>Confirms condition rejects nonfinite and out-of-range inputs.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void SystemConditionRejectsNonfiniteAndOutsideBounds(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemCondition(value));

    /// <summary>Confirms semantic identities do not depend on enum ordinals or labels.</summary>
    [Fact]
    public void SystemIdentitiesUseOnlyStableSemanticNames()
    {
        Assert.Equal("power-generation", ShipSystemId.PowerGeneration.Value);
        Assert.Equal("sensors", ShipSystemId.Sensors.Value);
        Assert.Equal("impulse-propulsion", ShipSystemId.ImpulsePropulsion.Value);
        Assert.Equal(ShipSystemId.Sensors, ShipSystemId.Parse("sensors"));
        Assert.Throws<ArgumentException>(() => ShipSystemId.Parse("Sensors"));
    }

    /// <summary>Confirms every constrained Pathfinder preset has its exact deterministic result.</summary>
    [Fact]
    public void PathfinderConstrainedPresetsAreDeterministicAndBounded()
    {
        ShipEngineeringState engineering = ConstrainedEngineering();

        Assert.Equal(new PowerUnits(75), engineering.AvailablePower(PathfinderEngineering));
        Assert.Equal(
            new PowerAllocation(new PowerUnits(44), new PowerUnits(31)),
            engineering.AllocationFor(PathfinderEngineering, PowerAllocationPreset.Balanced)
        );
        Assert.Equal(
            new PowerAllocation(new PowerUnits(70), new PowerUnits(5)),
            engineering.AllocationFor(PathfinderEngineering, PowerAllocationPreset.PrioritizeSensors)
        );
        Assert.Equal(
            new PowerAllocation(new PowerUnits(25), new PowerUnits(50)),
            engineering.AllocationFor(PathfinderEngineering, PowerAllocationPreset.PrioritizePropulsion)
        );
    }

    /// <summary>Confirms the player projection derives all Engineering values from the aggregate.</summary>
    [Fact]
    public void DefaultProjectionDerivesPowerCapabilityAndRangeFromOneAuthority()
    {
        EngineeringProjection engineering = CreateDefault().GetPlayerProjection().Ship.Engineering;

        Assert.Equal(new PowerUnits(120), engineering.NominalGeneration);
        Assert.Equal(new PowerUnits(75), engineering.AvailablePower);
        Assert.Equal(new PowerUnits(44), engineering.SensorAllocation);
        Assert.Equal(new PowerUnits(31), engineering.ImpulseAllocation);
        Assert.Equal(new PowerUnits(0), engineering.Reserve);
        Assert.Equal(44d / 70 * 0.4, engineering.SensorCapability, 12);
        Assert.Equal(31d / 50, engineering.ImpulseCapability, 12);
        Assert.Equal(30 * 44d / 70 * 0.4, engineering.EffectivePassiveSensorRange.Value, 12);
        Assert.Equal(6.2, engineering.EffectiveMaximumTacticalSpeed.Value, 12);
    }

    /// <summary>Confirms speed-dependent allocation rejection leaves all observable state unchanged.</summary>
    [Fact]
    public void AllocationThatWouldInvalidateCurrentSpeedIsAtomic()
    {
        GameSimulation game = CreateDefault();
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(new SetTacticalCourseIntent(default, new SpeedKilometersPerSecond(6))).Outcome
        );
        PlayerProjection before = game.GetPlayerProjection();

        PowerAllocationResult result = game.ApplyPowerAllocationPreset(PowerAllocationPreset.PrioritizeSensors);

        Assert.Equal(PowerAllocationOutcome.CurrentSpeedExceedsResultingMaximum, result.Outcome);
        Assert.Empty(result.ResolvedEvents);
        Assert.Equal(before, game.GetPlayerProjection());
    }

    /// <summary>Confirms a zero sensor boundary interrupts its exact scan at the command time.</summary>
    [Fact]
    public void ZeroSensorAllocationCancelsExactScanAndReconcilesContactsAtSameTime()
    {
        GameSimulation game = CreateDefault();
        Assert.Equal(
            PowerAllocationOutcome.Accepted,
            game.ApplyPowerAllocationPreset(PowerAllocationPreset.PrioritizeSensors).Outcome
        );
        SimulationAdvanceResult acquisition = game.AdvanceFixedSteps(35);
        SensorContactSnapshot contact = Assert.Single(acquisition.Projection.Ship.Sensors.Contacts);
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(contact.Id).Outcome);

        PowerAllocationResult result = game.SetPowerAllocation(
            new PowerAllocation(new PowerUnits(0), new PowerUnits(50))
        );

        Assert.Equal(PowerAllocationOutcome.Accepted, result.Outcome);
        Assert.Contains(result.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.SensorContactStale);
        Assert.Contains(result.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.ActiveSensorScanInterrupted);
        Assert.Null(game.GetPlayerProjection().Ship.Sensors.ActiveScanContactId);
        Assert.DoesNotContain(
            game.CaptureState().Scheduler.OutstandingWork,
            item => item.Kind == ScheduledWorkKind.ActiveSensorScanCompletion
        );
    }

    /// <summary>Confirms analytical repair materialization and exact completion identity.</summary>
    [Fact]
    public void SensorRepairProgressesAnalyticallyAndCompletesWithExactIdentity()
    {
        GameSimulation game = CreateDefault();

        SimulationAdvanceResult midpoint = game.AdvanceFixedSteps(40);
        Assert.Equal(0.7, midpoint.Projection.Ship.Engineering.SensorCondition.Value, 12);
        Assert.Equal(0.5, midpoint.Projection.Ship.Engineering.ActiveRepair!.Progress, 12);

        SimulationAdvanceResult completion = game.AdvanceFixedSteps(40);
        PlayerAdvanceEvent resolved = Assert.Single(
            completion.ResolvedEvents,
            item => item.Kind == PlayerAdvanceEventKind.SystemRepairCompleted
        );
        Assert.Equal(ShipSystemId.Sensors, resolved.ShipSystemId);
        Assert.Equal(1, completion.Projection.Ship.Engineering.SensorCondition.Value);
        Assert.Null(completion.Projection.Ship.Engineering.ActiveRepair);
    }

    /// <summary>Confirms one repair slot and the deliberate generator-repair exclusion.</summary>
    [Fact]
    public void RepairValidationIsOneAtATimeAndRejectsGenerator()
    {
        GameSimulation game = CreateSingleShip(new SystemCondition(0.5), new SystemCondition(0.5));

        Assert.Equal(
            SystemRepairOutcome.UnsupportedSystem,
            game.BeginSystemRepair(ShipSystemId.PowerGeneration, new SystemCondition(1)).Outcome
        );
        Assert.Equal(
            SystemRepairOutcome.Accepted,
            game.BeginSystemRepair(ShipSystemId.ImpulsePropulsion, new SystemCondition(1)).Outcome
        );
        Assert.Equal(
            SystemRepairOutcome.RepairAlreadyActive,
            game.BeginSystemRepair(ShipSystemId.Sensors, new SystemCondition(1)).Outcome
        );
    }

    /// <summary>Confirms tactical commands use current effective propulsion rather than design maximum.</summary>
    [Fact]
    public void EffectivePropulsionBoundsTacticalCommands()
    {
        GameSimulation game = CreateDefault();

        Assert.Equal(
            SetTacticalCourseOutcome.SpeedExceedsCurrentCapability,
            game.SetTacticalCourse(new SetTacticalCourseIntent(default, new SpeedKilometersPerSecond(6.21))).Outcome
        );
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(new SetTacticalCourseIntent(default, new SpeedKilometersPerSecond(6.2))).Outcome
        );
    }

    /// <summary>Confirms the public projection excludes mutable, scheduled, and target-owned truth.</summary>
    [Fact]
    public void PublicEngineeringProjectionDoesNotExposeAggregateSchedulerOrNpcTruth()
    {
        Type[] forbidden = [typeof(GameSimulation), typeof(SimulationScheduler), typeof(ShipEngineeringState)];

        Assert.DoesNotContain(
            typeof(EngineeringProjection).GetProperties(),
            property => forbidden.Contains(property.PropertyType)
        );
        Assert.DoesNotContain(
            typeof(EngineeringProjection).GetProperties(),
            property => property.Name.Contains("TargetShip", StringComparison.Ordinal)
        );
    }

    private static ShipEngineeringState ConstrainedEngineering() =>
        new(
            new SystemCondition(0.625),
            new SystemCondition(0.4),
            new SystemCondition(1),
            new PowerAllocation(new PowerUnits(44), new PowerUnits(31))
        );

    private static GameSimulation CreateDefault() => FirstGameSetup.Create(CreateCatalog());

    private static GameSimulation CreateSingleShip(SystemCondition sensors, SystemCondition impulse)
    {
        var location = new StrategicLocation(new LocationId("local"), "Local", default);
        var shipId = new ShipInstanceId(1);
        var start = new ShipStart(
            shipId,
            new ShipDefinitionId("pathfinder"),
            "USS Pathfinder",
            default,
            default,
            new SystemCondition(1),
            sensors,
            impulse,
            new PowerAllocation(new PowerUnits(70), new PowerUnits(50)),
            new AtLocationStart(location.Id)
        );
        return new GameBootstrap(
            new SimulationTime(0),
            new StrategicMap([location], []),
            shipId,
            [start]
        ).CreateSimulation(CreateCatalog());
    }

    private static ShipDefinitionCatalog CreateCatalog() =>
        new(
            new Dictionary<ShipDefinitionId, ShipDefinition>
            {
                [new ShipDefinitionId("pathfinder")] = new ShipDefinition(
                    new ShipDefinitionId("pathfinder"),
                    "Pathfinder class",
                    new SpeedKilometersPerSecond(10),
                    new DistanceKilometers(30),
                    new SimulationDuration(2000),
                    PathfinderEngineering
                ),
            }
        );
}
