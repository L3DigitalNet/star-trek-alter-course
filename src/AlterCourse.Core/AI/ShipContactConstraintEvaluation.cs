namespace AlterCourse.Core.AI;

/// <summary>Reports whether one hard candidate constraint was satisfied.</summary>
public readonly record struct ShipContactConstraintEvaluation(ShipContactDecisionConstraint Constraint, bool Satisfied);
