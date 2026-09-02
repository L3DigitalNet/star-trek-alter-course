using System.Collections.Immutable;
using AlterCourse.Core.Identity;

namespace AlterCourse.Core.Simulation;

/// <summary>Stores and orders immutable data-only scheduled simulation work.</summary>
public sealed class SimulationScheduler
{
    private SimulationScheduler(
        long nextWorkId,
        long nextSequence,
        ImmutableArray<ScheduledWork> outstandingWork
    )
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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextWorkId);
        ArgumentOutOfRangeException.ThrowIfNegative(nextSequence);
        ArgumentNullException.ThrowIfNull(outstandingWork);

        ScheduledWork[] work = outstandingWork.ToArray();
        HashSet<long> identities = [];
        HashSet<long> sequences = [];

        foreach (ScheduledWork item in work)
        {
            ValidateRestoredItem(item, nextWorkId, nextSequence, identities, sequences);
        }

        ImmutableArray<ScheduledWork> ordered =
            [.. work.OrderBy(item => item.DueTime.Milliseconds).ThenBy(item => item.Sequence)];
        return new SimulationScheduler(nextWorkId, nextSequence, ordered);
    }

    /// <summary>Schedules a known consequence and returns the following scheduler state.</summary>
    /// <param name="dueTime">The simulation time at which the work becomes due.</param>
    /// <param name="kind">The known consequence kind.</param>
    /// <returns>The following scheduler state and scheduled work item.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    /// <exception cref="OverflowException">A following identity or sequence cannot be represented.</exception>
    public (SimulationScheduler Scheduler, ScheduledWork Work) Schedule(
        SimulationTime dueTime,
        ScheduledWorkKind kind
    )
    {
        ScheduledWork.ValidateKind(kind);
        long followingWorkId = checked(NextWorkId + 1);
        long followingSequence = checked(NextSequence + 1);
        ScheduledWork scheduled = new(new ScheduledWorkId(NextWorkId), dueTime, NextSequence, kind);

        ImmutableArray<ScheduledWork> outstanding = OutstandingWork.Add(scheduled).Sort(CompareWork);
        return (new SimulationScheduler(followingWorkId, followingSequence, outstanding), scheduled);
    }

    /// <summary>Removes and returns every item due at or before an explicit boundary.</summary>
    /// <param name="through">The inclusive simulation-time boundary.</param>
    /// <returns>The following scheduler state and due work in stable order.</returns>
    public (SimulationScheduler Scheduler, IReadOnlyList<ScheduledWork> DueWork) DequeueDue(
        SimulationTime through
    )
    {
        int dueCount = 0;
        while (
            dueCount < OutstandingWork.Length
            && OutstandingWork[dueCount].DueTime.Milliseconds <= through.Milliseconds
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

    private static void ValidateRestoredItem(
        ScheduledWork item,
        long nextWorkId,
        long nextSequence,
        HashSet<long> identities,
        HashSet<long> sequences
    )
    {
        if (item.Id.Value <= 0)
        {
            throw new ArgumentException("Outstanding work contains an uninitialized identity.", nameof(item));
        }

        if (item.Sequence < 0)
        {
            throw new ArgumentException("Outstanding work contains a negative sequence.", nameof(item));
        }

        try
        {
            ScheduledWork.ValidateKind(item.Kind);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ArgumentException("Outstanding work contains an unknown kind.", nameof(item), exception);
        }

        if (!identities.Add(item.Id.Value))
        {
            throw new ArgumentException("Outstanding work contains a duplicate identity.", nameof(item));
        }

        if (!sequences.Add(item.Sequence))
        {
            throw new ArgumentException("Outstanding work contains a duplicate sequence.", nameof(item));
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
    }
}
