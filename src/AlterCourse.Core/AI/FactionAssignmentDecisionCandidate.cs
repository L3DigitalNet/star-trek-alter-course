using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.AI;

/// <summary>Explains the assignment eligibility and ranking facts for one controlled ship.</summary>
public sealed record FactionAssignmentDecisionCandidate
{
    /// <summary>Initializes complete evidence for one candidate.</summary>
    public FactionAssignmentDecisionCandidate(
        ShipInstanceId shipId,
        LocationId? currentLocationId,
        FactionAssignmentCandidateReason reason,
        SimulationDuration? directRouteDuration
    )
    {
        if (shipId.Value <= 0)
        {
            throw new ArgumentException("A decision candidate requires an initialized ship identity.", nameof(shipId));
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), "Assignment candidate reason is unsupported.");
        }

        if ((reason == FactionAssignmentCandidateReason.Eligible) != (directRouteDuration is not null))
        {
            throw new ArgumentException(
                "An eligible candidate requires a route duration, and a rejected candidate cannot have one.",
                nameof(directRouteDuration)
            );
        }

        ShipId = shipId;
        CurrentLocationId = currentLocationId;
        Reason = reason;
        DirectRouteDuration = directRouteDuration;
    }

    /// <summary>Gets the evaluated ship identity.</summary>
    public ShipInstanceId ShipId { get; }

    /// <summary>Gets the ship's location when one was available to the policy.</summary>
    public LocationId? CurrentLocationId { get; }

    /// <summary>Gets the first hard constraint result for the candidate.</summary>
    public FactionAssignmentCandidateReason Reason { get; }

    /// <summary>Gets the direct route duration used for ranking an eligible candidate.</summary>
    public SimulationDuration? DirectRouteDuration { get; }

    /// <summary>Gets whether every hard assignment constraint passed.</summary>
    public bool IsEligible => Reason == FactionAssignmentCandidateReason.Eligible;
}
