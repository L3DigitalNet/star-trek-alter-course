namespace AlterCourse.Core.AI;

/// <summary>Explains hard constraints, score, and policy reason for one candidate.</summary>
public sealed record ShipContactDecisionCandidate
{
    private readonly ReadOnlyDecisionList<ShipContactConstraintEvaluation> _constraints;

    /// <summary>Initializes one candidate explanation.</summary>
    public ShipContactDecisionCandidate(
        ShipContactDecisionAction action,
        IEnumerable<ShipContactConstraintEvaluation> constraints,
        int? score,
        ShipContactDecisionPolicyReason policyReason
    )
    {
        ArgumentNullException.ThrowIfNull(constraints);
        ShipContactConstraintEvaluation[] materialized = constraints.ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("A decision candidate requires hard-constraint evidence.", nameof(constraints));
        }

        Action = action;
        _constraints = new ReadOnlyDecisionList<ShipContactConstraintEvaluation>(materialized);
        Score = score;
        PolicyReason = policyReason;
    }

    /// <summary>Gets the candidate action.</summary>
    public ShipContactDecisionAction Action { get; }

    /// <summary>Gets every hard constraint evaluated for this candidate.</summary>
    public IReadOnlyList<ShipContactConstraintEvaluation> Constraints => _constraints;

    /// <summary>Gets whether all hard constraints passed.</summary>
    public bool HardConstraintsSatisfied => _constraints.All(result => result.Satisfied);

    /// <summary>Gets the policy score, or null when a hard constraint rejected the candidate.</summary>
    public int? Score { get; }

    /// <summary>Gets the policy rule responsible for this evaluation.</summary>
    public ShipContactDecisionPolicyReason PolicyReason { get; }
}
