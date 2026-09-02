using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Content;

/// <summary>Provides validated ship definitions indexed by their stable domain identity.</summary>
public sealed class ShipDefinitionCatalog
{
    private readonly IReadOnlyDictionary<ShipDefinitionId, ShipDefinition> _definitions;

    internal ShipDefinitionCatalog(Dictionary<ShipDefinitionId, ShipDefinition> definitions)
    {
        _definitions = new Dictionary<ShipDefinitionId, ShipDefinition>(definitions);
    }

    /// <summary>Gets all validated definitions in stable identity order.</summary>
    public IReadOnlyCollection<ShipDefinition> Definitions =>
        _definitions.Values.OrderBy(definition => definition.Id.Value, StringComparer.Ordinal).ToArray();

    /// <summary>Gets the definition with the required stable identity.</summary>
    public ShipDefinition GetRequired(ShipDefinitionId id) =>
        _definitions.TryGetValue(id, out ShipDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"No ship definition exists with identity '{id.Value}'.");
}
