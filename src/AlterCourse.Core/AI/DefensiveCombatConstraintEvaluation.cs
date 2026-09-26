namespace AlterCourse.Core.AI;

/// <summary>Records one deterministic actor-safe defensive decision fact.</summary>
internal sealed record DefensiveCombatConstraintEvaluation(DefensiveCombatConstraint Constraint, bool Satisfied);
