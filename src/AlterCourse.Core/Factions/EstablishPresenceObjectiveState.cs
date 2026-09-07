using AlterCourse.Core.Identity;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Factions;

/// <summary>Retains one establish-presence objective and its exact ordinary-order commitment.</summary>
internal sealed record EstablishPresenceObjectiveState(
    LocationId TargetLocationId,
    FactionObjectiveStatus Status,
    ShipInstanceId? AssignedShipId = null,
    ShipOrderId? AssignedOrderId = null
);
