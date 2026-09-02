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

    /// <summary>Confirms immutable operations do not alter their receiver.</summary>
    [Fact]
    public void OperationsPreserveReceiverState()
    {
        var initial = SimulationScheduler.Create();
        (SimulationScheduler scheduled, ScheduledWork work) = initial.Schedule(
            new SimulationTime(100),
            ScheduledWorkKind.TravelArrival
        );

        (SimulationScheduler remaining, IReadOnlyList<ScheduledWork> due) = scheduled.DequeueDue(
            new SimulationTime(100)
        );

        Assert.Empty(initial.OutstandingWork);
        Assert.Equal(1, initial.NextWorkId);
        Assert.Equal(0, initial.NextSequence);
        Assert.Equal(new[] { work }, scheduled.OutstandingWork);
        Assert.Equal(2, scheduled.NextWorkId);
        Assert.Equal(1, scheduled.NextSequence);
        Assert.Empty(remaining.OutstandingWork);
        Assert.Equal(new[] { work }, due);
    }

    /// <summary>Confirms a due batch is a snapshot of work outstanding when dequeue begins.</summary>
    [Fact]
    public void DequeueDueReturnsSameBoundarySnapshot()
    {
        (SimulationScheduler scheduled, ScheduledWork first) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(100), ScheduledWorkKind.TravelArrival);
        (SimulationScheduler remaining, IReadOnlyList<ScheduledWork> firstBatch) = scheduled.DequeueDue(
            new SimulationTime(100)
        );
        (SimulationScheduler rescheduled, ScheduledWork second) = remaining.Schedule(
            new SimulationTime(100),
            ScheduledWorkKind.SensorRepairCompletion
        );

        (SimulationScheduler final, IReadOnlyList<ScheduledWork> secondBatch) = rescheduled.DequeueDue(
            new SimulationTime(100)
        );

        Assert.Equal(new[] { first }, firstBatch);
        Assert.Equal(new[] { second }, secondBatch);
        Assert.Empty(final.OutstandingWork);
    }

    /// <summary>Confirms restoration sorts work and continues persisted counters.</summary>
    [Fact]
    public void RestoreSortsOutstandingWorkAndContinuesCounters()
    {
        ScheduledWork later = new(new ScheduledWorkId(7), new SimulationTime(900), 12, ScheduledWorkKind.TravelArrival);
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

    /// <summary>Confirms restoration uses persisted sequence rather than input order at one due time.</summary>
    [Fact]
    public void RestoreOrdersSameTimeWorkByPersistedSequence()
    {
        ScheduledWork first = Work(1, 0, 500);
        ScheduledWork second = Work(2, 1, 500);

        var restored = SimulationScheduler.Restore(3, 2, [second, first]);

        Assert.Equal(new[] { first, second }, restored.OutstandingWork);
    }

    /// <summary>Confirms live post-dequeue state restores and continues deterministic allocation.</summary>
    [Fact]
    public void PostDequeueStateRestoresAndContinues()
    {
        (SimulationScheduler afterFirst, _) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(100), ScheduledWorkKind.TravelArrival);
        (SimulationScheduler live, ScheduledWork future) = afterFirst.Schedule(
            new SimulationTime(300),
            ScheduledWorkKind.SensorRepairCompletion
        );
        (SimulationScheduler postDequeue, _) = live.DequeueDue(new SimulationTime(100));

        var restored = SimulationScheduler.Restore(
            postDequeue.NextWorkId,
            postDequeue.NextSequence,
            postDequeue.OutstandingWork
        );
        (SimulationScheduler continued, ScheduledWork next) = restored.Schedule(
            new SimulationTime(200),
            ScheduledWorkKind.TravelArrival
        );

        Assert.Equal(new[] { next, future }, continued.OutstandingWork);
        Assert.Equal(new ScheduledWorkId(3), next.Id);
        Assert.Equal(2, next.Sequence);
        Assert.Equal(4, continued.NextWorkId);
        Assert.Equal(3, continued.NextSequence);
    }

    /// <summary>Confirms restoration rejects duplicate identities and sequences.</summary>
    [Fact]
    public void RestoreRejectsDuplicateWorkIdentityOrSequence()
    {
        ScheduledWork first = Work(1, 0);

        ArgumentException duplicateIdentity = Assert.Throws<ArgumentException>(() =>
            SimulationScheduler.Restore(3, 2, [first, Work(1, 1)])
        );
        ArgumentException duplicateSequence = Assert.Throws<ArgumentException>(() =>
            SimulationScheduler.Restore(3, 2, [first, Work(2, 0, 200)])
        );

        Assert.Equal("outstandingWork", duplicateIdentity.ParamName);
        Assert.Equal("outstandingWork", duplicateSequence.ParamName);
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
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationScheduler.Restore(0, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationScheduler.Restore(1, -1, []));
    }

    /// <summary>Confirms invalid work data cannot cross public boundaries.</summary>
    [Fact]
    public void ScheduledWorkRejectsInvalidSequenceOrKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScheduledWork(new ScheduledWorkId(1), new SimulationTime(0), -1, ScheduledWorkKind.TravelArrival)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScheduledWork(new ScheduledWorkId(1), new SimulationTime(0), 0, (ScheduledWorkKind)0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScheduledWork(new ScheduledWorkId(1), new SimulationTime(0), 0, (ScheduledWorkKind)999)
        );
        ArgumentException invalidRestoration = Assert.Throws<ArgumentException>(() =>
            SimulationScheduler.Restore(1, 0, [default])
        );

        Assert.Equal("outstandingWork", invalidRestoration.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulationScheduler.Create().Schedule(new SimulationTime(0), (ScheduledWorkKind)0)
        );
    }

    /// <summary>Confirms null outstanding work is rejected at the public restoration boundary.</summary>
    [Fact]
    public void RestoreRejectsNullOutstandingWork()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            SimulationScheduler.Restore(1, 0, null!)
        );

        Assert.Equal("outstandingWork", exception.ParamName);
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

    private static ScheduledWork Work(long id, long sequence, long dueTime = 100) =>
        new(new ScheduledWorkId(id), new SimulationTime(dueTime), sequence, ScheduledWorkKind.TravelArrival);
}
