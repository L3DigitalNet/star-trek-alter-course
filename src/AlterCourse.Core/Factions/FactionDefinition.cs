namespace AlterCourse.Core.Factions;

/// <summary>Defines the immutable authored identity and label shared by runtime faction state.</summary>
public sealed record FactionDefinition
{
    /// <summary>Gets the maximum persisted display-name length.</summary>
    public const int MaximumDisplayNameLength = 64;

    /// <summary>Initializes a minimal faction definition.</summary>
    public FactionDefinition(FactionDefinitionId id, string displayName)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("Faction definition requires an initialized identity.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (displayName.Length > MaximumDisplayNameLength)
        {
            throw new ArgumentException(
                $"Faction display name cannot exceed {MaximumDisplayNameLength} characters.",
                nameof(displayName)
            );
        }

        Id = id;
        DisplayName = displayName;
    }

    /// <summary>Gets the stable definition identity.</summary>
    public FactionDefinitionId Id { get; }

    /// <summary>Gets the reusable player-facing faction label.</summary>
    public string DisplayName { get; }
}
