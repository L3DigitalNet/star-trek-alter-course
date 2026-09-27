using System.Text.RegularExpressions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tests.Support;
using CsCheck;

namespace AlterCourse.Core.Tests;

/// <summary>Explores bounded simulation invariants with reproducible, shrinking CsCheck inputs.</summary>
/// <remarks>Requires the pinned test-only CsCheck package. Its generators never supply gameplay randomness.</remarks>
public sealed class GeneratedSimulationInvariantTests
{
    private const int Iterations = 256;

    /// <summary>Intentional invariant failure retains a minimized case and a replayable seed.</summary>
    [Fact]
    public void GeneratedFailureReportsCounterexampleAndReplays()
    {
        CsCheckException failure = Assert.Throws<CsCheckException>(() =>
            SampleCases(
                Gen.Int[0, 100],
                value => Assert.True(value < 5, "generated invariant"),
                value => "counterexample=" + value
            )
        );
        Assert.Contains("generated invariant", failure.Message, StringComparison.Ordinal);
        Match replaySeed = Regex.Match(failure.Message, "Set seed: \"(?<seed>[^\"]+)\"", RegexOptions.NonBacktracking);
        Assert.True(replaySeed.Success, failure.Message);
        Match counterexample = Regex.Match(
            failure.Message,
            "counterexample=(?<value>[0-9]+)",
            RegexOptions.NonBacktracking
        );
        Assert.True(counterexample.Success, failure.Message);
        CsCheckException replay = Assert.Throws<CsCheckException>(() =>
            Gen.Int[0, 100]
                .Sample(
                    value => Assert.True(value < 5, "generated invariant"),
                    seed: replaySeed.Groups["seed"].Value,
                    iter: 1,
                    time: -1,
                    threads: 1,
                    print: value => "counterexample=" + value
                )
        );
        Assert.Contains(counterexample.Value, replay.Message, StringComparison.Ordinal);
        Assert.Contains("generated invariant", replay.Message, StringComparison.Ordinal);
    }

    /// <summary>Scheduling, cancellation, dequeue, and restored continuation agree with an independent list model.</summary>
    [Fact]
    public void SchedulerOrderingAndContinuationMatchListModel()
    {
        SampleCases(
            Gen.Select(Gen.Int[0, 20].Array[0, 32], Gen.Int[0, 33], Gen.Int[0, 20], Gen.Int[0, 20]),
            (times, cancel, cutoff, continuedTime) =>
            {
                var target = new ShipInstanceId(1);
                SimulationScheduler scheduler = Schedule(times, target);

                // The model uses insertion indices, never production sort/comparison helpers or emitted IDs.
                var model = times
                    .Select((time, index) => (Time: time, Id: (long)index + 1, Sequence: (long)index))
                    .ToList();
                long cancelledId = cancel + 1L;
                (SimulationScheduler cancelled, bool removed) = scheduler.Cancel(new ScheduledWorkId(cancelledId));
                Assert.Equal(model.RemoveAll(item => item.Id == cancelledId) != 0, removed);
                Assert.Equal(times.Length, scheduler.OutstandingWork.Length);

                var restored = SimulationScheduler.Restore(
                    cancelled.NextWorkId,
                    cancelled.NextSequence,
                    cancelled.OutstandingWork.Reverse()
                );
                (SimulationScheduler live, IReadOnlyList<ScheduledWork> liveDue) = cancelled.DequeueDue(
                    new SimulationTime(cutoff)
                );
                (SimulationScheduler resumed, IReadOnlyList<ScheduledWork> resumedDue) = restored.DequeueDue(
                    new SimulationTime(cutoff)
                );
                Assert.Equal(liveDue, resumedDue);
                AssertModelOrder(model.Where(item => item.Time <= cutoff), resumedDue);
                model.RemoveAll(item => item.Time <= cutoff);
                (SimulationScheduler continuedLive, ScheduledWork liveNext) = live.Schedule(
                    new SimulationTime(continuedTime),
                    target,
                    ScheduledWorkKind.OrderWake
                );
                (SimulationScheduler continuedRestored, ScheduledWork resumedNext) = resumed.Schedule(
                    new SimulationTime(continuedTime),
                    target,
                    ScheduledWorkKind.OrderWake
                );
                Assert.Equal(liveNext, resumedNext);
                Assert.Equal(times.Length + 1L, resumedNext.Id.Value);
                Assert.Equal(times.Length, resumedNext.Sequence);
                model.Add((continuedTime, times.Length + 1L, times.Length));
                AssertModelOrder(model, continuedRestored.OutstandingWork);
                Assert.Equal(continuedLive.OutstandingWork.ToArray(), continuedRestored.OutstandingWork.ToArray());
                Assert.Equal(times.Length + 2L, continuedRestored.NextWorkId);
                Assert.Equal(times.Length + 1L, continuedRestored.NextSequence);
            }
        );
    }

    /// <summary>Every passing generated input replays independently of the process RNG state.</summary>
    [Fact]
    public void CompleteGeneratedSequenceReplays()
    {
        int[] first = CaptureSequence();
        int[] second = CaptureSequence();
        Assert.Equal(Iterations, first.Length);
        Assert.Equal(first, second);
        Assert.True(first.Distinct().Count() > Iterations / 2);
    }

    private static int[] CaptureSequence()
    {
        var values = new List<int>();
        foreach (string seed in CaseSeeds())
        {
            Gen.Int[0, 1000000]
                .Sample(
                    value =>
                    {
                        values.Add(value);
                    },
                    seed: seed,
                    iter: 1,
                    time: -1,
                    threads: 1
                );
        }
        return values.ToArray();
    }

    private static void SampleCases<T1, T2, T3>(Gen<(T1, T2, T3)> gen, Action<T1, T2, T3> assertion) =>
        SampleCases(gen, value => assertion(value.Item1, value.Item2, value.Item3));

    private static void SampleCases<T1, T2, T3, T4>(Gen<(T1, T2, T3, T4)> gen, Action<T1, T2, T3, T4> assertion) =>
        SampleCases(gen, value => assertion(value.Item1, value.Item2, value.Item3, value.Item4));

    private static void SampleCases<T>(Gen<T> gen, Action<T> assertion, Func<T, string>? print = null)
    {
        foreach (string seed in CaseSeeds())
        {
            try
            {
                gen.Sample(assertion, seed: seed, iter: 1, time: -1, threads: 1, print: print);
            }
            catch (CsCheckException)
            {
                // Only diagnostic minimization uses the ambient test RNG; the failing first input is seeded again.
                gen.Sample(assertion, seed: seed, iter: Iterations, time: -1, threads: 1, print: print);
                throw;
            }
        }
    }

    private static IEnumerable<string> CaseSeeds()
    {
        // A private explicit state advances across cases without reading or changing PCG.ThreadPCG.
        var stream = new PCG(7, 123456789012UL);
        for (int index = 0; index < Iterations; index++)
        {
            yield return stream.ToString();
            _ = stream.Next64();
        }
    }

    private static SimulationScheduler Schedule(int[] times, ShipInstanceId target)
    {
        var scheduler = SimulationScheduler.Create();
        foreach (int time in times)
        {
            (scheduler, _) = scheduler.Schedule(new SimulationTime(time), target, ScheduledWorkKind.OrderWake);
        }
        return scheduler;
    }

    private static void AssertModelOrder(
        IEnumerable<(int Time, long Id, long Sequence)> model,
        IEnumerable<ScheduledWork> actual
    ) =>
        Assert.Equal(
            model.OrderBy(item => item.Time).ThenBy(item => item.Sequence).Select(item => item.Id),
            actual.Select(item => item.Id.Value)
        );

    /// <summary>Balanced power conserves available budget, obeys demands, and ignores installation enumeration order.</summary>
    [Fact]
    public void BalancedPowerConservesBudgetAcrossGeneratedLoadouts()
    {
        SampleCases(
            Gen.Select(
                Gen.Int[1, PowerUnits.MaximumValue].Array[0, InstalledSystemCollection.MaximumCount - 1],
                Gen.Int[1, PowerUnits.MaximumValue],
                Gen.Int[0, 1000]
            ),
            (demands, output, condition) =>
            {
                InstalledSystem[] installations =
                [
                    TestSystems.Install(1, TestSystems.Generator("generator", output), condition: condition / 1000d),
                    .. demands.Select(
                        (demand, index) =>
                            TestSystems.Install(
                                17 + (index * 41L),
                                TestSystems.Consumer("consumer" + index, index % 3, demand)
                            )
                    ),
                ];
                ShipEngineeringState original = TestSystems.Engineering(installations);
                ShipEngineeringState reordered = TestSystems.Engineering([.. installations.Reverse()]);
                PowerAllocation allocation = original.BalancedAllocation();
                Assert.Equal(
                    Math.Min(original.AvailablePower.Value, demands.Sum(demand => (long)demand)),
                    allocation.Total
                );
                Assert.Equal(demands.Length, allocation.Entries.Count);
                for (int index = 0; index < demands.Length; index++)
                {
                    Assert.InRange(TestSystems.Share(allocation, 17 + (index * 41L)), 0, demands[index]);
                }
                AssertBalancedOracle(demands, original.AvailablePower.Value, allocation);
                Assert.Equal(allocation, reordered.BalancedAllocation());
                original.WithAllocation(allocation).Validate();
            }
        );
    }

    /// <summary>Shield damage conserves damage and stronger power cannot increase penetration.</summary>
    [Fact]
    public void ShieldDamageConservesDamageAndMonotonicallyProtects()
    {
        SampleCases(
            Gen.Select(Gen.Int[0, 10000], Gen.Int[0, 10000], Gen.Int[0, 10000], Gen.Int[0, 10000]),
            (damageUnits, conditionUnits, powerUnits, increaseUnits) =>
            {
                double damage = damageUnits / 10000d;
                var condition = new SystemCondition(conditionUnits / 10000d);
                double power = powerUnits / 10000d;
                ShieldDamageResult result = ShieldDamage.Resolve(damage, condition, new PowerSatisfactionRatio(power));
                ShieldDamageResult stronger = ShieldDamage.Resolve(
                    damage,
                    condition,
                    new PowerSatisfactionRatio(Math.Min(1, power + (increaseUnits / 10000d)))
                );
                AssertShieldOracle(damageUnits, conditionUnits, powerUnits, result);
                Assert.Equal(damage, result.AbsorbedDamage + result.PenetratingDamage, 12);
                Assert.Equal(condition.Value, result.ShieldCondition.Value + result.AbsorbedDamage, 12);
                Assert.InRange(result.AbsorbedDamage, 0, damage);
                Assert.InRange(result.PenetratingDamage, 0, damage);
                Assert.InRange(result.ShieldCondition.Value, 0, condition.Value);
                Assert.True(stronger.PenetratingDamage <= result.PenetratingDamage);
                Assert.True(stronger.AbsorbedDamage >= result.AbsorbedDamage);
            }
        );
    }

    /// <summary>Exact oracles reject greedy allocation and powerless shield mutants despite conservation.</summary>
    [Fact]
    public void MathematicalOraclesRejectConservationPreservingMutants()
    {
        int[] demands = [40, 60];
        var greedy = new PowerAllocation([
            new PowerAllocationEntry(new InstalledSystemId(17), new PowerUnits(40)),
            new PowerAllocationEntry(new InstalledSystemId(58), new PowerUnits(10)),
        ]);
        Assert.Equal(50, greedy.Total);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertBalancedOracle(demands, 50, greedy));
        var balanced = new PowerAllocation([
            new PowerAllocationEntry(new InstalledSystemId(17), new PowerUnits(20)),
            new PowerAllocationEntry(new InstalledSystemId(58), new PowerUnits(30)),
        ]);
        AssertBalancedOracle(demands, 50, balanced);
        var noAbsorption = new ShieldDamageResult(new SystemCondition(0.8), 0, 0.5);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertShieldOracle(5000, 8000, 5000, noAbsorption));
        AssertShieldOracle(
            5000,
            8000,
            5000,
            ShieldDamage.Resolve(0.5, new SystemCondition(0.8), new PowerSatisfactionRatio(0.5))
        );
    }

    private static void AssertBalancedOracle(int[] demands, int available, PowerAllocation actual)
    {
        if (demands.Length == 0)
        {
            Assert.Empty(actual.Entries);
            return;
        }
        decimal total = demands.Sum(value => (decimal)value);
        decimal budget = Math.Min(available, total);
        int[] floors = demands.Select(demand => (int)decimal.Floor(budget * demand / total)).ToArray();
        int remainder = (int)budget - floors.Sum();
        int[] eligible = Enumerable
            .Range(0, demands.Length)
            .OrderBy(index => index % 3)
            .ThenBy(index => 17 + index * 41L)
            .Where(index => floors[index] < demands[index])
            .ToArray();
        // Exact decimal quotas and remainder ranks form an oracle independent of the allocator's mutable distribution loop.
        for (int index = 0; index < demands.Length; index++)
        {
            int rank = Array.IndexOf(eligible, index);
            int expected = floors[index] + (rank >= 0 && rank < remainder ? 1 : 0);
            Assert.Equal(expected, TestSystems.Share(actual, 17 + index * 41L));
        }
    }

    private static void AssertShieldOracle(
        int damageUnits,
        int conditionUnits,
        int powerUnits,
        ShieldDamageResult actual
    )
    {
        decimal damage = damageUnits / 10000m;
        decimal condition = conditionUnits / 10000m;
        decimal capacity = (decimal)conditionUnits * powerUnits / 100000000m;
        decimal absorption = Math.Min(damage, capacity);
        Assert.Equal((double)absorption, actual.AbsorbedDamage, 12);
        Assert.Equal((double)(damage - absorption), actual.PenetratingDamage, 12);
        Assert.Equal((double)(condition - absorption), actual.ShieldCondition.Value, 12);
    }
}
