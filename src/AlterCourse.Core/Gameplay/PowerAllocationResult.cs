namespace AlterCourse.Core.Gameplay;

/// <summary>Returns a typed allocation outcome and same-time player-visible consequences.</summary>
public sealed record PowerAllocationResult(
    PowerAllocationOutcome Outcome,
    IReadOnlyList<PlayerAdvanceEvent> ResolvedEvents
);
