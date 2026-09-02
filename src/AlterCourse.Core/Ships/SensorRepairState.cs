using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Correlates an active time-derived sensor repair with its scheduled completion.</summary>
public sealed record SensorRepairState
{
    internal SensorRepairState(
        SensorIntegrity startingIntegrity,
        SensorIntegrity targetIntegrity,
        SimulationTime startedAt,
        SimulationTime expectedCompletion,
        ScheduledWorkId scheduledCompletionId
    )
    {
        if (targetIntegrity.Value <= startingIntegrity.Value)
        {
            throw new ArgumentException("Repair target must exceed starting integrity.", nameof(targetIntegrity));
        }

        if (expectedCompletion.Milliseconds <= startedAt.Milliseconds)
        {
            throw new ArgumentException("Repair completion must follow its start.", nameof(expectedCompletion));
        }

        if (scheduledCompletionId.Value <= 0)
        {
            throw new ArgumentException("Repair requires initialized scheduled work.", nameof(scheduledCompletionId));
        }

        StartingIntegrity = startingIntegrity;
        TargetIntegrity = targetIntegrity;
        StartedAt = startedAt;
        ExpectedCompletion = expectedCompletion;
        ScheduledCompletionId = scheduledCompletionId;
    }

    /// <summary>Gets integrity at repair start.</summary>
    public SensorIntegrity StartingIntegrity { get; }

    /// <summary>Gets integrity materialized at completion.</summary>
    public SensorIntegrity TargetIntegrity { get; }

    /// <summary>Gets repair start time.</summary>
    public SimulationTime StartedAt { get; }

    /// <summary>Gets scheduled completion time.</summary>
    public SimulationTime ExpectedCompletion { get; }

    /// <summary>Gets correlated scheduled-work identity.</summary>
    public ScheduledWorkId ScheduledCompletionId { get; }

    internal double ProgressAt(SimulationTime time)
    {
        double elapsed = time.Milliseconds - StartedAt.Milliseconds;
        double duration = ExpectedCompletion.Milliseconds - StartedAt.Milliseconds;
        return Math.Clamp(elapsed / duration, 0, 1);
    }

    internal SensorIntegrity IntegrityAt(SimulationTime time)
    {
        double progress = ProgressAt(time);
        return new SensorIntegrity(
            StartingIntegrity.Value + ((TargetIntegrity.Value - StartingIntegrity.Value) * progress)
        );
    }
}
