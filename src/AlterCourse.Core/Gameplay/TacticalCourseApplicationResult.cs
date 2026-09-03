namespace AlterCourse.Core.Gameplay;

internal sealed record TacticalCourseApplicationResult(
    SetTacticalCourseOutcome Outcome,
    SimulationState CandidateState
);
