using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.AI;

/// <summary>Defines the complete immutable input to one cautious-contact decision.</summary>
public sealed record ShipContactDecisionInput
{
    /// <summary>Initializes a supported cautious-contact decision request.</summary>
    public ShipContactDecisionInput(
        ShipInstanceId decidingShipId,
        SimulationTime decisionTime,
        ShipContactDecisionGoal goal,
        ShipContactPosture posture,
        ShipContactDecisionFacts facts
    )
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (goal != ShipContactDecisionGoal.RespondCautiously)
        {
            throw new ArgumentOutOfRangeException(nameof(goal), "Decision goal is unsupported.");
        }

        if (posture != ShipContactPosture.CautiousContact)
        {
            throw new ArgumentOutOfRangeException(nameof(posture), "Decision posture is unsupported.");
        }

        DecidingShipId = decidingShipId;
        DecisionTime = decisionTime;
        Goal = goal;
        Posture = posture;
        Facts = facts;
    }

    /// <summary>Gets the ship that owns the decision.</summary>
    public ShipInstanceId DecidingShipId { get; }

    /// <summary>Gets the authoritative simulation time supplied to the policy.</summary>
    public SimulationTime DecisionTime { get; }

    /// <summary>Gets the active decision goal.</summary>
    public ShipContactDecisionGoal Goal { get; }

    /// <summary>Gets the active contact posture.</summary>
    public ShipContactPosture Posture { get; }

    /// <summary>Gets the immutable actor-safe facts supplied to the policy.</summary>
    public ShipContactDecisionFacts Facts { get; }
}
