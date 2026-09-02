using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares a ship present at one strategic location.</summary>
public sealed record AtLocationStart(LocationId LocationId) : ShipStrategicStart;
