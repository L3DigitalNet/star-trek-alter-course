namespace AlterCourse.Core.Simulation;

/// <summary>Defines the authoritative fixed tactical simulation quantum.</summary>
public static class SimulationFixedStep
{
    /// <summary>Gets the fixed one-hundred-millisecond quantum.</summary>
    public static SimulationDuration Duration { get; } = new(100);
}
