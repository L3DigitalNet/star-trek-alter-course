using AlterCourse.Core.Identity;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.AI;

/// <summary>Requests validated ordinary travel for one directly controlled ship.</summary>
public sealed record FactionAssignmentProposal
{
    /// <summary>Initializes a typed faction assignment proposal.</summary>
    public FactionAssignmentProposal(FactionId factionId, ShipInstanceId shipId, LocationId destination)
    {
        if (factionId.Value <= 0)
        {
            throw new ArgumentException(
                "An assignment proposal requires an initialized faction identity.",
                nameof(factionId)
            );
        }

        if (shipId.Value <= 0)
        {
            throw new ArgumentException(
                "An assignment proposal requires an initialized ship identity.",
                nameof(shipId)
            );
        }

        if (string.IsNullOrWhiteSpace(destination.Value))
        {
            throw new ArgumentException(
                "An assignment proposal requires an initialized destination.",
                nameof(destination)
            );
        }

        FactionId = factionId;
        ShipId = shipId;
        Destination = destination;
    }

    /// <summary>Gets the faction requesting the assignment.</summary>
    public FactionId FactionId { get; }

    /// <summary>Gets the selected directly controlled ship.</summary>
    public ShipInstanceId ShipId { get; }

    /// <summary>Gets the requested travel destination.</summary>
    public LocationId Destination { get; }
}
