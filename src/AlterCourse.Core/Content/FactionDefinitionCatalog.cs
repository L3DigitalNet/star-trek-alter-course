using AlterCourse.Core.Factions;

namespace AlterCourse.Core.Content;

/// <summary>Provides validated faction definitions indexed by their stable domain identity.</summary>
public sealed class FactionDefinitionCatalog
{
    private readonly IReadOnlyDictionary<FactionDefinitionId, FactionDefinition> _definitions;

    internal FactionDefinitionCatalog(Dictionary<FactionDefinitionId, FactionDefinition> definitions)
    {
        _definitions = new Dictionary<FactionDefinitionId, FactionDefinition>(definitions);
    }

    /// <summary>Gets an empty validated catalog.</summary>
    public static FactionDefinitionCatalog Empty { get; } = new([]);

    /// <summary>Gets all validated definitions in stable identity order.</summary>
    public IReadOnlyCollection<FactionDefinition> Definitions =>
        _definitions.Values.OrderBy(definition => definition.Id.Value, StringComparer.Ordinal).ToArray();

    /// <summary>Gets the definition with the required stable identity.</summary>
    public FactionDefinition GetRequired(FactionDefinitionId id) =>
        _definitions.TryGetValue(id, out FactionDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"No faction definition exists with identity '{id.Value}'.");
}
