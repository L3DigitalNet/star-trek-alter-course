namespace AlterCourse.Core.Simulation;

/// <summary>Represents a nonnegative duration in integer simulation milliseconds.</summary>
public readonly record struct SimulationDuration
{
    /// <summary>Initializes a simulation duration.</summary>
    /// <param name="milliseconds">The nonnegative duration in milliseconds.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="milliseconds"/> is negative.
    /// </exception>
    public SimulationDuration(long milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        Milliseconds = milliseconds;
    }

    /// <summary>Gets the duration in integer simulation milliseconds.</summary>
    public long Milliseconds { get; }

    /// <summary>Adds another duration without allowing integer wraparound.</summary>
    /// <param name="other">The duration to add.</param>
    /// <returns>The combined duration.</returns>
    /// <exception cref="OverflowException">The combined duration exceeds the supported range.</exception>
    public SimulationDuration Add(SimulationDuration other) => new(checked(Milliseconds + other.Milliseconds));
}
