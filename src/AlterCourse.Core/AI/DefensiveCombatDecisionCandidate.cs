using AlterCourse.Core.Gameplay;

namespace AlterCourse.Core.AI;

/// <summary>Records one deterministic actor-safe defensive decision fact.</summary>
internal sealed record DefensiveCombatDecisionCandidate(
    DefensiveCombatDecisionAction Action,
    IReadOnlyList<DefensiveCombatConstraintEvaluation> Constraints,
    int? Rank,
    FireDirectedEnergyOutcome? FireRejection
);
