namespace AlterCourse.Core.Ships;

/// <summary>
/// An immutable ship design: identity, design label, and the default initial loadout for new ships.
/// </summary>
/// <remarks>
/// Equipment tuning (speed, sensing, generation, demands, repair timing, weapon facts) lives on the referenced
/// system definitions, not here. The loadout is only a new-ship default: a live ship owns its actual installations,
/// and loading a save never reads, overlays, or replenishes this loadout.
/// </remarks>
public sealed record ShipDefinition
{
    /// <summary>Maximum design display-name length.</summary>
    public const int MaximumDesignDisplayNameLength = 64;

    /// <summary>Initializes a validated design.</summary>
    public ShipDefinition(ShipDefinitionId id, string designDisplayName, ShipLoadoutDefinition initialLoadout)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("Ship definition requires an initialized identity.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(designDisplayName);
        if (designDisplayName.Length > MaximumDesignDisplayNameLength)
        {
            throw new ArgumentException(
                $"Design display name cannot exceed {MaximumDesignDisplayNameLength} characters.",
                nameof(designDisplayName)
            );
        }

        ArgumentNullException.ThrowIfNull(initialLoadout);
        Id = id;
        DesignDisplayName = designDisplayName;
        InitialLoadout = initialLoadout;
    }

    /// <summary>Gets the stable design identity.</summary>
    public ShipDefinitionId Id { get; }

    /// <summary>Gets the design display label.</summary>
    public string DesignDisplayName { get; }

    /// <summary>Gets the default initial loadout used when a start omits an explicit loadout.</summary>
    public ShipLoadoutDefinition InitialLoadout { get; }
}
