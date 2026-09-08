using AlterCourse.Core.Identity;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.AI;

/// <summary>Contains only assignment-relevant facts for one directly controlled ship.</summary>
public sealed record FactionShipAssignmentSnapshot
{
    /// <summary>Initializes an immutable own-asset administrative snapshot.</summary>
    public FactionShipAssignmentSnapshot(
        ShipInstanceId shipId,
        bool isPlayerShip,
        FactionShipStrategicStatus strategicStatus,
        LocationId? currentLocationId,
        bool hasActiveOrder
    )
    {
        if (shipId.Value <= 0)
        {
            throw new ArgumentException(
                "A faction ship snapshot requires an initialized ship identity.",
                nameof(shipId)
            );
        }

        if (!Enum.IsDefined(strategicStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(strategicStatus), "Ship strategic status is unsupported.");
        }

        bool validLocation = strategicStatus switch
        {
            FactionShipStrategicStatus.AtLocation => currentLocationId is { } location
                && !string.IsNullOrWhiteSpace(location.Value),
            FactionShipStrategicStatus.Traveling => currentLocationId is null,
            _ => false,
        };
        if (!validLocation)
        {
            throw new ArgumentException(
                "A current location is required exactly when the ship is at a location.",
                nameof(currentLocationId)
            );
        }

        ShipId = shipId;
        IsPlayerShip = isPlayerShip;
        StrategicStatus = strategicStatus;
        CurrentLocationId = currentLocationId;
        HasActiveOrder = hasActiveOrder;
    }

    /// <summary>Gets the directly controlled ship identity.</summary>
    public ShipInstanceId ShipId { get; }

    /// <summary>Gets whether this ship is reserved for player control.</summary>
    public bool IsPlayerShip { get; }

    /// <summary>Gets the ship's assignment-relevant strategic status.</summary>
    public FactionShipStrategicStatus StrategicStatus { get; }

    /// <summary>Gets the current location when the ship is at one.</summary>
    public LocationId? CurrentLocationId { get; }

    /// <summary>Gets whether an ordinary ship order already commits the ship.</summary>
    public bool HasActiveOrder { get; }
}
