namespace AlterCourse.Core.Simulation;

/// <summary>Represents monotonic elapsed universe time in integer simulation milliseconds.</summary>
public readonly record struct SimulationTime
{
    /// <summary>Initializes a simulation time.</summary>
    /// <param name="milliseconds">The nonnegative elapsed time in milliseconds.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="milliseconds"/> is negative.
    /// </exception>
    public SimulationTime(long milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        Milliseconds = milliseconds;
    }

    /// <summary>Gets the elapsed time in integer simulation milliseconds.</summary>
    public long Milliseconds { get; }

    /// <summary>Advances by an explicit duration without allowing integer wraparound.</summary>
    /// <param name="duration">The nonnegative duration to advance.</param>
    /// <returns>The advanced time.</returns>
    /// <exception cref="OverflowException">The advanced time exceeds the supported range.</exception>
    public SimulationTime AdvanceBy(SimulationDuration duration) =>
        new(checked(Milliseconds + duration.Milliseconds));

    /// <summary>Advances to an explicit monotonic target.</summary>
    /// <param name="target">The target simulation time.</param>
    /// <returns><paramref name="target"/> when it is not earlier than this value.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="target"/> is earlier than this value.</exception>
    public SimulationTime AdvanceTo(SimulationTime target)
    {
        if (target.Milliseconds < Milliseconds)
        {
            throw new InvalidOperationException("Simulation time cannot move backwards.");
        }

        return target;
    }
}
