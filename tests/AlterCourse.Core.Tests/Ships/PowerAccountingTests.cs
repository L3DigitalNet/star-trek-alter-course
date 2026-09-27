using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Tests.Gameplay;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Ships;

/// <summary>Verifies generic power accounting over arbitrary installed consumers against an independent oracle.</summary>
public sealed class PowerAccountingTests
{
    /// <summary>
    /// Balanced allocation over nonconsecutive identities equals a decimal oracle written here: floor of the
    /// proportional share, then one unit per still-eligible consumer in canonical (common order, identity) order.
    /// </summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.625)]
    [InlineData(0.41)]
    [InlineData(0.01)]
    public void BalancedMatchesDecimalOracleForArbitraryIdentities(double generatorCondition)
    {
        (long Id, int Order, int Demand)[] consumers = [(17, 300, 50), (2, 200, 70), (900, 100, 33)];
        ShipEngineeringState engineering = Build(consumers, 120, generatorCondition);

        PowerAllocation balanced = engineering.BalancedAllocation();

        Dictionary<long, int> expected = BalancedOracle(consumers, engineering.AvailablePower.Value);
        foreach ((long id, _, int demand) in consumers)
        {
            Assert.Equal(expected[id], TestSystems.Share(balanced, id));
            Assert.InRange(TestSystems.Share(balanced, id), 0, demand);
        }

        Assert.Equal(Math.Min(engineering.AvailablePower.Value, 153), balanced.Total);
    }

    /// <summary>Renumbering installations that keep their common orders yields the same canonical vector.</summary>
    [Fact]
    public void RemainderIsStableUnderRenumbering()
    {
        (long, int, int)[] original = [(17, 300, 50), (2, 200, 70), (900, 100, 33)];
        (long, int, int)[] renumbered = [(3, 300, 50), (40, 200, 70), (8, 100, 33)];

        int[] Vector((long Id, int Order, int Demand)[] consumers)
        {
            ShipEngineeringState engineering = Build(consumers, 101, 1);
            PowerAllocation balanced = engineering.BalancedAllocation();
            return [.. consumers.OrderBy(entry => entry.Order).Select(entry => TestSystems.Share(balanced, entry.Id))];
        }

        Assert.Equal(Vector(original), Vector(renumbered));
    }

    /// <summary>A ship with no consumers has an empty allocation and keeps all available power in reserve.</summary>
    [Fact]
    public void NoConsumersYieldsEmptyAllocationAndFullReserve()
    {
        ShipEngineeringState engineering = TestSystems.Engineering(
            TestSystems.Install(7, TestSystems.Generator("g", 120), condition: 0.5)
        );

        Assert.Empty(engineering.BalancedAllocation().Entries);
        Assert.Empty(engineering.Allocation.Entries);
        Assert.Equal(60, engineering.AvailablePower.Value);
        Assert.Equal(engineering.AvailablePower, engineering.Reserve);
    }

    /// <summary>Without a generator nothing is available, so only an all-zero allocation is valid.</summary>
    [Fact]
    public void NoGeneratorAdmitsOnlyZeroAllocation()
    {
        ShipEngineeringState zero = TestSystems.Engineering(
            TestSystems.Install(2, TestSystems.Consumer("a", 200, 70), 0),
            TestSystems.Install(3, TestSystems.Consumer("b", 300, 50), 0)
        );

        Assert.Equal(0, zero.AvailablePower.Value);
        Assert.Equal(0, zero.NominalGeneration.Value);
        Assert.All(zero.BalancedAllocation().Entries, entry => Assert.Equal(0, entry.Allocation.Value));
        zero.Validate();
        ShipEngineeringState powered = zero.WithAllocation(
            new PowerAllocation([
                new(new InstalledSystemId(2), new PowerUnits(1)),
                new(new InstalledSystemId(3), default),
            ])
        );
        Assert.Throws<InvalidOperationException>(powered.Validate);
    }

    /// <summary>Priority serves the named installed consumer first, then the rest in canonical order.</summary>
    [Fact]
    public void PriorityServesNamedIdentityFirst()
    {
        ShipEngineeringState engineering = Build([(17, 300, 50), (2, 200, 70), (900, 100, 33)], 100, 1);

        PowerAllocation priority = engineering.PriorityAllocation(new InstalledSystemId(17));

        Assert.Equal(50, TestSystems.Share(priority, 17));
        Assert.Equal(33, TestSystems.Share(priority, 900));
        Assert.Equal(17, TestSystems.Share(priority, 2));
        Assert.Throws<ArgumentException>(() => engineering.PriorityAllocation(new InstalledSystemId(1)));
    }

    /// <summary>
    /// Brownout scales the committed shares and never raises a consumer the player left at zero, unlike Balanced,
    /// which redistributes by demand.
    /// </summary>
    [Fact]
    public void BrownoutScalesPriorSharesUnlikeBalanced()
    {
        ShipEngineeringState committed = TestSystems.Engineering(
            TestSystems.Install(1, TestSystems.Generator("g", 120)),
            TestSystems.Install(2, TestSystems.Consumer("a", 200, 70), 70),
            TestSystems.Install(3, TestSystems.Consumer("b", 300, 50), 0),
            TestSystems.Install(4, TestSystems.Consumer("c", 400, 40), 40)
        );
        ShipEngineeringState damaged = committed.WithCondition(new InstalledSystemId(1), new SystemCondition(0.5));

        PowerAllocation brownout = damaged.ReconcileAvailablePower();
        PowerAllocation balanced = damaged.BalancedAllocation();

        // Oracle: 60 × 70/110 = 38.18 → 38, 60 × 40/110 = 21.8 → 21, remainder 1 to the first eligible (id 2).
        Assert.Equal(39, TestSystems.Share(brownout, 2));
        Assert.Equal(0, TestSystems.Share(brownout, 3));
        Assert.Equal(21, TestSystems.Share(brownout, 4));
        Assert.Equal(60, brownout.Total);
        Assert.True(TestSystems.Share(balanced, 3) > 0);
        Assert.Equal(committed.Allocation, committed.ReconcileAvailablePower());
    }

    /// <summary>Fifteen consumers of maximum demand beside a maximum generator stay exact without overflow.</summary>
    [Fact]
    public void MaximumConsumersAndDemandsStayExact()
    {
        (long Id, int Order, int Demand)[] consumers =
        [
            .. Enumerable
                .Range(0, InstalledSystemCollection.MaximumCount - 1)
                .Select(index => ((long)(index + 2), index + 1, PowerUnits.MaximumValue)),
        ];
        ShipEngineeringState engineering = Build(consumers, PowerUnits.MaximumValue, 1);

        PowerAllocation balanced = engineering.BalancedAllocation();

        Assert.Equal(PowerUnits.MaximumValue, balanced.Total);
        Assert.Equal(BalancedOracle(consumers, PowerUnits.MaximumValue), ToDictionary(balanced));
        Assert.Equal(66_667, TestSystems.Share(balanced, 2));
        Assert.Equal(66_666, TestSystems.Share(balanced, 16));

        InstalledSystem[] sixteen =
        [
            .. Enumerable
                .Range(1, InstalledSystemCollection.MaximumCount)
                .Select(index =>
                    TestSystems.Install(index, TestSystems.Consumer($"c{index}", index, PowerUnits.MaximumValue))
                ),
        ];
        PowerAllocation unpowered = TestSystems.Engineering(sixteen).BalancedAllocation();
        Assert.Equal(16, unpowered.Entries.Count);
        Assert.Equal(0, unpowered.Total);
    }

    /// <summary>
    /// Exact allocation outcomes follow the Binding order: unknown consumers (a generator or nonexistent key) before
    /// an incomplete key set, then per-consumer demand, then total power; a refusal changes nothing.
    /// </summary>
    [Theory]
    [InlineData(new long[] { 1, 2, 3, 4, 5 }, new[] { 0, 44, 31, 0, 0 }, PowerAllocationOutcome.UnknownConsumer, 1L)]
    [InlineData(new long[] { 2, 3, 4, 5, 99 }, new[] { 44, 31, 0, 0, 0 }, PowerAllocationOutcome.UnknownConsumer, 99L)]
    [InlineData(new long[] { 1, 2 }, new[] { 0, 44 }, PowerAllocationOutcome.UnknownConsumer, 1L)]
    [InlineData(new long[] { 2, 3, 4 }, new[] { 44, 31, 0 }, PowerAllocationOutcome.IncompleteAllocation, 0L)]
    [InlineData(new long[] { 2, 3, 4, 5 }, new[] { 71, 0, 0, 0 }, PowerAllocationOutcome.ConsumerDemandExceeded, 2L)]
    [InlineData(new long[] { 2, 3, 4, 5 }, new[] { 70, 10, 0, 0 }, PowerAllocationOutcome.AvailablePowerExceeded, 0L)]
    public void ExactAllocationRefusesInBindingOrder(
        long[] keys,
        int[] values,
        PowerAllocationOutcome expected,
        long consumer
    )
    {
        GameSimulation game = new Milestone3ProofFixture().CreateDefault();
        SimulationState before = game.CaptureState();
        var allocation = new PowerAllocation(
            keys.Select(
                (key, index) => new PowerAllocationEntry(new InstalledSystemId(key), new PowerUnits(values[index]))
            )
        );

        PowerAllocationResult result = game.SetPowerAllocation(allocation);

        Assert.Equal(expected, result.Outcome);
        if (consumer != 0)
        {
            Assert.Equal(new InstalledSystemId(consumer), result.Consumer);
        }

        Assert.Empty(result.ResolvedEvents);
        Assert.Same(before, game.CaptureState());
    }

    private static ShipEngineeringState Build(
        (long Id, int Order, int Demand)[] consumers,
        int generatorOutput,
        double generatorCondition
    ) =>
        TestSystems.Engineering([
            TestSystems.Install(1, TestSystems.Generator("g", generatorOutput), condition: generatorCondition),
            .. consumers.Select(entry =>
                TestSystems.Install(entry.Id, TestSystems.Consumer($"c{entry.Id}", entry.Order, entry.Demand), 0)
            ),
        ]);

    /// <summary>Independent decimal oracle for Balanced; deliberately does not call production code.</summary>
    private static Dictionary<long, int> BalancedOracle((long Id, int Order, int Demand)[] consumers, int available)
    {
        (long Id, int Order, int Demand)[] canonical =
        [
            .. consumers.OrderBy(entry => entry.Order).ThenBy(entry => entry.Id),
        ];
        decimal totalDemand = canonical.Sum(entry => (decimal)entry.Demand);
        decimal budget = Math.Min(available, totalDemand);
        Dictionary<long, int> shares = canonical.ToDictionary(
            entry => entry.Id,
            entry => (int)decimal.Floor(budget * entry.Demand / totalDemand)
        );
        decimal remainder = budget - shares.Values.Sum(share => (decimal)share);
        foreach ((long id, _, int demand) in canonical)
        {
            if (remainder > 0 && shares[id] < demand)
            {
                shares[id]++;
                remainder--;
            }
        }

        Assert.Equal(0, remainder);
        return shares;
    }

    private static Dictionary<long, int> ToDictionary(PowerAllocation allocation) =>
        allocation.Entries.ToDictionary(entry => entry.Consumer.Value, entry => entry.Allocation.Value);
}
