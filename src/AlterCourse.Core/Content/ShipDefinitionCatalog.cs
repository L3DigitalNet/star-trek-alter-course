using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Content;

/// <summary>Immutable ship designs resolved against one system-definition catalog.</summary>
/// <remarks>
/// A ship catalog carries the system catalog its loadouts were resolved against, so the pair cannot be supplied
/// mismatched to bootstrap, restore, or persistence. Every design's loadout references only definitions present
/// in <see cref="SystemDefinitions"/>.
/// </remarks>
public sealed class ShipDefinitionCatalog
{
    private readonly IReadOnlyDictionary<ShipDefinitionId, ShipDefinition> _definitions;

    internal ShipDefinitionCatalog(
        Dictionary<ShipDefinitionId, ShipDefinition> definitions,
        SystemDefinitionCatalog systemDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(systemDefinitions);
        foreach (ShipDefinition definition in definitions.Values)
        {
            foreach (InitialInstalledSystem system in definition.InitialLoadout.Systems)
            {
                if (!systemDefinitions.TryGet(system.DefinitionId, out _))
                {
                    throw new ArgumentException(
                        $"Ship definition '{definition.Id.Value}' references unknown system definition "
                            + $"'{system.DefinitionId.Value}'.",
                        nameof(definitions)
                    );
                }
            }
        }

        _definitions = new Dictionary<ShipDefinitionId, ShipDefinition>(definitions);
        SystemDefinitions = systemDefinitions;
    }

    /// <summary>Gets designs in ordinal identity order.</summary>
    public IReadOnlyCollection<ShipDefinition> Definitions =>
        _definitions.Values.OrderBy(definition => definition.Id.Value, StringComparer.Ordinal).ToArray();

    /// <summary>Gets the system-definition catalog every loadout was resolved against.</summary>
    public SystemDefinitionCatalog SystemDefinitions { get; }

    /// <summary>Gets one design that must exist.</summary>
    public ShipDefinition GetRequired(ShipDefinitionId id) =>
        _definitions.TryGetValue(id, out ShipDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"No ship definition exists with identity '{id.Value}'.");

    /// <summary>Gets one design when present.</summary>
    public bool TryGet(ShipDefinitionId id, out ShipDefinition? definition) =>
        _definitions.TryGetValue(id, out definition);
}
