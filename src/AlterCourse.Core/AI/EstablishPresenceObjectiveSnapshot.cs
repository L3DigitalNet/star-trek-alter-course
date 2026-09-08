using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.AI;

/// <summary>Identifies the location where a faction intends to establish ship presence.</summary>
public sealed record EstablishPresenceObjectiveSnapshot
{
    /// <summary>Initializes an establish-presence objective from actor-known topology.</summary>
    public EstablishPresenceObjectiveSnapshot(LocationId targetLocationId)
    {
        if (string.IsNullOrWhiteSpace(targetLocationId.Value))
        {
            throw new ArgumentException(
                "A presence objective requires an initialized location.",
                nameof(targetLocationId)
            );
        }

        TargetLocationId = targetLocationId;
    }

    /// <summary>Gets the location where presence is required.</summary>
    public LocationId TargetLocationId { get; }
}
