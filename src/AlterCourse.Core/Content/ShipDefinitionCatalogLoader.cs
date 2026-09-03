using System.Globalization;
using System.Text.Json;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using Json.Schema;

namespace AlterCourse.Core.Content;

/// <summary>Strictly validates version-three authored ship JSON and constructs domain definitions.</summary>
public sealed class ShipDefinitionCatalogLoader
{
    /// <summary>Gets the maximum number of authored definitions admitted into one development catalog.</summary>
    public const int MaximumDefinitions = 256;

    private static readonly Uri SchemaBaseUri = new(
        "https://l3digital.net/star-trek-alter-course/schemas/ship-definition-v3.schema.json"
    );
    private static readonly EvaluationOptions SchemaEvaluationOptions = new()
    {
        OutputFormat = OutputFormat.List,
        Culture = CultureInfo.InvariantCulture,
    };

    private readonly JsonSchema _schema;

    /// <summary>Initializes the loader from the canonical version-three JSON Schema text.</summary>
    public ShipDefinitionCatalogLoader(string schemaText)
    {
        ArgumentNullException.ThrowIfNull(schemaText);
        _schema = JsonSchema.FromText(
            schemaText,
            new BuildOptions { SchemaRegistry = new SchemaRegistry() },
            SchemaBaseUri
        );
    }

    /// <summary>Loads and validates one definition supplied as JSON text.</summary>
    public ShipDefinition LoadText(string json, string sourceIdentity) =>
        Load(ShipDefinitionContent.FromText(sourceIdentity, json));

    /// <summary>Loads and validates one definition supplied as UTF-8 JSON bytes.</summary>
    public ShipDefinition LoadUtf8(ReadOnlySpan<byte> utf8Json, string sourceIdentity) =>
        Load(ShipDefinitionContent.FromUtf8(sourceIdentity, utf8Json));

    /// <summary>Loads and validates one definition from the stream's current position.</summary>
    public ShipDefinition Load(Stream stream, string sourceIdentity) =>
        Load(ShipDefinitionContent.FromStream(sourceIdentity, stream));

    /// <summary>Loads a complete catalog and rejects identities repeated across source documents.</summary>
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
                throw Failure(
                    "catalog.duplicate-id",
                    source.SourceIdentity,
                    "#/id",
                    string.Empty,
                    $"Ship definition identity '{definition.Id.Value}' duplicates the definition in '{earlierSource}'."
                );
            }

            identities.Add(definition.Id, source.SourceIdentity);
            definitions.Add(definition.Id, definition);
        }

        return new ShipDefinitionCatalog(definitions);
    }

    private ShipDefinition Load(ShipDefinitionContent content)
    {
        JsonDocument document = ParseStrict(content);
        using (document)
        {
            ValidateSchema(document.RootElement, content.SourceIdentity);
            AuthoredShipDefinitionV3 authored = ReadAuthoredModel(document.RootElement, content.SourceIdentity);
            return ValidateSemantics(authored, content.SourceIdentity);
        }
    }

    private static JsonDocument ParseStrict(ShipDefinitionContent content)
    {
        try
        {
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

        ShipContentDiagnostic[] diagnostics = Flatten(results)
            .Where(result => !result.IsValid && result.Errors is { Count: > 0 })
            .SelectMany(result =>
                result
                    .Errors!.OrderBy(error => error.Key, StringComparer.Ordinal)
                    .Select(error => new ShipContentDiagnostic(
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

        throw new ShipContentValidationException(diagnostics);
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

    private static AuthoredShipDefinitionV3 ReadAuthoredModel(JsonElement root, string sourceIdentity) =>
        new(
            ReadInt32(root, "schemaVersion", sourceIdentity),
            root.GetProperty("id").GetString()!,
            root.GetProperty("designDisplayName").GetString()!,
            root.GetProperty("maximumTacticalSpeedKilometersPerSecond").GetDouble(),
            root.GetProperty("passiveSensorRangeKilometers").GetDouble(),
            ReadInt64(root, "activeScanDurationMilliseconds", sourceIdentity),
            ReadInt64(root, "sensorRepairDurationMilliseconds", sourceIdentity)
        );

    private static int ReadInt32(JsonElement root, string propertyName, string sourceIdentity)
    {
        JsonElement value = root.GetProperty(propertyName);
        if (value.TryGetInt32(out int integer))
        {
            return integer;
        }

        if (
            value.TryGetDecimal(out decimal numeric)
            && decimal.Truncate(numeric) == numeric
            && numeric is >= int.MinValue and <= int.MaxValue
        )
        {
            return decimal.ToInt32(numeric);
        }

        throw UnmappableInteger(propertyName, sourceIdentity, "a 32-bit integer");
    }

    private static long ReadInt64(JsonElement root, string propertyName, string sourceIdentity)
    {
        JsonElement value = root.GetProperty(propertyName);
        if (value.TryGetInt64(out long integer))
        {
            return integer;
        }

        if (
            value.TryGetDecimal(out decimal numeric)
            && decimal.Truncate(numeric) == numeric
            && numeric is >= long.MinValue and <= long.MaxValue
        )
        {
            return decimal.ToInt64(numeric);
        }

        throw UnmappableInteger(propertyName, sourceIdentity, "a 64-bit integer");
    }

    private static ShipContentValidationException UnmappableInteger(
        string propertyName,
        string sourceIdentity,
        string expectedType
    ) =>
        Failure(
            "semantic.invalid-value",
            sourceIdentity,
            $"#/{propertyName}",
            string.Empty,
            $"'{propertyName}' must be representable as {expectedType}."
        );

    private static ShipDefinition ValidateSemantics(AuthoredShipDefinitionV3 authored, string sourceIdentity)
    {
        var diagnostics = new List<ShipContentDiagnostic>();
        ShipDefinitionId id = ValidateIdentity(authored.Id, sourceIdentity, diagnostics);
        ValidateDesignDisplayName(authored.DesignDisplayName, sourceIdentity, diagnostics);
        ValidateSpeed(authored.MaximumTacticalSpeedKilometersPerSecond, sourceIdentity, diagnostics);
        ValidatePassiveSensorRange(authored.PassiveSensorRangeKilometers, sourceIdentity, diagnostics);
        ValidateDuration(
            authored.ActiveScanDurationMilliseconds,
            "activeScanDurationMilliseconds",
            "Active scan duration",
            sourceIdentity,
            diagnostics
        );
        ValidateRepairDuration(authored.SensorRepairDurationMilliseconds, sourceIdentity, diagnostics);

        if (diagnostics.Count > 0)
        {
            throw new ShipContentValidationException(diagnostics);
        }

        return new ShipDefinition(
            id,
            authored.DesignDisplayName,
            new SpeedKilometersPerSecond(authored.MaximumTacticalSpeedKilometersPerSecond),
            new DistanceKilometers(authored.PassiveSensorRangeKilometers),
            new SimulationDuration(authored.ActiveScanDurationMilliseconds),
            new SimulationDuration(authored.SensorRepairDurationMilliseconds)
        );
    }

    private static ShipDefinitionId ValidateIdentity(
        string authoredId,
        string sourceIdentity,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        if (string.IsNullOrWhiteSpace(authoredId))
        {
            diagnostics.Add(
                Semantic(sourceIdentity, "#/id", "Ship definition identity must contain non-whitespace text.")
            );
            return default;
        }

        return new ShipDefinitionId(authoredId);
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
                Semantic(sourceIdentity, "#/designDisplayName", "Design display name must contain non-whitespace text.")
            );
        }
        else if (designDisplayName.Length > ShipDefinition.MaximumDesignDisplayNameLength)
        {
            diagnostics.Add(
                Semantic(
                    sourceIdentity,
                    "#/designDisplayName",
                    $"Design display name cannot exceed {ShipDefinition.MaximumDesignDisplayNameLength} characters."
                )
            );
        }
    }

    private static void ValidateSpeed(double speed, string sourceIdentity, List<ShipContentDiagnostic> diagnostics)
    {
        if (!double.IsFinite(speed) || speed < 0)
        {
            diagnostics.Add(
                Semantic(
                    sourceIdentity,
                    "#/maximumTacticalSpeedKilometersPerSecond",
                    "Maximum tactical speed must be finite and nonnegative."
                )
            );
        }
    }

    private static void ValidatePassiveSensorRange(
        double range,
        string sourceIdentity,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        if (!double.IsFinite(range) || range < 0)
        {
            diagnostics.Add(
                Semantic(
                    sourceIdentity,
                    "#/passiveSensorRangeKilometers",
                    "Passive sensor range must be finite and nonnegative."
                )
            );
        }
    }

    private static void ValidateDuration(
        long milliseconds,
        string propertyName,
        string displayName,
        string sourceIdentity,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        if (milliseconds <= 0 || milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            diagnostics.Add(
                Semantic(
                    sourceIdentity,
                    $"#/{propertyName}",
                    $"{displayName} must be positive and align to the 100 millisecond simulation step."
                )
            );
        }
    }

    private static void ValidateRepairDuration(
        long milliseconds,
        string sourceIdentity,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        ValidateDuration(
            milliseconds,
            "sensorRepairDurationMilliseconds",
            "Sensor repair duration",
            sourceIdentity,
            diagnostics
        );
    }

    private static ShipContentDiagnostic Semantic(string sourceIdentity, string location, string message) =>
        new("semantic.invalid-value", sourceIdentity, location, string.Empty, message);

    private static ShipContentValidationException Failure(
        string code,
        string sourceIdentity,
        string instanceLocation,
        string schemaLocation,
        string message
    ) => new([new ShipContentDiagnostic(code, sourceIdentity, instanceLocation, schemaLocation, message)]);

    private sealed record AuthoredShipDefinitionV3(
        int SchemaVersion,
        string Id,
        string DesignDisplayName,
        double MaximumTacticalSpeedKilometersPerSecond,
        double PassiveSensorRangeKilometers,
        long ActiveScanDurationMilliseconds,
        long SensorRepairDurationMilliseconds
    );
}
