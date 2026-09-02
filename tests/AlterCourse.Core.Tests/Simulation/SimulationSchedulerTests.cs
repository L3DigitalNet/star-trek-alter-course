using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Simulation;

/// <summary>Verifies deterministic scheduled-work ordering and restoration.</summary>
public sealed class SimulationSchedulerTests
{
    /// <summary>Confirms same-time, same-kind work for different ships uses persisted sequence order.</summary>
    [Fact]
    public void SameTimeWorkUsesStableSequenceOrder()
    {
        var scheduler = SimulationScheduler.Create();
        (SimulationScheduler afterFirst, ScheduledWork first) = scheduler.Schedule(
            new SimulationTime(500),
            Target(1),
            ScheduledWorkKind.TravelArrival
        );
        (SimulationScheduler afterSecond, ScheduledWork second) = afterFirst.Schedule(
            new SimulationTime(500),
            Target(2),
            ScheduledWorkKind.TravelArrival
        );

        (SimulationScheduler remaining, IReadOnlyList<ScheduledWork> due) = afterSecond.DequeueDue(
            new SimulationTime(500)
        );

        Assert.Equal(new[] { first, second }, due);
        Assert.Equal(Target(1), first.TargetShipId);
        Assert.Equal(Target(2), second.TargetShipId);
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
            Target(),
            ScheduledWorkKind.TravelArrival
        );
        (SimulationScheduler afterBoundary, ScheduledWork boundary) = afterEarlier.Schedule(
            new SimulationTime(500),
            Target(),
            ScheduledWorkKind.SensorRepairCompletion
        );
        (SimulationScheduler afterFuture, ScheduledWork future) = afterBoundary.Schedule(
            new SimulationTime(501),
            Target(),
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
            Target(),
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

    /// <summary>Confirms exact cancellation removes work at any ordered position without reallocating counters.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CancelRemovesExactWorkAtEveryOrderedPosition(int cancellationIndex)
    {
        (SimulationScheduler afterFirst, ScheduledWork first) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(100), Target(1), ScheduledWorkKind.TravelArrival);
        (SimulationScheduler afterSecond, ScheduledWork second) = afterFirst.Schedule(
            new SimulationTime(200),
            Target(2),
            ScheduledWorkKind.SensorRepairCompletion
        );
        (SimulationScheduler scheduled, ScheduledWork third) = afterSecond.Schedule(
            new SimulationTime(300),
            Target(3),
            ScheduledWorkKind.OrderWake
        );
        ScheduledWork[] original = [first, second, third];

        (SimulationScheduler following, bool removed) = scheduled.Cancel(original[cancellationIndex].Id);

        Assert.True(removed);
        Assert.Equal(original.Where((_, index) => index != cancellationIndex).ToArray(), following.OutstandingWork);
        Assert.Equal(4, following.NextWorkId);
        Assert.Equal(3, following.NextSequence);
        Assert.Equal(original, scheduled.OutstandingWork);
        Assert.Equal(4, scheduled.NextWorkId);
        Assert.Equal(3, scheduled.NextSequence);
    }

    /// <summary>Confirms cancellation correlates only by identity among otherwise identical work.</summary>
    [Fact]
    public void CancelPreservesSameTimeKindAndTargetWork()
    {
        (SimulationScheduler afterFirst, ScheduledWork first) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(500), Target(), ScheduledWorkKind.OrderWake);
        (SimulationScheduler afterSecond, ScheduledWork cancelled) = afterFirst.Schedule(
            new SimulationTime(500),
            Target(),
            ScheduledWorkKind.OrderWake
        );
        (SimulationScheduler scheduled, ScheduledWork third) = afterSecond.Schedule(
            new SimulationTime(500),
            Target(),
            ScheduledWorkKind.OrderWake
        );

        (SimulationScheduler following, bool removed) = scheduled.Cancel(cancelled.Id);

        Assert.True(removed);
        Assert.Equal(new[] { first, third }, following.OutstandingWork);
        Assert.Equal(new[] { first, cancelled, third }, scheduled.OutstandingWork);
    }

    /// <summary>Confirms an initialized identity never allocated by this scheduler is an ordinary negative result.</summary>
    [Fact]
    public void CancelMissingIdentityReturnsUnchangedScheduler()
    {
        (SimulationScheduler scheduled, ScheduledWork work) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(100), Target(), ScheduledWorkKind.OrderWake);

        (SimulationScheduler following, bool removed) = scheduled.Cancel(new ScheduledWorkId(999));

        Assert.False(removed);
        Assert.Equal(new[] { work }, following.OutstandingWork);
        Assert.Equal(scheduled.NextWorkId, following.NextWorkId);
        Assert.Equal(scheduled.NextSequence, following.NextSequence);
    }

    /// <summary>Confirms an identity for work already dequeued is an ordinary negative result.</summary>
    [Fact]
    public void CancelStaleIdentityReturnsUnchangedScheduler()
    {
        (SimulationScheduler scheduled, ScheduledWork work) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(100), Target(), ScheduledWorkKind.OrderWake);
        (SimulationScheduler dequeued, IReadOnlyList<ScheduledWork> due) = scheduled.DequeueDue(
            new SimulationTime(100)
        );

        (SimulationScheduler following, bool removed) = dequeued.Cancel(work.Id);

        Assert.Equal(new[] { work }, due);
        Assert.False(removed);
        Assert.Empty(following.OutstandingWork);
        Assert.Equal(dequeued.NextWorkId, following.NextWorkId);
        Assert.Equal(dequeued.NextSequence, following.NextSequence);
    }

    /// <summary>Confirms cancellation rejects an uninitialized correlation identity.</summary>
    [Fact]
    public void CancelRejectsUninitializedIdentity()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            SimulationScheduler.Create().Cancel(default)
        );

        Assert.Equal("id", exception.ParamName);
    }

    /// <summary>Confirms a due batch is a snapshot of work outstanding when dequeue begins.</summary>
    [Fact]
    public void DequeueDueReturnsSameBoundarySnapshot()
    {
        (SimulationScheduler scheduled, ScheduledWork first) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(100), Target(), ScheduledWorkKind.TravelArrival);
        (SimulationScheduler remaining, IReadOnlyList<ScheduledWork> firstBatch) = scheduled.DequeueDue(
            new SimulationTime(100)
        );
        (SimulationScheduler rescheduled, ScheduledWork second) = remaining.Schedule(
            new SimulationTime(100),
            Target(),
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
        ScheduledWork later = new(
            new ScheduledWorkId(7),
            new SimulationTime(900),
            12,
            Target(2),
            ScheduledWorkKind.TravelArrival
        );
        ScheduledWork earlier = new(
            new ScheduledWorkId(4),
            new SimulationTime(200),
            8,
            Target(1),
            ScheduledWorkKind.SensorRepairCompletion
        );

        var restored = SimulationScheduler.Restore(8, 13, [later, earlier]);
        (SimulationScheduler next, ScheduledWork scheduled) = restored.Schedule(
            new SimulationTime(300),
            Target(3),
            ScheduledWorkKind.TravelArrival
        );

        Assert.Equal(new[] { earlier, later }, restored.OutstandingWork);
        Assert.Equal(new ScheduledWorkId(8), scheduled.Id);
        Assert.Equal(13, scheduled.Sequence);
        Assert.Equal(Target(3), scheduled.TargetShipId);
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
            .Schedule(new SimulationTime(100), Target(), ScheduledWorkKind.TravelArrival);
        (SimulationScheduler live, ScheduledWork future) = afterFirst.Schedule(
            new SimulationTime(300),
            Target(),
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
            Target(),
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
    public void ScheduledWorkRejectsInvalidSequenceTargetOrKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScheduledWork(
                new ScheduledWorkId(1),
                new SimulationTime(0),
                -1,
                Target(),
                ScheduledWorkKind.TravelArrival
            )
        );
        Assert.Throws<ArgumentException>(() =>
            new ScheduledWork(
                new ScheduledWorkId(1),
                new SimulationTime(0),
                0,
                default,
                ScheduledWorkKind.TravelArrival
            )
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScheduledWork(new ScheduledWorkId(1), new SimulationTime(0), 0, Target(), (ScheduledWorkKind)0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScheduledWork(new ScheduledWorkId(1), new SimulationTime(0), 0, Target(), (ScheduledWorkKind)999)
        );
        ArgumentException invalidRestoration = Assert.Throws<ArgumentException>(() =>
            SimulationScheduler.Restore(1, 0, [default])
        );

        Assert.Equal("outstandingWork", invalidRestoration.ParamName);
        Assert.Throws<ArgumentException>(() =>
            SimulationScheduler.Create().Schedule(new SimulationTime(0), default, ScheduledWorkKind.TravelArrival)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulationScheduler.Create().Schedule(new SimulationTime(0), Target(), (ScheduledWorkKind)0)
        );
    }

    /// <summary>Confirms order wake-up work crosses construction, scheduling, and restoration boundaries.</summary>
    [Fact]
    public void OrderWakeIsSupportedScheduledWork()
    {
        ScheduledWork persisted = new(
            new ScheduledWorkId(4),
            new SimulationTime(250),
            8,
            Target(),
            ScheduledWorkKind.OrderWake
        );

        var restored = SimulationScheduler.Restore(5, 9, [persisted]);
        (SimulationScheduler following, ScheduledWork scheduled) = restored.Schedule(
            new SimulationTime(500),
            Target(2),
            ScheduledWorkKind.OrderWake
        );

        Assert.Equal(ScheduledWorkKind.OrderWake, restored.OutstandingWork.Single().Kind);
        Assert.Equal(ScheduledWorkKind.OrderWake, scheduled.Kind);
        Assert.Equal(new[] { persisted, scheduled }, following.OutstandingWork);
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

    /// <summary>Confirms restoration stops hostile enumeration and accepts the scheduler-owned maximum.</summary>
    [Fact]
    public void RestoreBoundsOutstandingWorkMaterialization()
    {
        ScheduledWork[] maximum =
        [
            .. Enumerable.Range(1, SimulationScheduler.MaximumOutstandingWork).Select(index => Work(index, index - 1)),
        ];

        var restored = SimulationScheduler.Restore(
            SimulationScheduler.MaximumOutstandingWork + 1,
            SimulationScheduler.MaximumOutstandingWork,
            maximum
        );

        Assert.Equal(SimulationScheduler.MaximumOutstandingWork, restored.OutstandingWork.Length);
        Assert.Throws<ArgumentException>(() =>
            SimulationScheduler.Restore(2, 1, OverflowAfter(Work(1, 0), SimulationScheduler.MaximumOutstandingWork + 1))
        );
    }

    /// <summary>Confirms persisted counter bounds reject scheduling atomically at exhaustion.</summary>
    [Fact]
    public void RejectsCountersOutsidePersistedRangeAndPreservesAtomicExhaustion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationScheduler.Restore(long.MaxValue - 1, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationScheduler.Restore(1, long.MaxValue - 1, []));
        var exhaustedId = SimulationScheduler.Restore(long.MaxValue - 2, 0, []);
        var exhaustedSequence = SimulationScheduler.Restore(1, long.MaxValue - 2, []);

        Assert.Throws<OverflowException>(() =>
            exhaustedId.Schedule(new SimulationTime(0), Target(), ScheduledWorkKind.TravelArrival)
        );
        Assert.Throws<OverflowException>(() =>
            exhaustedSequence.Schedule(new SimulationTime(0), Target(), ScheduledWorkKind.TravelArrival)
        );
        Assert.Equal(long.MaxValue - 2, exhaustedId.NextWorkId);
        Assert.Equal(0, exhaustedId.NextSequence);
        Assert.Empty(exhaustedId.OutstandingWork);
        Assert.Equal(1, exhaustedSequence.NextWorkId);
        Assert.Equal(long.MaxValue - 2, exhaustedSequence.NextSequence);
        Assert.Empty(exhaustedSequence.OutstandingWork);
    }

    private static IEnumerable<T> OverflowAfter<T>(T value, int yieldedCount)
    {
        for (int index = 0; index < yieldedCount; index++)
        {
            yield return value;
        }

        throw new InvalidOperationException("The bounded consumer enumerated past its rejection threshold.");
    }

    private static ScheduledWork Work(long id, long sequence, long dueTime = 100, long targetShipId = 1) =>
        new(
            new ScheduledWorkId(id),
            new SimulationTime(dueTime),
            sequence,
            Target(targetShipId),
            ScheduledWorkKind.TravelArrival
        );

    private static ShipInstanceId Target(long value = 1) => new(value);
}
