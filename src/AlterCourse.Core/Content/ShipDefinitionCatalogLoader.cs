using System.Globalization;
using System.Text.Json;
using AlterCourse.Core.Ships;
using Json.Schema;

namespace AlterCourse.Core.Content;

/// <summary>
/// Loads strict ship-definition V6 documents against an already loaded system-definition catalog.
/// </summary>
/// <remarks>
/// <para>
/// Order is load-bearing: the system catalog is complete before any ship document is read, so every
/// <c>initialLoadout</c> reference resolves against a fixed set and a missing definition is a diagnostic rather
/// than a partially materialized design. Earlier ship versions (V3–V5) keep their historical schemas but are not
/// accepted here; V5 is rejected with <c>schema.const</c> at <c>#/schemaVersion</c>.
/// </para>
/// <para>
/// Storage validation (identity range, duplicates, allocator continuation) runs first; per-kind cardinality is a
/// separate typed-admission step reported as <c>semantic.unsupported-cardinality</c>, matching the runtime split
/// between <see cref="InstalledSystemCollection"/> and <see cref="ShipSystemAdmission"/>.
/// </para>
/// </remarks>
public sealed class ShipDefinitionCatalogLoader
{
    /// <summary>Maximum ship definitions in one catalog.</summary>
    public const int MaximumDefinitions = 256;

    /// <summary>Maximum JSON nesting depth accepted in one ship document.</summary>
    public const int MaximumDepth = 8;

    private const int SchemaVersion = 6;

    private static readonly Uri SchemaBaseUri = new(
        "https://l3digital.net/star-trek-alter-course/schemas/ship-definition-v6.schema.json"
    );

    private readonly JsonSchema _schema;
    private readonly SystemDefinitionCatalog _systems;

    /// <summary>Initializes the loader from the V6 schema text and the resolved system-definition catalog.</summary>
    public ShipDefinitionCatalogLoader(string schemaText, SystemDefinitionCatalog systems)
    {
        ArgumentNullException.ThrowIfNull(schemaText);
        ArgumentNullException.ThrowIfNull(systems);
        _schema = JsonSchema.FromText(
            schemaText,
            new BuildOptions { SchemaRegistry = new SchemaRegistry() },
            SchemaBaseUri
        );
        _systems = systems;
    }

    /// <summary>Loads one definition from UTF-16 text.</summary>
    public ShipDefinition LoadText(string json, string sourceIdentity) =>
        Load(ShipDefinitionContent.FromText(sourceIdentity, json));

    /// <summary>Loads one definition from UTF-8 bytes.</summary>
    public ShipDefinition LoadUtf8(ReadOnlySpan<byte> utf8Json, string sourceIdentity) =>
        Load(ShipDefinitionContent.FromUtf8(sourceIdentity, utf8Json));

    /// <summary>Loads one definition from a bounded stream.</summary>
    public ShipDefinition Load(Stream stream, string sourceIdentity) =>
        Load(ShipDefinitionContent.FromStream(sourceIdentity, stream));

    /// <summary>Loads a catalog of uniquely identified designs resolved against the system catalog.</summary>
    public ShipDefinitionCatalog LoadCatalog(IEnumerable<ShipDefinitionContent> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ShipDefinitionContent[] materialized = content.Take(MaximumDefinitions + 1).ToArray();
        if (materialized.Length > MaximumDefinitions)
        {
            throw new ArgumentException(
                $"A ship definition catalog supports at most {MaximumDefinitions} definitions.",
                nameof(content)
            );
        }

        var definitions = new Dictionary<ShipDefinitionId, ShipDefinition>();
        var identities = new Dictionary<ShipDefinitionId, string>();
        foreach (ShipDefinitionContent source in materialized)
        {
            ShipDefinition definition = Load(source);
            if (identities.TryGetValue(definition.Id, out string? earlierSource))
            {
                throw StrictContentJson.Failure(
                    "catalog.duplicate-id",
                    source.SourceIdentity,
                    "#/id",
                    $"Ship definition identity '{definition.Id.Value}' duplicates the definition in '{earlierSource}'."
                );
            }

            identities.Add(definition.Id, source.SourceIdentity);
            definitions.Add(definition.Id, definition);
        }

        return new ShipDefinitionCatalog(definitions, _systems);
    }

    private ShipDefinition Load(ShipDefinitionContent content)
    {
        using JsonDocument document = StrictContentJson.Parse(content.Utf8Json, content.SourceIdentity, MaximumDepth);
        JsonElement root = document.RootElement;
        IReadOnlyList<ShipContentDiagnostic> schemaDiagnostics = StrictContentJson.EvaluateSchema(
            _schema,
            root,
            content.SourceIdentity
        );
        if (schemaDiagnostics.Count > 0)
        {
            throw new ShipContentValidationException(schemaDiagnostics);
        }

        // The schema const already pins the version; this guard keeps a relaxed schema from admitting V5 input.
        if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
        {
            throw StrictContentJson.Failure(
                "schema.const",
                content.SourceIdentity,
                "#/schemaVersion",
                "Only ship definition version six is supported."
            );
        }

        var diagnostics = new List<ShipContentDiagnostic>();
        string source = content.SourceIdentity;
        string authoredId = root.GetProperty("id").GetString()!;
        string displayName = root.GetProperty("designDisplayName").GetString()!;
        ShipDefinitionId id = ValidateIdentity(authoredId, source, diagnostics);
        ValidateDesignDisplayName(displayName, source, diagnostics);
        ShipLoadoutDefinition? loadout = SystemDefinitionLoadoutReader.Read(
            root.GetProperty("initialLoadout"),
            "#/initialLoadout",
            _systems,
            source,
            diagnostics
        );
        if (diagnostics.Count > 0 || loadout is null)
        {
            throw new ShipContentValidationException(StrictContentJson.Sort(diagnostics));
        }

        ValidateCardinality(loadout, source);
        return new ShipDefinition(id, displayName, loadout);
    }

    private void ValidateCardinality(ShipLoadoutDefinition loadout, string source)
    {
        var diagnostics = new List<ShipContentDiagnostic>();
        foreach (
            IGrouping<ShipSystemKind, (InitialInstalledSystem System, int Index)> group in loadout
                .Systems.Select((system, index) => (System: system, Index: index))
                .GroupBy(entry => _systems.GetRequired(entry.System.DefinitionId).Kind)
        )
        {
            (InitialInstalledSystem System, int Index)[] members = [.. group];
            foreach ((InitialInstalledSystem system, int index) in members.Skip(1))
            {
                diagnostics.Add(
                    new ShipContentDiagnostic(
                        "semantic.unsupported-cardinality",
                        source,
                        $"#/initialLoadout/systems/{index.ToString(CultureInfo.InvariantCulture)}",
                        string.Empty,
                        $"The current simulation admits at most one installed '{group.Key.Value}' system; installed "
                            + $"system {system.Id.Value.ToString(CultureInfo.InvariantCulture)} is another."
                    )
                );
            }
        }

        if (diagnostics.Count > 0)
        {
            throw new ShipContentValidationException(StrictContentJson.Sort(diagnostics));
        }
    }

    private static ShipDefinitionId ValidateIdentity(
        string authoredId,
        string sourceIdentity,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        try
        {
            return new ShipDefinitionId(authoredId);
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(StrictContentJson.Semantic(sourceIdentity, "#/id", exception.Message));
            return default;
        }
    }

    private static void ValidateDesignDisplayName(
        string designDisplayName,
        string sourceIdentity,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        if (string.IsNullOrWhiteSpace(designDisplayName))
        {
            diagnostics.Add(
                StrictContentJson.Semantic(
                    sourceIdentity,
                    "#/designDisplayName",
                    "Design display name must contain non-whitespace text."
                )
            );
        }
    }
}
