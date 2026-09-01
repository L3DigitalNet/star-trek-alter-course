using System.Net.Http.Json;
using System.Text.Json;

namespace AlterCourse.AssetCtl.Providers;

internal static class ProviderAdapters
{
    public sealed class RecraftImageAdapter(HttpClient client)
        : HttpProviderBase(client, EndpointHosts),
            IAssetGenerator
    {
        private static readonly IReadOnlySet<AssetCapability> Capabilities = new HashSet<AssetCapability>
        {
            AssetCapability.RasterGenerate,
            AssetCapability.VectorGenerate,
            AssetCapability.ImageEdit,
            AssetCapability.ImageReferenceInput,
            AssetCapability.ImageTransparentOutput,
            AssetCapability.ImageBackgroundRemove,
            AssetCapability.ImageVectorize,
        };

        public string AdapterId => "recraft-images";

        public IReadOnlySet<AssetCapability> SupportedCapabilities => Capabilities;

        private static readonly IReadOnlySet<string> EndpointHosts = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "external.api.recraft.ai",
        };

        public IReadOnlySet<string> AllowedEndpointHosts => EndpointHosts;

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) =>
            ValidateKnown(options, "style", "substyle", "response_format");

        public async Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        )
        {
            ValidateOptions(context.Model.Options);
            global::System.Uri endpoint = Endpoint(context.Provider.Endpoint!, "images/generations");
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(
                    new
                    {
                        model = context.Model.VendorModel,
                        prompt = request.Prompt,
                        n = request.CandidateCount,
                        response_format = "b64_json",
                    }
                ),
            };
            using global::System.Text.Json.JsonDocument response = await SendJsonAsync(
                    message,
                    context,
                    cancellationToken
                )
                .ConfigureAwait(false);
            global::System.Collections.Generic.IReadOnlyList<global::AlterCourse.AssetCtl.Domain.DomainModels.GeneratedCandidate> candidates =
                await ParseImageDataAsync(response.RootElement, context, cancellationToken).ConfigureAwait(false);
            return new GenerationBatchResult(candidates, RequestId(response.RootElement), null);
        }

        internal static void ValidateKnown(IReadOnlyDictionary<string, string> options, params string[] known)
        {
            var allowed = new HashSet<string>(known, StringComparer.Ordinal);
            string? unknown = options.Keys.FirstOrDefault(key => !allowed.Contains(key));
            if (unknown is not null)
            {
                throw new ProviderException(
                    ProviderErrorCategory.InvalidRequest,
                    $"Unknown provider option '{unknown}'."
                );
            }
        }

        internal static string? RequestId(JsonElement root) =>
            root.TryGetProperty("id", out JsonElement id) ? id.GetString() : null;

        internal static Uri Endpoint(Uri endpoint, string path) => new(endpoint.AbsoluteUri.TrimEnd('/') + "/" + path);
    }

    public sealed class OpenAiImageAdapter(HttpClient client) : HttpProviderBase(client, EndpointHosts), IAssetGenerator
    {
        private static readonly IReadOnlySet<AssetCapability> Capabilities = new HashSet<AssetCapability>
        {
            AssetCapability.RasterGenerate,
            AssetCapability.ImageEdit,
            AssetCapability.ImageReferenceInput,
            AssetCapability.ImageTransparentOutput,
        };

        public string AdapterId => "openai-images";

        public IReadOnlySet<AssetCapability> SupportedCapabilities => Capabilities;

        private static readonly IReadOnlySet<string> EndpointHosts = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "api.openai.com",
        };

        public IReadOnlySet<string> AllowedEndpointHosts => EndpointHosts;

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) =>
            RecraftImageAdapter.ValidateKnown(options, "quality", "background", "output_compression");

        public async Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        )
        {
            ValidateOptions(context.Model.Options);
            bool editing = request.References.Count != 0;
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                RecraftImageAdapter.Endpoint(
                    context.Provider.Endpoint!,
                    editing ? "images/edits" : "images/generations"
                )
            );
            if (editing)
            {
                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(context.Model.VendorModel), "model");
                multipart.Add(new StringContent(request.Prompt), "prompt");
                multipart.Add(
                    new StringContent(
                        request.CandidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    ),
                    "n"
                );
                foreach ((string FileName, string MediaType, byte[] Bytes) reference in request.References)
                {
                    var part = new ByteArrayContent(reference.Bytes);
                    part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(reference.MediaType);
                    multipart.Add(part, "image[]", reference.FileName);
                }

                message.Content = multipart;
            }
            else
            {
                message.Content = JsonContent.Create(
                    new
                    {
                        model = context.Model.VendorModel,
                        prompt = request.Prompt,
                        n = request.CandidateCount,
                        output_format = "png",
                    }
                );
            }

            using global::System.Text.Json.JsonDocument response = await SendJsonAsync(
                    message,
                    context,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return new GenerationBatchResult(
                await ParseImageDataAsync(response.RootElement, context, cancellationToken).ConfigureAwait(false),
                RecraftImageAdapter.RequestId(response.RootElement),
                null
            );
        }
    }

    public sealed class XaiImageAdapter(HttpClient client) : HttpProviderBase(client, EndpointHosts), IAssetGenerator
    {
        private static readonly IReadOnlySet<AssetCapability> Capabilities = new HashSet<AssetCapability>
        {
            AssetCapability.RasterGenerate,
            AssetCapability.ImageEdit,
            AssetCapability.ImageReferenceInput,
        };

        public string AdapterId => "xai-images";

        public IReadOnlySet<AssetCapability> SupportedCapabilities => Capabilities;

        private static readonly IReadOnlySet<string> EndpointHosts = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "api.x.ai",
        };

        public IReadOnlySet<string> AllowedEndpointHosts => EndpointHosts;

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) =>
            RecraftImageAdapter.ValidateKnown(options, "aspect_ratio", "resolution");

        public async Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        )
        {
            ValidateOptions(context.Model.Options);
            Dictionary<string, object?> payload = new(4, StringComparer.Ordinal)
            {
                ["model"] = context.Model.VendorModel,
                ["prompt"] = request.Prompt,
                ["n"] = request.CandidateCount,
                ["response_format"] = "b64_json",
            };
            if (request.References.Count != 0)
            {
                payload["image"] = request
                    .References.Select(reference => Convert.ToBase64String(reference.Bytes))
                    .ToArray();
            }

            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                RecraftImageAdapter.Endpoint(context.Provider.Endpoint!, "images/generations")
            )
            {
                Content = JsonContent.Create(payload),
            };
            using global::System.Text.Json.JsonDocument response = await SendJsonAsync(
                    message,
                    context,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return new GenerationBatchResult(
                await ParseImageDataAsync(response.RootElement, context, cancellationToken).ConfigureAwait(false),
                RecraftImageAdapter.RequestId(response.RootElement),
                null
            );
        }
    }
}
