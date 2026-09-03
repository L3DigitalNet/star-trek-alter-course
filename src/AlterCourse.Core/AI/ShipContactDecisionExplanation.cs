using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.AI;

/// <summary>Explains one deterministic contact decision and its resulting typed course intent.</summary>
public sealed record ShipContactDecisionExplanation
{
    private readonly ReadOnlyDecisionList<ShipContactDecisionCandidate> _candidates;

    /// <summary>Initializes a complete contact-decision explanation.</summary>
    public ShipContactDecisionExplanation(
        ShipInstanceId decidingShipId,
        SimulationTime decisionTime,
        ShipContactDecisionGoal goal,
        ShipContactPosture posture,
        ShipContactDecisionFacts actorKnownFacts,
        SensorContactId? primaryContactId,
        IEnumerable<ShipContactDecisionCandidate> candidates,
        ShipContactDecisionTieRule tieRule,
        ShipContactDecisionAction? selectedAction,
        SetTacticalCourseIntent? resultingCourse,
        bool randomnessUsed
    )
    {
        ArgumentNullException.ThrowIfNull(actorKnownFacts);
        ArgumentNullException.ThrowIfNull(candidates);
        ShipContactDecisionCandidate[] materialized = candidates.ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("A decision explanation requires candidate evidence.", nameof(candidates));
        }

        DecidingShipId = decidingShipId;
        DecisionTime = decisionTime;
        Goal = goal;
        Posture = posture;
        ActorKnownFacts = actorKnownFacts;
        PrimaryContactId = primaryContactId;
        _candidates = new ReadOnlyDecisionList<ShipContactDecisionCandidate>(materialized);
        TieRule = tieRule;
        SelectedAction = selectedAction;
        ResultingCourse = resultingCourse;
        RandomnessUsed = randomnessUsed;
    }

    /// <summary>Gets the ship that made the decision.</summary>
    public ShipInstanceId DecidingShipId { get; }

    /// <summary>Gets the authoritative simulation time of the decision.</summary>
    public SimulationTime DecisionTime { get; }

    /// <summary>Gets the actor goal used to evaluate candidates.</summary>
    public ShipContactDecisionGoal Goal { get; }

    /// <summary>Gets the actor posture used to evaluate candidates.</summary>
    public ShipContactPosture Posture { get; }

    /// <summary>Gets the complete actor-safe facts used by the policy.</summary>
    public ShipContactDecisionFacts ActorKnownFacts { get; }

    /// <summary>Gets the observer-local primary contact, when one is current.</summary>
    public SensorContactId? PrimaryContactId { get; }

    /// <summary>Gets all candidates in stable Hold, Approach, Withdraw order.</summary>
    public IReadOnlyList<ShipContactDecisionCandidate> Candidates => _candidates;

    /// <summary>Gets the deterministic rule used to resolve equal scores.</summary>
    public ShipContactDecisionTieRule TieRule { get; }

    /// <summary>Gets the selected action, or null when no action is legal.</summary>
    public ShipContactDecisionAction? SelectedAction { get; }

    /// <summary>Gets the resulting course intent, or null when no course command is needed.</summary>
    public SetTacticalCourseIntent? ResultingCourse { get; }

    /// <summary>Gets whether the decision consumed randomness.</summary>
    public bool RandomnessUsed { get; }
}
