using AlterCourse.Core.Player;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Returns consequential outcomes and the fresh player view after fixed-step advancement.</summary>
public sealed record SimulationAdvanceResult
{
    internal SimulationAdvanceResult(
        SimulationTime finalTime,
        IReadOnlyList<ScheduledWorkKind> resolvedKinds,
        PlayerProjection projection
    ) => (FinalTime, ResolvedKinds, Projection) = (finalTime, resolvedKinds, projection);

    /// <summary>Gets the final authoritative simulation time.</summary>
    public SimulationTime FinalTime { get; }

    /// <summary>Gets player-targeted consequences resolved during advancement in deterministic execution order.</summary>
    public IReadOnlyList<ScheduledWorkKind> ResolvedKinds { get; }

    /// <summary>Gets the fresh player view after all requested steps and consequences resolve.</summary>
    public PlayerProjection Projection { get; }
}
