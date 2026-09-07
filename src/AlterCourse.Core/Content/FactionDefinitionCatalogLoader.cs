using System.Globalization;
using System.Text;
using System.Text.Json;
using AlterCourse.Core.Factions;
using Json.Schema;

namespace AlterCourse.Core.Content;

/// <summary>Strictly validates version-one authored faction JSON and constructs domain definitions.</summary>
public sealed class FactionDefinitionCatalogLoader
{
    /// <summary>Gets the maximum number of authored definitions admitted into one development catalog.</summary>
    public const int MaximumDefinitions = 256;

    private static readonly Uri SchemaBaseUri = new(
        "https://l3digital.net/star-trek-alter-course/schemas/faction-definition-v1.schema.json"
    );
    private static readonly EvaluationOptions SchemaEvaluationOptions = new()
    {
        OutputFormat = OutputFormat.List,
        Culture = CultureInfo.InvariantCulture,
    };
    private static readonly UTF8Encoding StrictUtf8Encoding = new(false, true);

    private readonly JsonSchema _schema;

    /// <summary>Initializes the loader from the canonical version-one JSON Schema text.</summary>
    public FactionDefinitionCatalogLoader(string schemaText)
    {
        ArgumentNullException.ThrowIfNull(schemaText);
        _schema = JsonSchema.FromText(
            schemaText,
            new BuildOptions { SchemaRegistry = new SchemaRegistry() },
            SchemaBaseUri
        );
    }

    /// <summary>Loads and validates one definition supplied as JSON text.</summary>
    public FactionDefinition LoadText(string json, string sourceIdentity) =>
        Load(FactionDefinitionContent.FromText(sourceIdentity, json));

    /// <summary>Loads and validates one definition supplied as UTF-8 JSON bytes.</summary>
    public FactionDefinition LoadUtf8(ReadOnlySpan<byte> utf8Json, string sourceIdentity) =>
        Load(FactionDefinitionContent.FromUtf8(sourceIdentity, utf8Json));

    /// <summary>Loads and validates one definition from the stream's current position.</summary>
    public FactionDefinition Load(Stream stream, string sourceIdentity) =>
        Load(FactionDefinitionContent.FromStream(sourceIdentity, stream));

    /// <summary>Loads a complete catalog and rejects identities repeated across source documents.</summary>
    public FactionDefinitionCatalog LoadCatalog(IEnumerable<FactionDefinitionContent> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        FactionDefinitionContent[] materialized = content.Take(MaximumDefinitions + 1).ToArray();
        if (materialized.Length > MaximumDefinitions)
        {
            throw new ArgumentException(
                $"A faction definition catalog supports at most {MaximumDefinitions} definitions.",
                nameof(content)
            );
        }

        var definitions = new Dictionary<FactionDefinitionId, FactionDefinition>();
        var identities = new Dictionary<FactionDefinitionId, string>();

        foreach (FactionDefinitionContent source in materialized)
        {
            FactionDefinition definition = Load(source);
            if (identities.TryGetValue(definition.Id, out string? earlierSource))
            {
                throw Failure(
                    "catalog.duplicate-id",
                    source.SourceIdentity,
                    "#/id",
                    string.Empty,
                    $"Faction definition identity '{definition.Id.Value}' duplicates the definition in '{earlierSource}'."
                );
            }

            identities.Add(definition.Id, source.SourceIdentity);
            definitions.Add(definition.Id, definition);
        }

        return new FactionDefinitionCatalog(definitions);
    }

    private FactionDefinition Load(FactionDefinitionContent content)
    {
        JsonDocument document = ParseStrict(content);
        using (document)
        {
            ValidateSchema(document.RootElement, content.SourceIdentity);
            AuthoredFactionDefinitionV1 authored = ReadAuthoredModel(document.RootElement);
            return ValidateSemantics(authored, content.SourceIdentity);
        }
    }

    private static JsonDocument ParseStrict(FactionDefinitionContent content)
    {
        try
        {
            StrictUtf8Encoding.GetCharCount(content.Utf8Json.Span);
            // Duplicate detection precedes JsonDocument construction because System.Text.Json otherwise keeps
            // duplicate object members, allowing schema evaluation and typed mapping to observe different values.
            DetectDuplicateMembers(content.Utf8Json.Span, content.SourceIdentity);
            return JsonDocument.Parse(content.Utf8Json);
        }
        catch (JsonException exception)
        {
            string location = exception.BytePositionInLine is long position
                ? $"byte:{position.ToString(CultureInfo.InvariantCulture)}"
                : "#";
            throw Failure(
                "json.invalid",
                content.SourceIdentity,
                location,
                string.Empty,
                $"Invalid UTF-8 JSON: {exception.Message}"
            );
        }
        catch (DecoderFallbackException exception)
        {
            throw Failure(
                "json.invalid",
                content.SourceIdentity,
                $"byte:{exception.Index.ToString(CultureInfo.InvariantCulture)}",
                string.Empty,
                $"Invalid UTF-8 JSON: {exception.Message}"
            );
        }
    }

    private static void DetectDuplicateMembers(ReadOnlySpan<byte> utf8Json, string sourceIdentity)
    {
        var reader = new Utf8JsonReader(
            utf8Json,
            new JsonReaderOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow }
        );
        var objectMembers = new Stack<HashSet<string>>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    objectMembers.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.EndObject:
                    objectMembers.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    string member = reader.GetString()!;
                    if (!objectMembers.Peek().Add(member))
                    {
                        throw Failure(
                            "json.duplicate-member",
                            sourceIdentity,
                            $"byte:{reader.TokenStartIndex.ToString(CultureInfo.InvariantCulture)}",
                            string.Empty,
                            $"Found duplicate JSON member '{member}'."
                        );
                    }

                    break;
            }
        }
    }

    private void ValidateSchema(JsonElement instance, string sourceIdentity)
    {
        EvaluationResults results = _schema.Evaluate(instance, SchemaEvaluationOptions);
        if (results.IsValid)
        {
            return;
        }

        FactionContentDiagnostic[] diagnostics = Flatten(results)
            .Where(result => !result.IsValid && result.Errors is { Count: > 0 })
            .SelectMany(result =>
                result
                    .Errors!.OrderBy(error => error.Key, StringComparer.Ordinal)
                    .Select(error => new FactionContentDiagnostic(
                        "schema." + error.Key,
                        sourceIdentity,
                        Location(result.InstanceLocation.ToString()),
                        result.SchemaLocation.ToString(),
                        error.Value
                    ))
            )
            .OrderBy(diagnostic => diagnostic.InstanceLocation, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.SchemaLocation, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();

        throw new FactionContentValidationException(diagnostics);
    }

    private static IEnumerable<EvaluationResults> Flatten(EvaluationResults result)
    {
        yield return result;
        foreach (EvaluationResults detail in result.Details ?? [])
        {
            foreach (EvaluationResults descendant in Flatten(detail))
            {
                yield return descendant;
            }
        }
    }

    private static string Location(string pointer) => string.IsNullOrEmpty(pointer) ? "#" : "#" + pointer;

    private static AuthoredFactionDefinitionV1 ReadAuthoredModel(JsonElement root) =>
        new(root.GetProperty("id").GetString()!, root.GetProperty("displayName").GetString()!);

    private static FactionDefinition ValidateSemantics(AuthoredFactionDefinitionV1 authored, string sourceIdentity)
    {
        var diagnostics = new List<FactionContentDiagnostic>();
        FactionDefinitionId id = ValidateIdentity(authored.Id, sourceIdentity, diagnostics);
        ValidateDisplayName(authored.DisplayName, sourceIdentity, diagnostics);

        if (diagnostics.Count > 0)
        {
            throw new FactionContentValidationException(diagnostics);
        }

        return new FactionDefinition(id, authored.DisplayName);
    }

    private static FactionDefinitionId ValidateIdentity(
        string authoredId,
        string sourceIdentity,
        List<FactionContentDiagnostic> diagnostics
    )
    {
        if (string.IsNullOrWhiteSpace(authoredId))
        {
            diagnostics.Add(
                Semantic(sourceIdentity, "#/id", "Faction definition identity must contain non-whitespace text.")
            );
            return default;
        }

        return new FactionDefinitionId(authoredId);
    }

    private static void ValidateDisplayName(
        string displayName,
        string sourceIdentity,
        List<FactionContentDiagnostic> diagnostics
    )
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            diagnostics.Add(
                Semantic(sourceIdentity, "#/displayName", "Faction display name must contain non-whitespace text.")
            );
        }
        else if (displayName.Length > FactionDefinition.MaximumDisplayNameLength)
        {
            diagnostics.Add(
                Semantic(
                    sourceIdentity,
                    "#/displayName",
                    $"Faction display name cannot exceed {FactionDefinition.MaximumDisplayNameLength} characters."
                )
            );
        }
    }

    private static FactionContentDiagnostic Semantic(string sourceIdentity, string location, string message) =>
        new("semantic.invalid-value", sourceIdentity, location, string.Empty, message);

    private static FactionContentValidationException Failure(
        string code,
        string sourceIdentity,
        string instanceLocation,
        string schemaLocation,
        string message
    ) => new([new FactionContentDiagnostic(code, sourceIdentity, instanceLocation, schemaLocation, message)]);

    private sealed record AuthoredFactionDefinitionV1(string Id, string DisplayName);
}
