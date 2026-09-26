namespace AlterCourse.Core.Gameplay;

/// <summary>Returns the actor-safe outcome and qualitative observed consequences of a fire command.</summary>
public sealed record FireDirectedEnergyResult(
    FireDirectedEnergyOutcome Outcome,
    IReadOnlyList<PlayerAdvanceEvent> ResolvedEvents
);
