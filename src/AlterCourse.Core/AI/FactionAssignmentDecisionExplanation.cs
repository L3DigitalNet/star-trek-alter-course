namespace AlterCourse.Core.AI;

/// <summary>Explains one deterministic faction assignment decision and its optional typed proposal.</summary>
public sealed record FactionAssignmentDecisionExplanation
{
    private readonly ReadOnlyDecisionList<FactionAssignmentDecisionCandidate> _candidates;

    /// <summary>Initializes the complete result and diagnostics for one faction decision.</summary>
    public FactionAssignmentDecisionExplanation(
        FactionAssignmentDecisionInput actorKnownFacts,
        IEnumerable<FactionAssignmentDecisionCandidate> candidates,
        FactionAssignmentDecisionTieRule tieRule,
        FactionAssignmentDecisionOutcome outcome,
        FactionAssignmentProposal? proposal,
        bool randomnessUsed
    )
    {
        ArgumentNullException.ThrowIfNull(actorKnownFacts);
        ArgumentNullException.ThrowIfNull(candidates);
        FactionAssignmentDecisionCandidate[] materialized = candidates.ToArray();
        if (materialized.Select(candidate => candidate.ShipId).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("Decision candidates require unique ship identities.", nameof(candidates));
        }

        if (!Enum.IsDefined(tieRule))
        {
            throw new ArgumentOutOfRangeException(nameof(tieRule), "Assignment tie rule is unsupported.");
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), "Assignment decision outcome is unsupported.");
        }

        if ((outcome == FactionAssignmentDecisionOutcome.AssignmentProposed) != (proposal is not null))
        {
            throw new ArgumentException(
                "Exactly an assignment-proposed outcome requires a typed proposal.",
                nameof(proposal)
            );
        }

        ActorKnownFacts = actorKnownFacts;
        _candidates = new ReadOnlyDecisionList<FactionAssignmentDecisionCandidate>(materialized);
        TieRule = tieRule;
        Outcome = outcome;
        Proposal = proposal;
        RandomnessUsed = randomnessUsed;
    }

    /// <summary>Gets the complete immutable actor-safe facts used by the policy.</summary>
    public FactionAssignmentDecisionInput ActorKnownFacts { get; }

    /// <summary>Gets every directly controlled candidate in stable ship-identity order.</summary>
    public IReadOnlyList<FactionAssignmentDecisionCandidate> Candidates => _candidates;

    /// <summary>Gets the deterministic rule used to rank eligible ships.</summary>
    public FactionAssignmentDecisionTieRule TieRule { get; }

    /// <summary>Gets the decision outcome.</summary>
    public FactionAssignmentDecisionOutcome Outcome { get; }

    /// <summary>Gets the proposed ordinary travel assignment when one was selected.</summary>
    public FactionAssignmentProposal? Proposal { get; }

    /// <summary>Gets whether the decision consumed randomness.</summary>
    public bool RandomnessUsed { get; }
}
