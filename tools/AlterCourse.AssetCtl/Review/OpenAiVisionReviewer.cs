using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlterCourse.AssetCtl.Providers;

namespace AlterCourse.AssetCtl.Review;

internal sealed class OpenAiVisionReviewer(HttpClient client) : HttpProviderBase(client, EndpointHosts), IAssetReviewer
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        MaxDepth = 8,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly IReadOnlySet<string> EndpointHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "api.openai.com",
    };

    public static class SemanticReviewSchema
    {
        public const string Json = """
            {
              "type":"object",
              "additionalProperties":false,
              "required":["matches_subject","required_constraints_satisfied","prohibited_content_absent","readable_at_target_sizes","style_adherence","semantic_clarity","visual_defects","unrequested_text_detected","logo_or_watermark_detected","overall_score","decision"],
              "properties":{
                "matches_subject":{"type":"boolean"},
                "required_constraints_satisfied":{"type":"boolean"},
                "prohibited_content_absent":{"type":"boolean"},
                "readable_at_target_sizes":{"type":"boolean"},
                "style_adherence":{"type":"number","minimum":0,"maximum":1},
                "semantic_clarity":{"type":"number","minimum":0,"maximum":1},
                "visual_defects":{"type":"array","items":{"type":"string"},"maxItems":20},
                "unrequested_text_detected":{"type":"boolean"},
                "logo_or_watermark_detected":{"type":"boolean"},
                "overall_score":{"type":"number","minimum":0,"maximum":1},
                "decision":{"type":"string","enum":["pass","fail"]}
              }
            }
            """;
    }

    private static readonly IReadOnlySet<AssetCapability> Capabilities = new HashSet<AssetCapability>
    {
        AssetCapability.ReviewSemantic,
        AssetCapability.ReviewReferenceComparison,
    };

    public string AdapterId => "openai-vision-review";

    public IReadOnlySet<AssetCapability> SupportedCapabilities => Capabilities;

    public IReadOnlySet<string> AllowedEndpointHosts => EndpointHosts;

    public void ValidateOptions(IReadOnlyDictionary<string, string> options) =>
        RecraftImageAdapter.ValidateKnown(options, "reasoning_effort");

    public async Task<SemanticReviewResult> ReviewAsync(
        ProviderExecutionContext context,
        SemanticReviewRequest request,
        CancellationToken cancellationToken
    )
    {
        ValidateOptions(context.Model.Options);
        IEnumerable<string> required = (request.StyleRequired ?? []).Concat(request.Request.Required);
        IEnumerable<string> prohibited = (request.StyleProhibited ?? []).Concat(request.Request.Prohibited);
        string prompt =
            $"Review asset '{request.Request.Id}'. Purpose: {request.Request.Purpose}. Resolved style: {request.StyleSummary}. Required: {string.Join("; ", required)}. Prohibited: {string.Join("; ", prohibited)}. Return only the required structured rubric.";
        using HttpRequestMessage message = CreateRequest(context, request, prompt);
        ReviewerResponse response = await SendJsonAsync<ReviewerResponse>(message, context, cancellationToken)
            .ConfigureAwait(false);
        string? json = response.OutputText ?? StructuredChoice(response.Choices);

        return Parse(
            json
                ?? throw new ProviderException(
                    ProviderErrorCategory.MalformedResponse,
                    "Reviewer response omitted structured output."
                )
        );
    }

    private static string? StructuredChoice(ReviewerChoice[]? choices)
    {
        if (choices is null)
        {
            return null;
        }

        string? content = choices.Length == 1 ? choices[0]?.Message?.Content : null;
        if (content is null)
        {
            throw new ProviderException(
                ProviderErrorCategory.MalformedResponse,
                "Reviewer response must contain exactly one structured choice."
            );
        }

        return content;
    }

    private static HttpRequestMessage CreateRequest(
        ProviderExecutionContext context,
        SemanticReviewRequest request,
        string prompt
    )
    {
        var content = new List<object>
        {
            new { type = "input_text", text = prompt },
            new
            {
                type = "input_image",
                image_url = $"data:{request.MediaType};base64,{Convert.ToBase64String(request.Original)}",
            },
        };
        foreach ((int size, byte[] preview) in request.TargetPreviews.OrderBy(pair => pair.Key))
        {
            content.Add(new { type = "input_text", text = $"Target-size preview: {size}x{size} pixels" });
            content.Add(
                new { type = "input_image", image_url = $"data:image/png;base64,{Convert.ToBase64String(preview)}" }
            );
        }

        using var schema = JsonDocument.Parse(request.RubricJsonSchema);
        var payload = new
        {
            model = context.Model.VendorModel,
            input = new object[] { new { role = "user", content } },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "asset_review",
                    strict = true,
                    schema = schema.RootElement.Clone(),
                },
            },
        };
        return new HttpRequestMessage(
            HttpMethod.Post,
            RecraftImageAdapter.Endpoint(context.Provider.Endpoint!, "responses")
        )
        {
            Content = JsonContent.Create(payload),
        };
    }

    public static SemanticReviewResult Parse(string json)
    {
        try
        {
            SemanticReviewPayload payload =
                JsonSerializer.Deserialize<SemanticReviewPayload>(json, StrictJson)
                ?? throw new JsonException("semantic review was null");
            if (payload.Decision is not ("pass" or "fail"))
            {
                throw new JsonException("decision must be pass or fail");
            }

            if (payload.VisualDefects is null)
            {
                throw new JsonException("visual_defects is required");
            }

            if (payload.VisualDefects.Length > 20)
            {
                throw new JsonException("visual_defects must contain at most 20 items");
            }

            if (payload.VisualDefects.Any(static value => value is null))
            {
                throw new JsonException("visual_defects entries must be strings");
            }

            return new SemanticReviewResult(
                payload.MatchesSubject,
                payload.RequiredConstraintsSatisfied,
                payload.ProhibitedContentAbsent,
                payload.ReadableAtTargetSizes,
                Score(payload.StyleAdherence, "style_adherence"),
                Score(payload.SemanticClarity, "semantic_clarity"),
                payload.VisualDefects.Select(static value => value!).ToArray(),
                payload.UnrequestedTextDetected,
                payload.LogoOrWatermarkDetected,
                Score(payload.OverallScore, "overall_score"),
                payload.Decision,
                "not-run"
            );
        }
        catch (Exception exception)
            when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ProviderException(
                ProviderErrorCategory.MalformedResponse,
                $"Invalid semantic review: {exception.Message}"
            );
        }
    }

    private static double Score(double value, string property) =>
        double.IsFinite(value) && value is >= 0 and <= 1
            ? value
            : throw new JsonException($"{property} must be within 0..1");

    private sealed class ReviewerResponse
    {
        [JsonPropertyName("output_text")]
        public string? OutputText { get; init; }

        [JsonPropertyName("choices")]
        public ReviewerChoice[]? Choices { get; init; }
    }

    private sealed class ReviewerChoice
    {
        [JsonPropertyName("message")]
        [JsonRequired]
        public ReviewerMessage? Message { get; init; }
    }

    private sealed class ReviewerMessage
    {
        [JsonPropertyName("content")]
        [JsonRequired]
        public string? Content { get; init; }
    }

    private sealed class SemanticReviewPayload
    {
        [JsonPropertyName("matches_subject"), JsonRequired]
        public bool MatchesSubject { get; init; }

        [JsonPropertyName("required_constraints_satisfied"), JsonRequired]
        public bool RequiredConstraintsSatisfied { get; init; }

        [JsonPropertyName("prohibited_content_absent"), JsonRequired]
        public bool ProhibitedContentAbsent { get; init; }

        [JsonPropertyName("readable_at_target_sizes"), JsonRequired]
        public bool ReadableAtTargetSizes { get; init; }

        [JsonPropertyName("style_adherence"), JsonRequired]
        public double StyleAdherence { get; init; }

        [JsonPropertyName("semantic_clarity"), JsonRequired]
        public double SemanticClarity { get; init; }

        [JsonPropertyName("visual_defects"), JsonRequired]
        public string?[]? VisualDefects { get; init; }

        [JsonPropertyName("unrequested_text_detected"), JsonRequired]
        public bool UnrequestedTextDetected { get; init; }

        [JsonPropertyName("logo_or_watermark_detected"), JsonRequired]
        public bool LogoOrWatermarkDetected { get; init; }

        [JsonPropertyName("overall_score"), JsonRequired]
        public double OverallScore { get; init; }

        [JsonPropertyName("decision"), JsonRequired]
        public required string Decision { get; init; }
    }
}
