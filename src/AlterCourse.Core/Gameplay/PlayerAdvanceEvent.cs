using AlterCourse.Core.Sensors;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Describes one player-safe advancement event and its optional observer-local contact.</summary>
public sealed record PlayerAdvanceEvent(
    PlayerAdvanceEventKind Kind,
    SimulationTime OccurredAt,
    SensorContactId? SensorContactId = null
);
