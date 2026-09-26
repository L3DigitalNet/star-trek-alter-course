using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Ships;

/// <summary>Verifies deterministic combat engineering and normalized damage contracts.</summary>
public sealed class CombatFoundationTests
{
    /// <summary>Confirms every supported system round trips through condition lookup and replacement.</summary>
    [Theory]
    [InlineData("power-generation")]
    [InlineData("sensors")]
    [InlineData("impulse-propulsion")]
    [InlineData("shields")]
    [InlineData("directed-energy-weapons")]
    public void CombatSystemIdentitiesAreSupported(string name)
    {
        var system = ShipSystemKind.Parse(name);
        Assert.Equal(name, system.Value);
        ShipEngineeringState changed = TestEngineering.WithCondition(State(1, Allocation(0, 0, 0, 0)), system, 0.3);
        Assert.Equal(0.3, TestEngineering.ConditionOf(changed, system));
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
            State(generation, Allocation(0, 0, 0, 0)).BalancedAllocation()
        );

    /// <summary>Confirms each priority consumes its demand before the remaining semantic order.</summary>
    [Theory]
    [InlineData(2L, 70, 5, 0, 0)]
    [InlineData(3L, 25, 50, 0, 0)]
    [InlineData(4L, 35, 0, 40, 0)]
    [InlineData(5L, 45, 0, 0, 30)]
    public void PriorityAllocationIsExact(long priority, int sensors, int impulse, int shields, int weapons) =>
        Assert.Equal(
            Allocation(sensors, impulse, shields, weapons),
            State(0.625, Allocation(0, 0, 0, 0)).PriorityAllocation(new InstalledSystemId(priority))
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
            State(generation, Allocation(45, 32, 25, 18)).ReconcileAvailablePower()
        );

    /// <summary>Confirms remainders skip exhausted earlier consumers without inventing allocations.</summary>
    [Fact]
    public void BrownoutSkipsZeroSharesAndPreservesCoveredReserve()
    {
        Assert.Equal(Allocation(0, 1, 0, 0), State(0.01, Allocation(0, 1, 1, 1)).ReconcileAvailablePower());
        PowerAllocation allocation = Allocation(10, 5, 3, 2);
        Assert.Equal(allocation, State(0.625, allocation).ReconcileAvailablePower());
        Assert.Equal(55, State(0.625, allocation).Reserve.Value);
    }

    /// <summary>Confirms wide proportional products and four-consumer totals remain exact at quantity bounds.</summary>
    [Fact]
    public void HighBoundsUseWideArithmetic()
    {
        var tuning = new PathfinderTuning(
            Generation: 1_000_000,
            SensorDemand: 1_000_000,
            ImpulseDemand: 1_000_000,
            ShieldDemand: 1_000_000,
            WeaponDemand: 1_000_000
        );
        ShipEngineeringState state = TestEngineering.State(
            1,
            Allocation(1_000_000, 1_000_000, 1_000_000, 1_000_000),
            tuning
        );
        Assert.Equal(4_000_000, state.Allocation.Total);
        Assert.Equal(Allocation(250_000, 250_000, 250_000, 250_000), state.ReconcileAvailablePower());
        Assert.Equal(Allocation(250_000, 250_000, 250_000, 250_000), state.BalancedAllocation());
    }

    /// <summary>Confirms validation includes new demands and total available power.</summary>
    [Theory]
    [InlineData(0, 0, 41, 0)]
    [InlineData(0, 0, 0, 31)]
    [InlineData(70, 50, 1, 0)]
    public void InvalidCombatAllocationsAreRejected(int sensors, int impulse, int shields, int weapons) =>
        Assert.Throws<InvalidOperationException>(() =>
            State(1, Allocation(sensors, impulse, shields, weapons)).Validate()
        );

    /// <summary>Confirms over-demand power cannot restore degraded capability.</summary>
    [Fact]
    public void CapabilityMultipliesConditionByCappedPowerSatisfaction()
    {
        // Over-demand allocations are built directly (bypassing the demand invariant) to pin the satisfaction cap.
        ShipEngineeringState state = TestEngineering.State(
            1,
            Allocation(100, 100, 100, 100),
            sensors: 0.4,
            impulse: 0.4,
            shields: 0.4,
            weapons: 0.4
        );
        Assert.Equal(0.4, TestEngineering.CapabilityOf(state, ShipSystemKind.Sensors));
        Assert.Equal(0.4, TestEngineering.CapabilityOf(state, ShipSystemKind.ImpulsePropulsion));
        Assert.Equal(0.4, TestEngineering.CapabilityOf(state, ShipSystemKind.Shields));
        Assert.Equal(0.4, TestEngineering.CapabilityOf(state, ShipSystemKind.DirectedEnergyWeapons));
        state = state.WithAllocation(Allocation(35, 25, 20, 15));
        Assert.Equal(0.2, TestEngineering.CapabilityOf(state, ShipSystemKind.Shields));
        Assert.Equal(0.2, TestEngineering.CapabilityOf(state, ShipSystemKind.DirectedEnergyWeapons));
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
        var system = ShipSystemKind.Parse(name);
        InstalledSystem installed = TestEngineering.Of(State(1, Allocation(0, 0, 0, 0)), system);
        Assert.Equal(duration, installed.Definition.Repair!.FullRepairDuration.Milliseconds);
        var repair = new SystemRepairState(
            installed.Id,
            new SystemCondition(0),
            new SystemCondition(1),
            new SimulationTime(0),
            new SimulationTime(duration),
            new ScheduledWorkId(1)
        );
        Assert.Equal(installed.Id, repair.Target);
        Assert.Null(
            TestEngineering.Of(State(1, Allocation(0, 0, 0, 0)), ShipSystemKind.PowerGeneration).Definition.Repair
        );
    }

    /// <summary>Confirms a loadout without combat systems gains no combat capability implicitly.</summary>
    [Fact]
    public void AbsentCombatSystemsHaveNoCapability()
    {
        ShipEngineeringState state = TestEngineering.State(
            1,
            TestEngineering.Allocation(0, 0),
            PathfinderTuning.Production with
            {
                Combat = false,
            }
        );
        Assert.Empty(state.Systems.OfKind(ShipSystemKind.Shields));
        Assert.Empty(state.Systems.OfKind(ShipSystemKind.DirectedEnergyWeapons));
        Assert.Equal(0, TestEngineering.CapabilityOf(state, ShipSystemKind.Shields));
        Assert.Equal(0, TestEngineering.CapabilityOf(state, ShipSystemKind.DirectedEnergyWeapons));
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

    /// <summary>Confirms shield satisfaction handles zero and capped power.</summary>
    [Fact]
    public void ShieldPowerSatisfactionHasSafeEndpoints()
    {
        Assert.Equal(0, Satisfaction(State(1, Allocation(0, 0, 0, 0))));
        Assert.Equal(0.5, Satisfaction(State(1, Allocation(0, 0, 20, 0))));
        Assert.Equal(1, Satisfaction(State(1, Allocation(0, 0, 40, 0))));
    }

    private static double Satisfaction(ShipEngineeringState state) =>
        ShipEngineeringState.PowerSatisfaction(TestEngineering.Of(state, ShipSystemKind.Shields));

    private static PowerAllocation Allocation(int sensors, int impulse, int shields, int weapons) =>
        TestEngineering.Allocation(sensors, impulse, shields, weapons);

    private static ShipEngineeringState State(double generation, PowerAllocation allocation) =>
        TestEngineering.State(generation, allocation);
}
