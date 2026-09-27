using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Player;

/// <summary>
/// Preserves aim choices even when a transient constraint prevents firing. <see cref="AimKinds"/> is the loaded
/// catalog's public damage-target vocabulary, identical for every contact and never derived from either ship's
/// installations.
/// </summary>
public sealed record CombatTargetProjection(
    SensorContactId ContactId,
    DistanceKilometers? Range,
    FireDirectedEnergyOutcome Outcome,
    IReadOnlyList<ShipSystemKind> AimKinds
);
