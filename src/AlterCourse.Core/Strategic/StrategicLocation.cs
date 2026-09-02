namespace AlterCourse.Core.Strategic;

/// <summary>Defines one neutral strategic location.</summary>
public sealed record StrategicLocation
{
    /// <summary>Initializes a strategic location.</summary>
    public StrategicLocation(LocationId id, string displayName, StrategicMapPosition position)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("Location requires an initialized identity.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Id = id;
        DisplayName = displayName;
        Position = position;
    }

    /// <summary>Gets the stable identity.</summary>
    public LocationId Id { get; }

    /// <summary>Gets the player-facing name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the continuous map position.</summary>
    public StrategicMapPosition Position { get; }
}
