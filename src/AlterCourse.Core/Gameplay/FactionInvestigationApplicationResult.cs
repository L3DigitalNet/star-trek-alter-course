using AlterCourse.Core.AI;

namespace AlterCourse.Core.Gameplay;

internal sealed record FactionInvestigationApplicationResult(
    FactionInvestigationApplicationOutcome Outcome,
    SimulationState CandidateState,
    FactionInvestigationProposal Proposal
);
