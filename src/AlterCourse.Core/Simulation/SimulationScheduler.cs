using System.Collections.Immutable;
using AlterCourse.Core.Identity;

namespace AlterCourse.Core.Simulation;

/// <summary>Stores and orders immutable data-only scheduled simulation work.</summary>
public sealed class SimulationScheduler
{
    /// <summary>Gets the maximum number of outstanding consequences retained by one scheduler.</summary>
    public const int MaximumOutstandingWork = 4096;

    private SimulationScheduler(long nextWorkId, long nextSequence, ImmutableArray<ScheduledWork> outstandingWork)
    {
        NextWorkId = nextWorkId;
        NextSequence = nextSequence;
        OutstandingWork = outstandingWork;
    }

    /// <summary>Gets the next persisted scheduled-work identity value.</summary>
    public long NextWorkId { get; }

    /// <summary>Gets the next persisted stable ordering sequence.</summary>
    public long NextSequence { get; }

    /// <summary>Gets outstanding work in due-time and sequence order.</summary>
    public ImmutableArray<ScheduledWork> OutstandingWork { get; }

    /// <summary>Creates an empty scheduler at its initial deterministic state.</summary>
    /// <returns>An empty scheduler.</returns>
    public static SimulationScheduler Create() => new(1, 0, []);

    /// <summary>Restores persisted scheduler state after validating its ordering invariants.</summary>
    /// <param name="nextWorkId">The positive next scheduled-work identity value.</param>
    /// <param name="nextSequence">The nonnegative next ordering sequence.</param>
    /// <param name="outstandingWork">The outstanding data-only scheduled work.</param>
    /// <returns>A validated scheduler ordered by due time and sequence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outstandingWork"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A next counter is invalid or does not exceed every corresponding outstanding value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Outstanding work contains an invalid item, duplicate identity, or duplicate sequence.
    /// </exception>
    public static SimulationScheduler Restore(
        long nextWorkId,
        long nextSequence,
        IEnumerable<ScheduledWork> outstandingWork
    )
    {
        ArgumentNullException.ThrowIfNull(outstandingWork);

        if (!AreCountersWithinPersistedRange(nextWorkId, nextSequence))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextWorkId),
                nextWorkId,
                "Scheduler counters are outside the persisted range."
            );
        }

        ScheduledWork[] work = outstandingWork.Take(MaximumOutstandingWork + 1).ToArray();
        if (work.Length > MaximumOutstandingWork)
        {
            throw new ArgumentException(
                $"A scheduler supports at most {MaximumOutstandingWork} outstanding work items.",
                nameof(outstandingWork)
            );
        }

        HashSet<long> identities = [];
        // Sequences are globally unique, not merely unique per due time: (DueTime, Sequence)
        // must remain a total order independent of input enumeration and sort stability.
        HashSet<long> sequences = [];

        foreach (ScheduledWork item in work)
        {
            ValidateRestoredItem(item, nextWorkId, nextSequence, identities, sequences, nameof(outstandingWork));
        }

        ImmutableArray<ScheduledWork> ordered =
        [
            .. work.OrderBy(item => item.DueTime.Milliseconds).ThenBy(item => item.Sequence),
        ];
        return new SimulationScheduler(nextWorkId, nextSequence, ordered);
    }

    /// <summary>Schedules a known consequence and returns the following scheduler state.</summary>
    /// <param name="dueTime">The simulation time at which the work becomes due.</param>
    /// <param name="targetShipId">The ship instance that owns the scheduled consequence.</param>
    /// <param name="kind">The known consequence kind.</param>
    /// <returns>The following scheduler state and scheduled work item.</returns>
    /// <exception cref="ArgumentException"><paramref name="targetShipId"/> is uninitialized.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    /// <exception cref="OverflowException">A following identity or sequence cannot be represented.</exception>
    public (SimulationScheduler Scheduler, ScheduledWork Work) Schedule(
        SimulationTime dueTime,
        ShipInstanceId targetShipId,
        ScheduledWorkKind kind
    )
    {
        ScheduledWork.ValidateTarget(targetShipId);
        ScheduledWork.ValidateKind(kind);
        if (OutstandingWork.Length >= MaximumOutstandingWork)
        {
            throw new InvalidOperationException(
                $"Scheduling would exceed the {MaximumOutstandingWork}-item outstanding-work limit."
            );
        }

        long followingWorkId = checked(NextWorkId + 1);
        long followingSequence = checked(NextSequence + 1);
        if (!AreCountersWithinPersistedRange(followingWorkId, followingSequence))
        {
            throw new OverflowException("Scheduling would produce counters outside the persisted range.");
        }

        ScheduledWork scheduled = new(new ScheduledWorkId(NextWorkId), dueTime, NextSequence, targetShipId, kind);

        ImmutableArray<ScheduledWork> outstanding = OutstandingWork.Add(scheduled).Sort(CompareWork);
        return (new SimulationScheduler(followingWorkId, followingSequence, outstanding), scheduled);
    }

    /// <summary>Cancels the outstanding work with an exact stable identity.</summary>
    /// <param name="id">The initialized identity of the work to cancel.</param>
    /// <returns>
    /// The following scheduler state and whether matching outstanding work was removed. A missing or stale identity is
    /// an ordinary negative result.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is uninitialized.</exception>
    public (SimulationScheduler Scheduler, bool Removed) Cancel(ScheduledWorkId id)
    {
        if (id.Value <= 0)
        {
            throw new ArgumentException("Cancellation requires an initialized scheduled-work identity.", nameof(id));
        }

        for (int index = 0; index < OutstandingWork.Length; index++)
        {
            if (OutstandingWork[index].Id == id)
            {
                return (
                    new SimulationScheduler(NextWorkId, NextSequence, OutstandingWork.RemoveAt(index)),
                    true
                );
            }
        }

        return (this, false);
    }

    /// <summary>
    /// Removes and returns an immutable snapshot of work outstanding at call time and due at or before a boundary.
    /// </summary>
    /// <param name="through">The inclusive simulation-time boundary.</param>
    /// <returns>The following scheduler state and due work in stable order.</returns>
    public (SimulationScheduler Scheduler, IReadOnlyList<ScheduledWork> DueWork) DequeueDue(SimulationTime through)
    {
        int dueCount = 0;
        while (
            dueCount < OutstandingWork.Length && OutstandingWork[dueCount].DueTime.Milliseconds <= through.Milliseconds
        )
        {
            dueCount++;
        }

        ImmutableArray<ScheduledWork> due = OutstandingWork[..dueCount];
        ImmutableArray<ScheduledWork> remaining = OutstandingWork[dueCount..];
        return (new SimulationScheduler(NextWorkId, NextSequence, remaining), due);
    }

    private static int CompareWork(ScheduledWork left, ScheduledWork right)
    {
        int dueComparison = left.DueTime.Milliseconds.CompareTo(right.DueTime.Milliseconds);
        return dueComparison != 0 ? dueComparison : left.Sequence.CompareTo(right.Sequence);
    }

    internal static bool AreCountersWithinPersistedRange(long nextWorkId, long nextSequence) =>
        nextWorkId > 0 && nextWorkId < long.MaxValue - 1 && nextSequence >= 0 && nextSequence < long.MaxValue - 1;

    private static void ValidateRestoredItem(
        ScheduledWork item,
        long nextWorkId,
        long nextSequence,
        HashSet<long> identities,
        HashSet<long> sequences,
        string outstandingWorkParameterName
    )
    {
        if (item.Id.Value <= 0)
        {
            throw InvalidOutstandingWork("Outstanding work contains an uninitialized identity.");
        }

        if (item.Sequence < 0)
        {
            throw InvalidOutstandingWork("Outstanding work contains a negative sequence.");
        }

        if (item.TargetShipId.Value <= 0)
        {
            throw InvalidOutstandingWork("Outstanding work contains an uninitialized target ship identity.");
        }

        try
        {
            ScheduledWork.ValidateKind(item.Kind);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ArgumentException(
                "Outstanding work contains an unknown kind.",
                outstandingWorkParameterName,
                exception
            );
        }

        if (!identities.Add(item.Id.Value))
        {
            throw InvalidOutstandingWork("Outstanding work contains a duplicate identity.");
        }

        if (!sequences.Add(item.Sequence))
        {
            throw InvalidOutstandingWork("Outstanding work contains a duplicate sequence.");
        }

        if (item.Id.Value >= nextWorkId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextWorkId),
                nextWorkId,
                "Next work identity must exceed every outstanding identity."
            );
        }

        if (item.Sequence >= nextSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextSequence),
                nextSequence,
                "Next sequence must exceed every outstanding sequence."
            );
        }

        ArgumentException InvalidOutstandingWork(string message) => new(message, outstandingWorkParameterName);
    }
}
