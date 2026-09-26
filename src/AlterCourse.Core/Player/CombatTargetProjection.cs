using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Player;

/// <summary>Preserves supported choices even when a transient constraint prevents firing.</summary>
public sealed record CombatTargetProjection(
    SensorContactId ContactId,
    DistanceKilometers? Range,
    FireDirectedEnergyOutcome Outcome,
    IReadOnlyList<ShipSystemKind> SupportedSystems
);
