namespace AlterCourse.Core.AI;

/// <summary>Explains one deterministic investigation decision and its optional typed proposal.</summary>
public sealed record FactionInvestigationDecisionExplanation
{
    private readonly ReadOnlyDecisionList<FactionInvestigationReportEvaluation> _reports;
    private readonly ReadOnlyDecisionList<FactionInvestigationDecisionCandidate> _candidates;

    internal FactionInvestigationDecisionExplanation(
        FactionInvestigationDecisionInput actorKnownFacts,
        IEnumerable<FactionInvestigationReportEvaluation> reports,
        IEnumerable<FactionInvestigationDecisionCandidate> candidates,
        FactionInvestigationDecisionOutcome outcome,
        FactionInvestigationProposal? proposal
    )
    {
        ActorKnownFacts = actorKnownFacts;
        _reports = new ReadOnlyDecisionList<FactionInvestigationReportEvaluation>(reports);
        _candidates = new ReadOnlyDecisionList<FactionInvestigationDecisionCandidate>(candidates);
        Outcome = outcome;
        Proposal = proposal;
        TieRule = FactionInvestigationDecisionTieRule.ReportRecencyThenResponderRoute;
        RandomnessUsed = false;
    }

    /// <summary>Gets the complete immutable actor-safe facts used by the policy.</summary>
    public FactionInvestigationDecisionInput ActorKnownFacts { get; }

    /// <summary>Gets each report considered before selection in deterministic priority order.</summary>
    public IReadOnlyList<FactionInvestigationReportEvaluation> Reports => _reports;

    /// <summary>Gets report-specific candidate evaluations in stable ship order.</summary>
    public IReadOnlyList<FactionInvestigationDecisionCandidate> Candidates => _candidates;

    /// <summary>Gets the deterministic report and responder tie rule.</summary>
    public FactionInvestigationDecisionTieRule TieRule { get; }

    /// <summary>Gets the decision outcome.</summary>
    public FactionInvestigationDecisionOutcome Outcome { get; }

    /// <summary>Gets the exact proposed investigation when one was selected.</summary>
    public FactionInvestigationProposal? Proposal { get; }

    /// <summary>Gets whether the decision consumed randomness.</summary>
    public bool RandomnessUsed { get; }
}
