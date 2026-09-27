using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Describes one player-safe advancement event and its optional observer-local contact.</summary>
/// <remarks>
/// <see cref="SystemKind"/> is semantic: the aim kind on attacker-facing fire and penetration events, and
/// <c>shields</c> on <see cref="PlayerAdvanceEventKind.ShieldImpact"/> whatever the aim. <see cref="InstalledSystemId"/> is
/// set only on events about the player's own installations and is always null on attacker-facing events, so another
/// ship's installed identities, inventory, and damage never reach the player through this record.
/// </remarks>
public sealed record PlayerAdvanceEvent(
    PlayerAdvanceEventKind Kind,
    SimulationTime OccurredAt,
    SensorContactId? SensorContactId = null,
    ShipSystemKind? SystemKind = null,
    InstalledSystemId? InstalledSystemId = null
);
