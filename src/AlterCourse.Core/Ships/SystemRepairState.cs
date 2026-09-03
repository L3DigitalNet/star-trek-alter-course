using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Correlates one analytical system repair with its exact scheduled completion.</summary>
public sealed record SystemRepairState
{
    internal SystemRepairState(
        ShipSystemId targetSystem,
        SystemCondition startingCondition,
        SystemCondition targetCondition,
        SimulationTime startedAt,
        SimulationTime expectedCompletion,
        ScheduledWorkId scheduledCompletionId
    )
    {
        if (targetSystem != ShipSystemId.Sensors && targetSystem != ShipSystemId.ImpulsePropulsion)
        {
            throw new ArgumentException("Only sensors and impulse propulsion are repairable.", nameof(targetSystem));
        }

        if (targetCondition.Value <= startingCondition.Value)
        {
            throw new ArgumentException("Repair target must exceed starting condition.", nameof(targetCondition));
        }

        if (expectedCompletion.Milliseconds <= startedAt.Milliseconds)
        {
            throw new ArgumentException("Repair completion must follow its start.", nameof(expectedCompletion));
        }

        if (scheduledCompletionId.Value <= 0)
        {
            throw new ArgumentException("Repair requires initialized scheduled work.", nameof(scheduledCompletionId));
        }

        TargetSystem = targetSystem;
        StartingCondition = startingCondition;
        TargetCondition = targetCondition;
        StartedAt = startedAt;
        ExpectedCompletion = expectedCompletion;
        ScheduledCompletionId = scheduledCompletionId;
    }

    /// <summary>Gets the repaired system identity.</summary>
    public ShipSystemId TargetSystem { get; }

    /// <summary>Gets condition at repair start.</summary>
    public SystemCondition StartingCondition { get; }

    /// <summary>Gets condition materialized at completion.</summary>
    public SystemCondition TargetCondition { get; }

    /// <summary>Gets repair start time.</summary>
    public SimulationTime StartedAt { get; }

    /// <summary>Gets expected completion time.</summary>
    public SimulationTime ExpectedCompletion { get; }

    /// <summary>Gets the exact correlated scheduled-work identity.</summary>
    public ScheduledWorkId ScheduledCompletionId { get; }

    /// <summary>Gets normalized analytical progress at a simulation time.</summary>
    public double ProgressAt(SimulationTime time)
    {
        double elapsed = time.Milliseconds - StartedAt.Milliseconds;
        double duration = ExpectedCompletion.Milliseconds - StartedAt.Milliseconds;
        return Math.Clamp(elapsed / duration, 0, 1);
    }

    internal SystemCondition ConditionAt(SimulationTime time)
    {
        double progress = ProgressAt(time);
        return new SystemCondition(
            StartingCondition.Value + ((TargetCondition.Value - StartingCondition.Value) * progress)
        );
    }
}
