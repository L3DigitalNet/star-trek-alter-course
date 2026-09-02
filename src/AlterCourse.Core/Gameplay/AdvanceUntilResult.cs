using AlterCourse.Core.Player;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Returns the next player-relevant event boundary and resulting player view.</summary>
public sealed record AdvanceUntilResult
{
    internal AdvanceUntilResult(
        AdvanceUntilOutcome outcome,
        SimulationTime stoppedAt,
        IReadOnlyList<PlayerAdvanceEvent> resolvedEvents,
        PlayerProjection projection
    ) => (Outcome, StoppedAt, ResolvedEvents, Projection) = (outcome, stoppedAt, resolvedEvents, projection);

    /// <summary>Gets the stop outcome.</summary>
    public AdvanceUntilOutcome Outcome { get; }

    /// <summary>Gets the exact stop time.</summary>
    public SimulationTime StoppedAt { get; }

    /// <summary>Gets player-visible consequences resolved through the stop boundary in stable order.</summary>
    public IReadOnlyList<PlayerAdvanceEvent> ResolvedEvents { get; }

    /// <summary>Gets the fresh player view after resolution.</summary>
    public PlayerProjection Projection { get; }
}
