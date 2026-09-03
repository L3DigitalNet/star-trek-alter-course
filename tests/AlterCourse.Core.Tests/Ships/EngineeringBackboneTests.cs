using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
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

    /// <summary>Confirms addition reaches the declared maximum and rejects an overflowing result.</summary>
    [Fact]
    public void PowerUnitsAdditionIsCheckedAndBounded()
    {
        Assert.Equal(new PowerUnits(PowerUnits.MaximumValue), new PowerUnits(400_000) + new PowerUnits(600_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PowerUnits(PowerUnits.MaximumValue) + new PowerUnits(1));
    }

    /// <summary>Confirms condition includes both semantic endpoints.</summary>
    [Fact]
    public void SystemConditionAcceptsOfflineAndNominalEndpoints()
    {
        Assert.Equal(SystemConditionStatus.Offline, new SystemCondition(0).Status);
        Assert.Equal(SystemConditionStatus.Nominal, new SystemCondition(1).Status);
    }

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

    /// <summary>Confirms floor rounding is stable at representative exact and fractional boundaries.</summary>
    [Theory]
    [InlineData(10, 0, 0)]
    [InlineData(10, 0.69, 6)]
    [InlineData(10, 0.7, 7)]
    [InlineData(999_999, 1, 999_999)]
    public void AvailablePowerUsesDeterministicFloorRounding(int generation, double condition, int expected)
    {
        ShipEngineeringDefinition definition = Definition(generation, 1, 1);
        ShipEngineeringState engineering = Engineering(condition, 1, 1, 0, 0);

        Assert.Equal(new PowerUnits(expected), engineering.AvailablePower(definition));
    }

    /// <summary>Confirms every preset conserves available power and respects each authored demand.</summary>
    [Theory]
    [InlineData(120, 70, 50, 0)]
    [InlineData(11, 7, 5, 73)]
    [InlineData(100, 3, 2, 100)]
    [InlineData(1_000_000, 600_000, 400_000, 99)]
    public void PresetsConservePowerWithinDemand(
        int generation,
        int sensorDemand,
        int impulseDemand,
        int generationPercent
    )
    {
        ShipEngineeringDefinition definition = Definition(generation, sensorDemand, impulseDemand);
        ShipEngineeringState engineering = Engineering(generationPercent / 100d, 1, 1, 0, 0);

        foreach (PowerAllocationPreset preset in Enum.GetValues<PowerAllocationPreset>())
        {
            PowerAllocation allocation = engineering.AllocationFor(definition, preset);
            ShipEngineeringState allocated = engineering with { Allocation = allocation };
            int available = engineering.AvailablePower(definition).Value;

            Assert.InRange(allocation.Sensors.Value, 0, sensorDemand);
            Assert.InRange(allocation.ImpulsePropulsion.Value, 0, impulseDemand);
            Assert.Equal(
                available,
                allocation.Sensors.Value + allocation.ImpulsePropulsion.Value + allocated.Reserve(definition).Value
            );
        }
    }

    /// <summary>Confirms the balanced preset assigns indivisible remainders to sensors first.</summary>
    [Theory]
    [InlineData(5, 4, 3, 3, 2)]
    [InlineData(7, 5, 3, 5, 2)]
    [InlineData(8, 5, 4, 5, 3)]
    public void BalancedPresetUsesStableSensorFirstRemainder(
        int available,
        int sensorDemand,
        int impulseDemand,
        int expectedSensors,
        int expectedImpulse
    )
    {
        ShipEngineeringDefinition definition = Definition(available, sensorDemand, impulseDemand);
        ShipEngineeringState engineering = Engineering(1, 1, 1, 0, 0);

        PowerAllocation allocation = engineering.AllocationFor(definition, PowerAllocationPreset.Balanced);

        Assert.Equal(new PowerAllocation(new PowerUnits(expectedSensors), new PowerUnits(expectedImpulse)), allocation);
    }

    /// <summary>Confirms capability remains bounded and is monotonic in allocation and condition.</summary>
    [Theory]
    [InlineData(0, 0, 0.5, 0)]
    [InlineData(10, 20, 0.25, 0.5)]
    [InlineData(50, 70, 0.5, 1)]
    public void CapabilityIsBoundedAndMonotonic(
        int lowerAllocation,
        int higherAllocation,
        double lowerCondition,
        double higherCondition
    )
    {
        ShipEngineeringDefinition definition = Definition(120, 70, 50);
        ShipEngineeringState lowerPower = Engineering(1, higherCondition, 1, lowerAllocation, 0);
        ShipEngineeringState higherPower = Engineering(1, higherCondition, 1, higherAllocation, 0);
        ShipEngineeringState lowerConditionState = Engineering(1, lowerCondition, 1, higherAllocation, 0);

        double lowerPowerCapability = lowerPower.SensorCapability(definition);
        double higherPowerCapability = higherPower.SensorCapability(definition);
        double lowerConditionCapability = lowerConditionState.SensorCapability(definition);

        Assert.InRange(lowerPowerCapability, 0, 1);
        Assert.InRange(higherPowerCapability, 0, 1);
        Assert.InRange(lowerConditionCapability, 0, 1);
        Assert.True(lowerPowerCapability <= higherPowerCapability);
        Assert.True(lowerConditionCapability <= higherPowerCapability);
    }

    /// <summary>Confirms zero input removes capability and nominal input reproduces authored capability.</summary>
    [Fact]
    public void CapabilityEndpointsHaveZeroAndIdentitySemantics()
    {
        ShipEngineeringDefinition definition = Definition(120, 70, 50);
        ShipEngineeringState offline = Engineering(1, 0, 0, 70, 50);
        ShipEngineeringState unpowered = Engineering(1, 1, 1, 0, 0);
        ShipEngineeringState nominal = Engineering(1, 1, 1, 70, 50);

        Assert.Equal(0, offline.SensorCapability(definition));
        Assert.Equal(0, offline.ImpulseCapability(definition));
        Assert.Equal(0, unpowered.SensorCapability(definition));
        Assert.Equal(0, unpowered.ImpulseCapability(definition));
        Assert.Equal(1, nominal.SensorCapability(definition));
        Assert.Equal(1, nominal.ImpulseCapability(definition));
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

    /// <summary>Confirms each direct allocation bound rejects without replacing or changing the aggregate.</summary>
    [Theory]
    [InlineData(71, 0, PowerAllocationOutcome.SensorDemandExceeded)]
    [InlineData(0, 51, PowerAllocationOutcome.ImpulseDemandExceeded)]
    [InlineData(70, 50, PowerAllocationOutcome.AvailablePowerExceeded)]
    public void InvalidDirectAllocationPreservesCompleteAggregateIdentity(
        int sensors,
        int impulse,
        PowerAllocationOutcome expectedOutcome
    )
    {
        GameSimulation game = CreateDefault();
        SimulationState before = game.CaptureState();

        PowerAllocationResult result = game.SetPowerAllocation(
            new PowerAllocation(new PowerUnits(sensors), new PowerUnits(impulse))
        );

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Empty(result.ResolvedEvents);
        Assert.Same(before, game.CaptureState());
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

    /// <summary>Confirms impulse repair timing, identity, single completion, and save continuation.</summary>
    [Fact]
    public void ImpulseRepairProgressesAndResumesThroughExactAuthoredCompletion()
    {
        GameSimulation uninterrupted = CreateSingleShip(new SystemCondition(0.5), new SystemCondition(0.5));
        Assert.Equal(
            SystemRepairOutcome.Accepted,
            uninterrupted.BeginSystemRepair(ShipSystemId.ImpulsePropulsion, new SystemCondition(1)).Outcome
        );
        SystemRepairState started = Assert.IsType<SystemRepairState>(
            uninterrupted.CaptureState().GetRequiredShip(new ShipInstanceId(1)).Engineering.ActiveRepair
        );
        Assert.Equal(new SimulationTime(0), started.StartedAt);
        Assert.Equal(new SimulationTime(6_000), started.ExpectedCompletion);
        Assert.Contains(
            uninterrupted.CaptureState().Scheduler.OutstandingWork,
            work =>
                work.Id == started.ScheduledCompletionId
                && work.DueTime == new SimulationTime(6_000)
                && work.Kind == ScheduledWorkKind.SystemRepairCompletion
        );

        SimulationAdvanceResult midpoint = uninterrupted.AdvanceFixedSteps(30);
        Assert.Equal(new SimulationTime(3_000), midpoint.FinalTime);
        Assert.Equal(0.5, midpoint.Projection.Ship.Engineering.SensorCondition.Value, 12);
        Assert.Equal(0.75, midpoint.Projection.Ship.Engineering.ImpulseCondition.Value, 12);
        Assert.Equal(0.5, midpoint.Projection.Ship.Engineering.ActiveRepair!.Progress, 12);

        GameSaveMetadata metadata = ImpulseRepairMetadata();
        LoadedGameSave loaded = GamePersistence.Deserialize(
            GamePersistence.Serialize(uninterrupted, metadata),
            CreateCatalog(),
            "impulse-midpoint.json"
        );

        SimulationAdvanceResult completion = uninterrupted.AdvanceFixedSteps(30);
        SimulationAdvanceResult resumedCompletion = loaded.Simulation.AdvanceFixedSteps(30);
        Assert.Equal(completion, resumedCompletion);
        Assert.Equal(new SimulationTime(6_000), completion.FinalTime);
        PlayerAdvanceEvent resolved = Assert.Single(completion.ResolvedEvents);
        Assert.Equal(PlayerAdvanceEventKind.SystemRepairCompleted, resolved.Kind);
        Assert.Equal(new SimulationTime(6_000), resolved.OccurredAt);
        Assert.Equal(ShipSystemId.ImpulsePropulsion, resolved.ShipSystemId);
        Assert.Equal(0.5, completion.Projection.Ship.Engineering.SensorCondition.Value, 12);
        Assert.Equal(1, completion.Projection.Ship.Engineering.ImpulseCondition.Value);
        Assert.Null(completion.Projection.Ship.Engineering.ActiveRepair);
        Assert.DoesNotContain(
            uninterrupted.CaptureState().Scheduler.OutstandingWork,
            work => work.Kind == ScheduledWorkKind.SystemRepairCompletion
        );
        Assert.Equal(
            GamePersistence.Serialize(uninterrupted, metadata),
            GamePersistence.Serialize(loaded.Simulation, loaded.Metadata)
        );

        SimulationAdvanceResult afterCompletion = uninterrupted.AdvanceFixedSteps(1);
        SimulationAdvanceResult resumedAfterCompletion = loaded.Simulation.AdvanceFixedSteps(1);
        Assert.Equal(afterCompletion, resumedAfterCompletion);
        Assert.DoesNotContain(
            afterCompletion.ResolvedEvents,
            item => item.Kind == PlayerAdvanceEventKind.SystemRepairCompleted
        );
    }

    /// <summary>Confirms one repair slot and the deliberate generator-repair exclusion.</summary>
    [Fact]
    public void RepairValidationIsOneAtATimeAndRejectsGenerator()
    {
        GameSimulation game = CreateSingleShip(new SystemCondition(0.5), new SystemCondition(0.5));

        Assert.Equal(
            SystemRepairOutcome.UnsupportedSystem,
            game.BeginSystemRepair(default, new SystemCondition(1)).Outcome
        );
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

    /// <summary>Confirms a nominal system cannot consume the sole repair slot.</summary>
    [Fact]
    public void RepairOfNominalSystemIsRejectedWithoutStateChange()
    {
        GameSimulation game = CreateSingleShip(new SystemCondition(1), new SystemCondition(1));
        PlayerProjection before = game.GetPlayerProjection();

        SystemRepairResult result = game.BeginSystemRepair(ShipSystemId.Sensors, new SystemCondition(1));

        Assert.Equal(SystemRepairOutcome.TargetDoesNotImproveCondition, result.Outcome);
        Assert.Equal(before, game.GetPlayerProjection());
    }

    /// <summary>Confirms an impulse repair does not masquerade as sensor repair state.</summary>
    [Fact]
    public void ImpulseRepairAppearsOnlyInEngineeringProjection()
    {
        GameSimulation game = CreateSingleShip(new SystemCondition(0.5), new SystemCondition(0.5));
        Assert.Equal(
            SystemRepairOutcome.Accepted,
            game.BeginSystemRepair(ShipSystemId.ImpulsePropulsion, new SystemCondition(1)).Outcome
        );

        PlayerShipProjection projection = game.GetPlayerProjection().Ship;

        Assert.False(projection.Sensors.IsRepairing);
        Assert.Equal(1, projection.Sensors.RepairProgress);
        Assert.Equal(ShipSystemId.ImpulsePropulsion, projection.Engineering.ActiveRepair!.TargetSystem);
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

    private static ShipEngineeringState Engineering(
        double generationCondition,
        double sensorCondition,
        double impulseCondition,
        int sensorAllocation,
        int impulseAllocation
    ) =>
        new(
            new SystemCondition(generationCondition),
            new SystemCondition(sensorCondition),
            new SystemCondition(impulseCondition),
            new PowerAllocation(new PowerUnits(sensorAllocation), new PowerUnits(impulseAllocation))
        );

    private static ShipEngineeringDefinition Definition(int generation, int sensorDemand, int impulseDemand) =>
        new(
            new PowerUnits(generation),
            new PowerUnits(sensorDemand),
            new PowerUnits(impulseDemand),
            new SimulationDuration(100),
            new SimulationDuration(100)
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

    private static GameSaveMetadata ImpulseRepairMetadata()
    {
        var timestamp = new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero);
        return new GameSaveMetadata("impulse-repair", "Impulse Repair", timestamp, timestamp);
    }
}
