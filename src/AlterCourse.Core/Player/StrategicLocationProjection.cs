
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Player;

/// <summary>Projects one player-known strategic location.</summary>
public sealed record StrategicLocationProjection
{
    internal StrategicLocationProjection(
        LocationId id,
        string displayName,
        StrategicMapPosition position
    ) => (Id, DisplayName, Position) = (id, displayName, position);

    /// <summary>Gets the stable location identity.</summary>
    public LocationId Id { get; }

    /// <summary>Gets the player-facing location name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the continuous strategic-map position.</summary>
    public StrategicMapPosition Position { get; }
}
