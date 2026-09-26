using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Gameplay;

/// <summary>Result of one trusted power-allocation transition on one ship; state is a candidate until committed.</summary>
internal sealed record EngineeringApplication(
    PowerAllocationOutcome Outcome,
    InstalledSystemId? Consumer,
    SimulationState CandidateState,
    IReadOnlyList<PlayerAdvanceEvent> Events
);
