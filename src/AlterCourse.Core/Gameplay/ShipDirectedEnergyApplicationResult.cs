namespace AlterCourse.Core.Gameplay;

/// <summary>Retains one immutable candidate and its actor-safe events for trusted Core callers.</summary>
internal sealed record ShipDirectedEnergyApplicationResult(
    FireDirectedEnergyOutcome Outcome,
    SimulationState CandidateState,
    IReadOnlyList<PlayerAdvanceEvent> ResolvedEvents
);
