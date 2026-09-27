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
    private const string Seed = "123456789012";

    /// <summary>Intentional invariant failure retains a minimized case and a replayable seed.</summary>
    [Fact]
    public void GeneratedFailureReportsCounterexampleAndReplays()
    {
        CsCheckException failure = Assert.Throws<CsCheckException>(() =>
            Gen.Int[0, 100]
                .Sample(
                    value => value < 5,
                    seed: Seed,
                    iter: Iterations,
                    time: -1,
                    threads: 1,
                    print: value => "counterexample=" + value
                )
        );
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
                    value => value < 5,
                    seed: replaySeed.Groups["seed"].Value,
                    iter: 1,
                    time: -1,
                    threads: 1,
                    print: value => "counterexample=" + value
                )
        );
        Assert.Contains(counterexample.Value, replay.Message, StringComparison.Ordinal);
    }

    /// <summary>Scheduling, cancellation, dequeue, and restored continuation agree with an independent list model.</summary>
    [Fact]
    public void SchedulerOrderingAndContinuationMatchListModel()
    {
        Gen.Select(Gen.Int[0, 20].Array[0, 32], Gen.Int[0, 33], Gen.Int[0, 20], Gen.Int[0, 20])
            .Sample(
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
                },
                seed: Seed,
                iter: Iterations,
                time: -1,
                threads: 1
            );
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
        Gen.Select(
                Gen.Int[1, PowerUnits.MaximumValue].Array[0, InstalledSystemCollection.MaximumCount - 1],
                Gen.Int[1, PowerUnits.MaximumValue],
                Gen.Int[0, 1000]
            )
            .Sample(
                (demands, output, condition) =>
                {
                    InstalledSystem[] installations =
                    [
                        TestSystems.Install(
                            1,
                            TestSystems.Generator("generator", output),
                            condition: condition / 1000d
                        ),
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
                    Assert.Equal(allocation, reordered.BalancedAllocation());
                    original.WithAllocation(allocation).Validate();
                },
                seed: Seed,
                iter: Iterations,
                time: -1,
                threads: 1
            );
    }

    /// <summary>Shield damage conserves damage and stronger power cannot increase penetration.</summary>
    [Fact]
    public void ShieldDamageConservesDamageAndMonotonicallyProtects()
    {
        Gen.Select(Gen.Int[0, 10000], Gen.Int[0, 10000], Gen.Int[0, 10000], Gen.Int[0, 10000])
            .Sample(
                (damageUnits, conditionUnits, powerUnits, increaseUnits) =>
                {
                    double damage = damageUnits / 10000d;
                    var condition = new SystemCondition(conditionUnits / 10000d);
                    double power = powerUnits / 10000d;
                    ShieldDamageResult result = ShieldDamage.Resolve(damage, condition, power);
                    ShieldDamageResult stronger = ShieldDamage.Resolve(
                        damage,
                        condition,
                        Math.Min(1, power + (increaseUnits / 10000d))
                    );
                    Assert.Equal(damage, result.AbsorbedDamage + result.PenetratingDamage, 12);
                    Assert.Equal(condition.Value, result.ShieldCondition.Value + result.AbsorbedDamage, 12);
                    Assert.InRange(result.AbsorbedDamage, 0, damage);
                    Assert.InRange(result.PenetratingDamage, 0, damage);
                    Assert.InRange(result.ShieldCondition.Value, 0, condition.Value);
                    Assert.True(stronger.PenetratingDamage <= result.PenetratingDamage);
                    Assert.True(stronger.AbsorbedDamage >= result.AbsorbedDamage);
                },
                seed: Seed,
                iter: Iterations,
                time: -1,
                threads: 1
            );
    }
}
