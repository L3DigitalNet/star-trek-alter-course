namespace AlterCourse.Core.Gameplay;

/// <summary>Returns the immutable candidate state for one checked faction proposal.</summary>
internal sealed record FactionAssignmentApplicationResult(
    FactionAssignmentApplicationOutcome Outcome,
    SimulationState CandidateState
);
