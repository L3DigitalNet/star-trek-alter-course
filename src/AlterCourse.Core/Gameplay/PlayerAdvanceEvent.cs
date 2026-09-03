using AlterCourse.Core.Sensors;

namespace AlterCourse.Core.Gameplay;

/// <summary>Describes one player-safe advancement event and its optional observer-local contact.</summary>
public sealed record PlayerAdvanceEvent(PlayerAdvanceEventKind Kind, SensorContactId? SensorContactId = null);
