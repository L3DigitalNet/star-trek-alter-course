using System.Net.Http.Json;
using System.Text.Json;
using AlterCourse.AssetCtl.Providers;

namespace AlterCourse.AssetCtl.Review;

internal sealed class OpenAiVisionReviewer(HttpClient client) : HttpProviderBase(client, EndpointHosts), IAssetReviewer
{
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
        string prompt =
            $"Review asset '{request.Request.Id}'. Purpose: {request.Request.Purpose}. Required: {string.Join("; ", request.Request.Required)}. Prohibited: {string.Join("; ", request.Request.Prohibited)}. Return only the required structured rubric.";
        using HttpRequestMessage message = CreateRequest(context, request, prompt);
        using global::System.Text.Json.JsonDocument response = await SendJsonAsync(message, context, cancellationToken)
            .ConfigureAwait(false);
        string? json = null;
        if (response.RootElement.TryGetProperty("output_text", out JsonElement outputText))
        {
            json = outputText.GetString();
        }
        else if (
            response.RootElement.TryGetProperty("choices", out JsonElement choices)
            && choices.GetArrayLength() > 0
        )
        {
            json = choices[0].GetProperty("message").GetProperty("content").GetString();
        }

        return Parse(
            json
                ?? throw new ProviderException(
                    ProviderErrorCategory.MalformedResponse,
                    "Reviewer response omitted structured output."
                )
        );
    }

    private static HttpRequestMessage CreateRequest(
        ProviderExecutionContext context,
        SemanticReviewRequest request,
        string prompt
    )
    {
        var payload = new
        {
            model = context.Model.VendorModel,
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = prompt },
                        new
                        {
                            type = "input_image",
                            image_url = $"data:{request.MediaType};base64,{Convert.ToBase64String(request.Original)}",
                        },
                    },
                },
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "asset_review",
                    strict = true,
                    schema = JsonDocument.Parse(request.RubricJsonSchema).RootElement,
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
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            global::System.Text.Json.JsonElement root = document.RootElement;
            double style = Score(root, "style_adherence");
            double clarity = Score(root, "semantic_clarity");
            double overall = Score(root, "overall_score");
            string decision = RequiredString(root, "decision");
            if (decision is not ("pass" or "fail"))
            {
                throw new JsonException("decision must be pass or fail");
            }

            return new SemanticReviewResult(
                RequiredBoolean(root, "matches_subject"),
                RequiredBoolean(root, "required_constraints_satisfied"),
                RequiredBoolean(root, "prohibited_content_absent"),
                RequiredBoolean(root, "readable_at_target_sizes"),
                style,
                clarity,
                root.GetProperty("visual_defects")
                    .EnumerateArray()
                    .Select(value => value.GetString() ?? throw new JsonException("visual defect must be string"))
                    .ToArray(),
                RequiredBoolean(root, "unrequested_text_detected"),
                RequiredBoolean(root, "logo_or_watermark_detected"),
                overall,
                decision,
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

    private static double Score(JsonElement root, string property)
    {
        double result = root.GetProperty(property).GetDouble();
        return result is >= 0 and <= 1 ? result : throw new JsonException($"{property} must be within 0..1");
    }

    private static bool RequiredBoolean(JsonElement root, string property) => root.GetProperty(property).GetBoolean();

    private static string RequiredString(JsonElement root, string property) =>
        root.GetProperty(property).GetString() ?? throw new JsonException($"{property} is required");
}
