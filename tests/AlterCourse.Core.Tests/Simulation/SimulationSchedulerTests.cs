using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Simulation;

/// <summary>Verifies deterministic scheduled-work ordering and restoration.</summary>
public sealed class SimulationSchedulerTests
{
    /// <summary>Confirms same-time work is ordered by its persisted sequence.</summary>
    [Fact]
    public void SameTimeWorkUsesStableSequenceOrder()
    {
        var scheduler = SimulationScheduler.Create();
        (SimulationScheduler afterFirst, ScheduledWork first) = scheduler.Schedule(
            new SimulationTime(500),
            ScheduledWorkKind.TravelArrival
        );
        (SimulationScheduler afterSecond, ScheduledWork second) = afterFirst.Schedule(
            new SimulationTime(500),
            ScheduledWorkKind.SensorRepairCompletion
        );

        (SimulationScheduler remaining, IReadOnlyList<ScheduledWork> due) = afterSecond.DequeueDue(
            new SimulationTime(500)
        );

        Assert.Equal(new[] { first, second }, due);
        Assert.Empty(remaining.OutstandingWork);
        Assert.Equal(3, remaining.NextWorkId);
        Assert.Equal(2, remaining.NextSequence);
    }

    /// <summary>Confirms dequeue includes the boundary and preserves future work.</summary>
    [Fact]
    public void DequeueDueIncludesBoundaryAndPreservesFutureWork()
    {
        var scheduler = SimulationScheduler.Create();
        (SimulationScheduler afterEarlier, ScheduledWork earlier) = scheduler.Schedule(
            new SimulationTime(499),
            ScheduledWorkKind.TravelArrival
        );
        (SimulationScheduler afterBoundary, ScheduledWork boundary) = afterEarlier.Schedule(
            new SimulationTime(500),
            ScheduledWorkKind.SensorRepairCompletion
        );
        (SimulationScheduler afterFuture, ScheduledWork future) = afterBoundary.Schedule(
            new SimulationTime(501),
            ScheduledWorkKind.TravelArrival
        );

        (SimulationScheduler remaining, IReadOnlyList<ScheduledWork> due) = afterFuture.DequeueDue(
            new SimulationTime(500)
        );

        Assert.Equal(new[] { earlier, boundary }, due);
        Assert.Equal(new[] { future }, remaining.OutstandingWork);
    }

    /// <summary>Confirms restoration sorts work and continues persisted counters.</summary>
    [Fact]
    public void RestoreSortsOutstandingWorkAndContinuesCounters()
    {
        ScheduledWork later = new(
            new ScheduledWorkId(7),
            new SimulationTime(900),
            12,
            ScheduledWorkKind.TravelArrival
        );
        ScheduledWork earlier = new(
            new ScheduledWorkId(4),
            new SimulationTime(200),
            8,
            ScheduledWorkKind.SensorRepairCompletion
        );

        var restored = SimulationScheduler.Restore(8, 13, [later, earlier]);
        (SimulationScheduler next, ScheduledWork scheduled) = restored.Schedule(
            new SimulationTime(300),
            ScheduledWorkKind.TravelArrival
        );

        Assert.Equal(new[] { earlier, later }, restored.OutstandingWork);
        Assert.Equal(new ScheduledWorkId(8), scheduled.Id);
        Assert.Equal(13, scheduled.Sequence);
        Assert.Equal(9, next.NextWorkId);
        Assert.Equal(14, next.NextSequence);
    }

    /// <summary>Confirms restoration rejects duplicate identities and sequences.</summary>
    [Fact]
    public void RestoreRejectsDuplicateWorkIdentityOrSequence()
    {
        ScheduledWork first = Work(1, 0);

        Assert.Throws<ArgumentException>(() =>
            SimulationScheduler.Restore(3, 2, [first, Work(1, 1)])
        );
        Assert.Throws<ArgumentException>(() =>
            SimulationScheduler.Restore(3, 2, [first, Work(2, 0)])
        );
    }

    /// <summary>Confirms restoration counters are valid next values, not reused values.</summary>
    [Fact]
    public void RestoreRejectsCountersThatDoNotExceedOutstandingWork()
    {
        ScheduledWork work = Work(5, 9);

        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationScheduler.Restore(5, 10, [work]));
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationScheduler.Restore(6, 9, [work]));
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationScheduler.Restore(0, 10, [work]));
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationScheduler.Restore(6, -1, [work]));
    }

    /// <summary>Confirms invalid work data cannot cross public boundaries.</summary>
    [Fact]
    public void ScheduledWorkRejectsInvalidSequenceOrKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScheduledWork(
                new ScheduledWorkId(1),
                new SimulationTime(0),
                -1,
                ScheduledWorkKind.TravelArrival
            )
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScheduledWork(
                new ScheduledWorkId(1),
                new SimulationTime(0),
                0,
                (ScheduledWorkKind)999
            )
        );
        Assert.Throws<ArgumentException>(() => SimulationScheduler.Restore(1, 0, [default]));
    }

    /// <summary>Confirms scheduling rejects exhausted persisted counters.</summary>
    [Fact]
    public void ScheduleRejectsCounterOverflow()
    {
        var exhaustedId = SimulationScheduler.Restore(long.MaxValue, 0, []);
        var exhaustedSequence = SimulationScheduler.Restore(1, long.MaxValue, []);

        Assert.Throws<OverflowException>(() =>
            exhaustedId.Schedule(new SimulationTime(0), ScheduledWorkKind.TravelArrival)
        );
        Assert.Throws<OverflowException>(() =>
            exhaustedSequence.Schedule(new SimulationTime(0), ScheduledWorkKind.TravelArrival)
        );
    }

    private static ScheduledWork Work(long id, long sequence) =>
        new(
            new ScheduledWorkId(id),
            new SimulationTime(100),
            sequence,
            ScheduledWorkKind.TravelArrival
        );
}
