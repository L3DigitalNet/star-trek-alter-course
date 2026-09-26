using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Ships;

/// <summary>Verifies deterministic combat engineering and normalized damage contracts.</summary>
public sealed class CombatFoundationTests
{
    private static readonly ShipEngineeringDefinition Definition = new(
        new PowerUnits(120),
        new PowerUnits(70),
        new PowerUnits(50),
        new PowerUnits(40),
        new PowerUnits(30),
        new SimulationDuration(8000),
        new SimulationDuration(6000),
        new SimulationDuration(8000),
        new SimulationDuration(6000)
    );

    /// <summary>Confirms every supported system round trips through condition lookup and replacement.</summary>
    [Theory]
    [InlineData("power-generation")]
    [InlineData("sensors")]
    [InlineData("impulse-propulsion")]
    [InlineData("shields")]
    [InlineData("directed-energy-weapons")]
    public void CombatSystemIdentitiesAreSupported(string name)
    {
        var system = ShipSystemId.Parse(name);
        Assert.Equal(name, system.Value);
        ShipEngineeringState changed = State(1, Allocation(0, 0, 0, 0)).WithCondition(system, new SystemCondition(0.3));
        Assert.Equal(new SystemCondition(0.3), changed.ConditionFor(system));
    }

    /// <summary>Confirms authored proportional presets use one-unit semantic remainders.</summary>
    [Theory]
    [InlineData(0.625, 28, 20, 16, 11)]
    [InlineData(1, 45, 32, 25, 18)]
    public void PathfinderBalancedAllocationIsExact(
        double generation,
        int sensors,
        int impulse,
        int shields,
        int weapons
    ) =>
        Assert.Equal(
            Allocation(sensors, impulse, shields, weapons),
            State(generation, default).AllocationFor(Definition, PowerAllocationPreset.Balanced)
        );

    /// <summary>Confirms each priority consumes its demand before the remaining semantic order.</summary>
    [Theory]
    [InlineData(PowerAllocationPreset.PrioritizeSensors, 70, 5, 0, 0)]
    [InlineData(PowerAllocationPreset.PrioritizePropulsion, 25, 50, 0, 0)]
    [InlineData(PowerAllocationPreset.PrioritizeShields, 35, 0, 40, 0)]
    [InlineData(PowerAllocationPreset.PrioritizeDirectedEnergyWeapons, 45, 0, 0, 30)]
    public void PriorityAllocationIsExact(
        PowerAllocationPreset preset,
        int sensors,
        int impulse,
        int shields,
        int weapons
    ) =>
        Assert.Equal(
            Allocation(sensors, impulse, shields, weapons),
            State(0.625, default).AllocationFor(Definition, preset)
        );

    /// <summary>Confirms brownout preserves covered allocations and scales only existing shares.</summary>
    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0.01, 1, 0, 0, 0)]
    [InlineData(0.5, 23, 16, 12, 9)]
    [InlineData(1, 45, 32, 25, 18)]
    public void BrownoutAllocationIsExact(double generation, int sensors, int impulse, int shields, int weapons) =>
        Assert.Equal(
            Allocation(sensors, impulse, shields, weapons),
            State(generation, Allocation(45, 32, 25, 18)).ReconcileAvailablePower(Definition)
        );

    /// <summary>Confirms remainders skip exhausted earlier consumers without inventing allocations.</summary>
    [Fact]
    public void BrownoutSkipsZeroSharesAndPreservesCoveredReserve()
    {
        Assert.Equal(Allocation(0, 1, 0, 0), State(0.01, Allocation(0, 1, 1, 1)).ReconcileAvailablePower(Definition));
        PowerAllocation allocation = Allocation(10, 5, 3, 2);
        Assert.Equal(allocation, State(0.625, allocation).ReconcileAvailablePower(Definition));
        Assert.Equal(55, State(0.625, allocation).Reserve(Definition).Value);
    }

    /// <summary>Confirms wide proportional products and four-consumer totals remain exact at quantity bounds.</summary>
    [Fact]
    public void HighBoundsUseWideArithmetic()
    {
        var definition = new ShipEngineeringDefinition(
            new PowerUnits(1_000_000),
            new PowerUnits(1_000_000),
            new PowerUnits(1_000_000),
            new PowerUnits(1_000_000),
            new PowerUnits(1_000_000),
            new SimulationDuration(100),
            new SimulationDuration(100),
            new SimulationDuration(100),
            new SimulationDuration(100)
        );
        ShipEngineeringState state = State(1, Allocation(1_000_000, 1_000_000, 1_000_000, 1_000_000));
        Assert.Equal(4_000_000, state.Allocation.Total);
        Assert.Equal(Allocation(250_000, 250_000, 250_000, 250_000), state.ReconcileAvailablePower(definition));
        Assert.Equal(
            Allocation(250_000, 250_000, 250_000, 250_000),
            state.AllocationFor(definition, PowerAllocationPreset.Balanced)
        );
    }

    /// <summary>Confirms validation includes new demands and total available power.</summary>
    [Theory]
    [InlineData(0, 0, 41, 0)]
    [InlineData(0, 0, 0, 31)]
    [InlineData(70, 50, 1, 0)]
    public void InvalidCombatAllocationsAreRejected(int sensors, int impulse, int shields, int weapons) =>
        Assert.Throws<InvalidOperationException>(() =>
            State(1, Allocation(sensors, impulse, shields, weapons)).Validate(Definition)
        );

    /// <summary>Confirms over-demand power cannot restore degraded capability.</summary>
    [Fact]
    public void CapabilityMultipliesConditionByCappedPowerSatisfaction()
    {
        ShipEngineeringState state = State(1, Allocation(100, 100, 100, 100)) with
        {
            SensorCondition = new SystemCondition(0.4),
            ImpulseCondition = new SystemCondition(0.4),
            ShieldCondition = new SystemCondition(0.4),
            DirectedEnergyCondition = new SystemCondition(0.4),
        };
        Assert.Equal(0.4, state.SensorCapability(Definition));
        Assert.Equal(0.4, state.ImpulseCapability(Definition));
        Assert.Equal(0.4, state.ShieldCapability(Definition));
        Assert.Equal(0.4, state.DirectedEnergyCapability(Definition));
        state = state with { Allocation = Allocation(35, 25, 20, 15) };
        Assert.Equal(0.2, state.ShieldCapability(Definition));
        Assert.Equal(0.2, state.DirectedEnergyCapability(Definition));
    }

    /// <summary>Confirms shield depletion and subsystem penetration follow the exact normalized formula.</summary>
    [Theory]
    [InlineData(0.25, 1, 1, 0.75, 0.25, 0)]
    [InlineData(0.25, 0.2, 0.5, 0.1, 0.1, 0.15)]
    [InlineData(0.25, 1, 0, 1, 0, 0.25)]
    [InlineData(0.25, 0, 1, 0, 0, 0.25)]
    [InlineData(1, 0.2, 1, 0, 0.2, 0.8)]
    [InlineData(0, 1, 1, 1, 0, 0)]
    public void ShieldDamageHasExactAbsorptionAndPenetration(
        double damage,
        double condition,
        double power,
        double next,
        double absorbed,
        double penetration
    )
    {
        ShieldDamageResult result = ShieldDamage.Resolve(damage, new SystemCondition(condition), power);
        Assert.Equal(next, result.ShieldCondition.Value, 12);
        Assert.Equal(absorbed, result.AbsorbedDamage, 12);
        Assert.Equal(penetration, result.PenetratingDamage, 12);
        Assert.Equal(Math.Max(0, 0.5 - penetration), result.ApplyTo(new SystemCondition(0.5)).Value, 12);
        Assert.Equal(Math.Max(0, next - penetration), result.ApplyTo(result.ShieldCondition).Value, 12);
    }

    /// <summary>Confirms powered capacity is monotonic and absorbed plus penetrating damage is conserved.</summary>
    [Fact]
    public void ShieldDamageConservesOutputAndIsMonotonic()
    {
        double lastConditionAbsorbed = 0;
        for (int c = 0; c <= 10; c++)
        {
            double lastPowerAbsorbed = 0;
            for (int p = 0; p <= 10; p++)
            {
                ShieldDamageResult result = ShieldDamage.Resolve(0.75, new SystemCondition(c / 10d), p / 10d);
                Assert.Equal(0.75, result.AbsorbedDamage + result.PenetratingDamage, 12);
                Assert.True(result.AbsorbedDamage >= lastPowerAbsorbed);
                Assert.InRange(result.ShieldCondition.Value, 0, c / 10d);
                lastPowerAbsorbed = result.AbsorbedDamage;
            }
            Assert.True(lastPowerAbsorbed >= lastConditionAbsorbed);
            lastConditionAbsorbed = lastPowerAbsorbed;
        }
    }

    /// <summary>Confirms nonfinite and unnormalized shield inputs are rejected.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void ShieldDamageRejectsInvalidInputs(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShieldDamage.Resolve(value, new SystemCondition(1), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShieldDamage.Resolve(1, new SystemCondition(1), value));
    }

    /// <summary>Confirms all consumer systems use the same repair slot while generation remains unrepairable.</summary>
    [Theory]
    [InlineData("sensors", 8000)]
    [InlineData("impulse-propulsion", 6000)]
    [InlineData("shields", 8000)]
    [InlineData("directed-energy-weapons", 6000)]
    public void ConcreteConsumersAreRepairable(string name, long duration)
    {
        var system = ShipSystemId.Parse(name);
        Assert.Equal(duration, Definition.RepairDurationFor(system).Milliseconds);
        var repair = new SystemRepairState(
            system,
            new SystemCondition(0),
            new SystemCondition(1),
            new SimulationTime(0),
            new SimulationTime(duration),
            new ScheduledWorkId(1)
        );
        Assert.Equal(system, repair.TargetSystem);
        Assert.Throws<ArgumentException>(() => Definition.RepairDurationFor(ShipSystemId.PowerGeneration));
    }

    /// <summary>Confirms legacy definitions and state gain no combat capability implicitly.</summary>
    [Fact]
    public void LegacyConstructionKeepsCombatOffline()
    {
        var definition = new ShipEngineeringDefinition(
            new PowerUnits(120),
            new PowerUnits(70),
            new PowerUnits(50),
            new SimulationDuration(8000),
            new SimulationDuration(6000)
        );
        var state = new ShipEngineeringState(
            new SystemCondition(1),
            new SystemCondition(1),
            new SystemCondition(1),
            default
        );
        Assert.Equal(0, state.ShieldCondition.Value);
        Assert.Equal(0, state.DirectedEnergyCondition.Value);
        Assert.Equal(0, state.ShieldCapability(definition));
        Assert.Equal(0, state.DirectedEnergyCapability(definition));
        Assert.Throws<ArgumentException>(() => definition.RepairDurationFor(ShipSystemId.Shields));
    }

    /// <summary>Confirms weapon tuning has positive finite output and aligned timing.</summary>
    [Theory]
    [InlineData(0, 0.25, 2000)]
    [InlineData(20, 0, 2000)]
    [InlineData(20, 1.01, 2000)]
    [InlineData(20, double.NaN, 2000)]
    [InlineData(20, 0.25, 0)]
    [InlineData(20, 0.25, 2050)]
    public void InvalidWeaponTuningIsRejected(double range, double damage, long cooldown) =>
        Assert.ThrowsAny<ArgumentException>(() =>
            new DirectedEnergyWeaponDefinition(new DistanceKilometers(range), damage, new SimulationDuration(cooldown))
        );

    /// <summary>Confirms shield satisfaction handles absent capacity and caps excess power.</summary>
    [Fact]
    public void ShieldPowerSatisfactionHasSafeEndpoints()
    {
        Assert.Equal(0, State(1, default).ShieldPowerSatisfaction(Definition));
        Assert.Equal(0.5, State(1, Allocation(0, 0, 20, 0)).ShieldPowerSatisfaction(Definition));
        Assert.Equal(1, State(1, Allocation(0, 0, 80, 0)).ShieldPowerSatisfaction(Definition));
        var legacy = new ShipEngineeringDefinition(
            new PowerUnits(120),
            new PowerUnits(70),
            new PowerUnits(50),
            new SimulationDuration(8000),
            new SimulationDuration(6000)
        );
        Assert.Equal(0, State(1, default).ShieldPowerSatisfaction(legacy));
    }

    private static PowerAllocation Allocation(int sensors, int impulse, int shields, int weapons) =>
        new(new PowerUnits(sensors), new PowerUnits(impulse), new PowerUnits(shields), new PowerUnits(weapons));

    private static ShipEngineeringState State(double generation, PowerAllocation allocation) =>
        new(
            new SystemCondition(generation),
            new SystemCondition(1),
            new SystemCondition(1),
            new SystemCondition(1),
            new SystemCondition(1),
            allocation
        );
}
