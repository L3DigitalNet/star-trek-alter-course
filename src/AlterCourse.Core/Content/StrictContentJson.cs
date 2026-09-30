using System.Globalization;
using System.Text.Json;
using Json.Schema;

namespace AlterCourse.Core.Content;

/// <summary>
/// Strict, bounded JSON admission shared by authored-content loaders that report
/// <see cref="ShipContentDiagnostic"/>: duplicate-member rejection, a nesting-depth bound, no comments or trailing
/// commas, JSON Schema evaluation into sorted diagnostics, and checked integral reads.
/// </summary>
/// <remarks>
/// Intended as the single admission path for every content family that reports
/// <see cref="ShipContentDiagnostic"/>. The faction loader reports a different diagnostic type and does not consume
/// parsing or schema evaluation; its byte factories share only encoding-preamble admission.
/// </remarks>
internal static class StrictContentJson
{
    private static readonly EvaluationOptions SchemaEvaluationOptions = new()
    {
        OutputFormat = OutputFormat.List,
        Culture = CultureInfo.InvariantCulture,
    };

    /// <summary>Removes one initial UTF-8 encoding preamble from an already bounded byte document.</summary>
    internal static ReadOnlySpan<byte> WithoutUtf8Preamble(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith([0xEF, 0xBB, 0xBF]) ? bytes[3..] : bytes;

    /// <summary>
    /// Parses one document after rejecting duplicate members and nesting deeper than <paramref name="maxDepth"/>
    /// containers. Throws <see cref="ShipContentValidationException"/> (<c>json.invalid</c>,
    /// <c>json.duplicate-member</c>, or <c>json.too-deep</c>); the caller owns disposing the result.
    /// </summary>
    internal static JsonDocument Parse(ReadOnlyMemory<byte> utf8Json, string sourceIdentity, int maxDepth)
    {
        try
        {
            // Duplicate detection precedes JsonDocument construction because System.Text.Json otherwise keeps
            // duplicate object members, allowing schema evaluation and typed mapping to observe different values.
            Prescan(utf8Json.Span, sourceIdentity, maxDepth);
            return JsonDocument.Parse(
                utf8Json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = maxDepth,
                }
            );
        }
        catch (JsonException exception)
        {
            string location = exception.BytePositionInLine is long position
                ? $"byte:{position.ToString(CultureInfo.InvariantCulture)}"
                : "#";
            throw Failure("json.invalid", sourceIdentity, location, $"Invalid UTF-8 JSON: {exception.Message}");
        }
    }

    /// <summary>Evaluates a schema and returns deterministic, sorted diagnostics (empty when valid).</summary>
    internal static IReadOnlyList<ShipContentDiagnostic> EvaluateSchema(
        JsonSchema schema,
        JsonElement instance,
        string sourceIdentity
    )
    {
        EvaluationResults results = schema.Evaluate(instance, SchemaEvaluationOptions);
        if (results.IsValid)
        {
            return [];
        }

        return Flatten(results)
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
    }

    /// <summary>
    /// Reads a schema-validated integral number. JSON Schema admits integral forms such as <c>2e3</c> or
    /// <c>1e100</c>; values outside the target range yield <c>semantic.invalid-value</c> instead of throwing.
    /// </summary>
    internal static bool TryReadInt64(
        JsonElement value,
        string location,
        string sourceIdentity,
        List<ShipContentDiagnostic> diagnostics,
        out long result
    )
    {
        if (value.TryGetInt64(out result))
        {
            return true;
        }

        if (
            value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out decimal numeric)
            && decimal.Truncate(numeric) == numeric
            && numeric is >= long.MinValue and <= long.MaxValue
        )
        {
            result = decimal.ToInt64(numeric);
            return true;
        }

        diagnostics.Add(Semantic(sourceIdentity, location, "Value must be an integer representable in 64 bits."));
        result = 0;
        return false;
    }

    /// <summary>Reads a schema-validated integral number bounded to the 32-bit range.</summary>
    internal static bool TryReadInt32(
        JsonElement value,
        string location,
        string sourceIdentity,
        List<ShipContentDiagnostic> diagnostics,
        out int result
    )
    {
        if (TryReadInt64(value, location, sourceIdentity, diagnostics, out long wide))
        {
            if (wide is >= int.MinValue and <= int.MaxValue)
            {
                result = (int)wide;
                return true;
            }

            diagnostics.Add(Semantic(sourceIdentity, location, "Value must be an integer representable in 32 bits."));
        }

        result = 0;
        return false;
    }

    /// <summary>Reads a finite number; out-of-range literals such as <c>1e400</c> yield a diagnostic.</summary>
    internal static bool TryReadFiniteDouble(
        JsonElement value,
        string location,
        string sourceIdentity,
        List<ShipContentDiagnostic> diagnostics,
        out double result
    )
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out result) && double.IsFinite(result))
        {
            return true;
        }

        diagnostics.Add(Semantic(sourceIdentity, location, "Value must be a finite number."));
        result = 0;
        return false;
    }

    internal static ShipContentDiagnostic Semantic(string sourceIdentity, string location, string message) =>
        new("semantic.invalid-value", sourceIdentity, location, string.Empty, message);

    internal static ShipContentValidationException Failure(
        string code,
        string sourceIdentity,
        string instanceLocation,
        string message
    ) => new([new ShipContentDiagnostic(code, sourceIdentity, instanceLocation, string.Empty, message)]);

    /// <summary>Sorts diagnostics into the deterministic order every content exception reports.</summary>
    internal static ShipContentDiagnostic[] Sort(IEnumerable<ShipContentDiagnostic> diagnostics) =>
        diagnostics
            .OrderBy(diagnostic => diagnostic.SourceIdentity, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.InstanceLocation, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.SchemaLocation, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();

    private static void Prescan(ReadOnlySpan<byte> utf8Json, string sourceIdentity, int maxDepth)
    {
        // The reader's own limit is one level looser than the admitted depth so this scan, not a generic reader
        // exception, observes the first over-deep container and can report the specific json.too-deep code.
        var reader = new Utf8JsonReader(
            utf8Json,
            new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = maxDepth + 1,
            }
        );
        var objectMembers = new Stack<HashSet<string>?>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    ValidateContainerDepth(objectMembers.Count, maxDepth, reader.TokenStartIndex, sourceIdentity);

                    // Arrays push a null frame so depth counts every container while member tracking stays
                    // scoped to the enclosing object.
                    objectMembers.Push(
                        reader.TokenType == JsonTokenType.StartObject
                            ? new HashSet<string>(StringComparer.Ordinal)
                            : null
                    );
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    objectMembers.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    string member = ReadValidatedString(ref reader);
                    if (!objectMembers.Peek()!.Add(member))
                    {
                        throw Failure(
                            "json.duplicate-member",
                            sourceIdentity,
                            $"byte:{reader.TokenStartIndex.ToString(CultureInfo.InvariantCulture)}",
                            $"Found duplicate JSON member '{member}'."
                        );
                    }

                    break;
                case JsonTokenType.String:
                    // Schema evaluation may decode string values itself. Reject malformed Unicode
                    // here so library decoder failures keep the content boundary's typed contract.
                    _ = ReadValidatedString(ref reader);
                    break;
            }
        }
    }

    internal static string ReadValidatedString(ref Utf8JsonReader reader)
    {
        try
        {
            return reader.GetString()!;
        }
        catch (InvalidOperationException exception)
        {
            // Callers restrict this operation to string/name tokens; this translates only the
            // decoder's malformed UTF-8/UTF-16 signal, not arbitrary schema or domain failures.
            throw new JsonException("A JSON string contains invalid Unicode.", exception);
        }
    }

    private static void ValidateContainerDepth(int depth, int maximum, long tokenStartIndex, string sourceIdentity)
    {
        if (depth >= maximum)
        {
            throw Failure(
                "json.too-deep",
                sourceIdentity,
                $"byte:{tokenStartIndex.ToString(CultureInfo.InvariantCulture)}",
                $"JSON nesting exceeds the {maximum.ToString(CultureInfo.InvariantCulture)}-level limit."
            );
        }
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
}
