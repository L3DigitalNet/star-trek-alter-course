namespace AlterCourse.Core.Gameplay;

/// <summary>Result of one trusted repair transition; state is a candidate until committed.</summary>
internal sealed record RepairApplication(SystemRepairOutcome Outcome, SimulationState CandidateState);
