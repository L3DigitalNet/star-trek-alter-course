using AlterCourse.Core.Player;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Returns a typed scheduler-boundary stop and resulting player view.</summary>
public sealed record AdvanceUntilResult
{
    internal AdvanceUntilResult(
        AdvanceUntilOutcome outcome,
        SimulationTime stoppedAt,
        IReadOnlyList<ScheduledWorkKind> resolvedKinds,
        PlayerProjection projection
    ) => (Outcome, StoppedAt, ResolvedKinds, Projection) = (outcome, stoppedAt, resolvedKinds, projection);

    /// <summary>Gets the stop outcome.</summary>
    public AdvanceUntilOutcome Outcome { get; }

    /// <summary>Gets the exact stop time.</summary>
    public SimulationTime StoppedAt { get; }

    /// <summary>Gets player-targeted consequences resolved through the stop boundary in stable order.</summary>
    public IReadOnlyList<ScheduledWorkKind> ResolvedKinds { get; }

    /// <summary>Gets the fresh player view after resolution.</summary>
    public PlayerProjection Projection { get; }
}
