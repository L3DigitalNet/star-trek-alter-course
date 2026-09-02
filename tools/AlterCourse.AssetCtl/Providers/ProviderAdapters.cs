using System.Net.Http.Json;

namespace AlterCourse.AssetCtl.Providers;

internal static class ProviderAdapters
{
    public sealed class RecraftImageAdapter(HttpClient client) : HttpProviderBase(client), IAssetGenerator
    {
        private static readonly IReadOnlySet<AssetCapability> Capabilities = new HashSet<AssetCapability>
        {
            AssetCapability.RasterGenerate,
            AssetCapability.VectorGenerate,
            AssetCapability.ImageTransparentOutput,
            AssetCapability.ImageBackgroundRemove,
            AssetCapability.ImageVectorize,
        };

        public string AdapterId => "recraft-images";

        public bool RequiresNetwork => true;

        public IReadOnlySet<AssetCapability> SupportedCapabilities => Capabilities;

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) =>
            ValidateKnown(options, "style", "substyle", "response_format", "supported_sizes");

        public string? OutputContractRejection(ModelProfile model, OutputContract? output) =>
            ExactSizeRejection(model, output);

        public async Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        )
        {
            ValidateOptions(context.Model.Options);
            if (request.References.Count != 0)
            {
                throw new ProviderException(
                    ProviderErrorCategory.UnsupportedOutput,
                    "Recraft generation does not accept reference inputs."
                );
            }

            RequireExactSize(context.Model, request.Request.Output);
            global::System.Uri endpoint = Endpoint(context.Provider.Endpoint!, "images/generations");
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(
                    new
                    {
                        model = context.Model.VendorModel,
                        prompt = request.Prompt,
                        n = request.CandidateCount,
                        size = ExactSize(request.Request.Output),
                        response_format = "b64_json",
                    }
                ),
            };
            ProviderContracts.ImageResponse response = await SendJsonAsync<ProviderContracts.ImageResponse>(
                    message,
                    context,
                    cancellationToken
                )
                .ConfigureAwait(false);
            global::System.Collections.Generic.IReadOnlyList<global::AlterCourse.AssetCtl.Domain.DomainModels.GeneratedCandidate> candidates =
                await ParseImageDataAsync(response, context, request, cancellationToken).ConfigureAwait(false);
            return new GenerationBatchResult(
                candidates,
                ProviderRequestIdPolicy.Sanitize(response.Id, context.Credential),
                null
            );
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

        internal static Uri Endpoint(Uri endpoint, string path) => new(endpoint.AbsoluteUri.TrimEnd('/') + "/" + path);

        internal static string ExactSize(OutputContract output) => $"{output.Width}x{output.Height}";

        internal static string? ExactSizeRejection(ModelProfile model, OutputContract? output)
        {
            if (output is null)
            {
                return null;
            }

            string requested = ExactSize(output);
            if (
                !model.Options.TryGetValue("supported_sizes", out string? configured)
                || string.Equals(configured, "none", StringComparison.Ordinal)
                || !configured
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(requested, StringComparer.Ordinal)
            )
            {
                return requested;
            }

            return null;
        }

        internal static void RequireExactSize(ModelProfile model, OutputContract output)
        {
            string? rejection = ExactSizeRejection(model, output);
            if (rejection is not null)
            {
                throw new ProviderException(
                    ProviderErrorCategory.UnsupportedOutput,
                    $"Provider cannot guarantee exact output dimensions {rejection}."
                );
            }
        }
    }

    public sealed class OpenAiImageAdapter(HttpClient client) : HttpProviderBase(client), IAssetGenerator
    {
        private static readonly IReadOnlySet<AssetCapability> Capabilities = new HashSet<AssetCapability>
        {
            AssetCapability.RasterGenerate,
            AssetCapability.ImageEdit,
            AssetCapability.ImageReferenceInput,
            AssetCapability.ImageTransparentOutput,
        };

        public string AdapterId => "openai-images";

        public bool RequiresNetwork => true;

        public IReadOnlySet<AssetCapability> SupportedCapabilities => Capabilities;

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) =>
            RecraftImageAdapter.ValidateKnown(
                options,
                "quality",
                "background",
                "output_compression",
                "supported_sizes"
            );

        public string? OutputContractRejection(ModelProfile model, OutputContract? output) =>
            RecraftImageAdapter.ExactSizeRejection(model, output);

        public async Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        )
        {
            ValidateOptions(context.Model.Options);
            RecraftImageAdapter.RequireExactSize(context.Model, request.Request.Output);
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
                multipart.Add(new StringContent(RecraftImageAdapter.ExactSize(request.Request.Output)), "size");
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
                message.Content = JsonContent.Create(GenerationPayload(context, request));
            }

            ProviderContracts.ImageResponse response = await SendJsonAsync<ProviderContracts.ImageResponse>(
                    message,
                    context,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return new GenerationBatchResult(
                await ParseImageDataAsync(response, context, request, cancellationToken).ConfigureAwait(false),
                ProviderRequestIdPolicy.Sanitize(response.Id, context.Credential),
                null
            );
        }

        private static Dictionary<string, object?> GenerationPayload(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request
        )
        {
            Dictionary<string, object?> payload = new(7, StringComparer.Ordinal)
            {
                ["model"] = context.Model.VendorModel,
                ["prompt"] = request.Prompt,
                ["n"] = request.CandidateCount,
                ["size"] = RecraftImageAdapter.ExactSize(request.Request.Output),
                ["output_format"] = "png",
            };
            CopyOption(context.Model.Options, payload, "quality");
            if (request.Request.Output.TransparencyRequired)
            {
                payload["background"] = "transparent";
            }
            else
            {
                CopyOption(context.Model.Options, payload, "background");
            }

            return payload;
        }

        private static void CopyOption(
            IReadOnlyDictionary<string, string> options,
            Dictionary<string, object?> payload,
            string key
        )
        {
            if (options.TryGetValue(key, out string? value))
            {
                payload[key] = value;
            }
        }
    }

    public sealed class XaiImageAdapter(HttpClient client) : HttpProviderBase(client), IAssetGenerator
    {
        private static readonly IReadOnlySet<AssetCapability> Capabilities = new HashSet<AssetCapability>
        {
            AssetCapability.RasterGenerate,
            AssetCapability.ImageEdit,
            AssetCapability.ImageReferenceInput,
        };

        public string AdapterId => "xai-images";

        public bool RequiresNetwork => true;

        public IReadOnlySet<AssetCapability> SupportedCapabilities => Capabilities;

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) =>
            RecraftImageAdapter.ValidateKnown(options, "aspect_ratio", "resolution", "quality", "supported_sizes");

        public string? OutputContractRejection(ModelProfile model, OutputContract? output) =>
            RecraftImageAdapter.ExactSizeRejection(model, output);

        public async Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        )
        {
            ValidateOptions(context.Model.Options);
            RecraftImageAdapter.RequireExactSize(context.Model, request.Request.Output);
            Dictionary<string, object?> payload = new(4, StringComparer.Ordinal)
            {
                ["model"] = context.Model.VendorModel,
                ["prompt"] = request.Prompt,
                ["n"] = request.CandidateCount,
                ["response_format"] = "b64_json",
                ["aspect_ratio"] = AspectRatio(request.Request.Output),
            };
            if (context.Model.Options.TryGetValue("resolution", out string? resolution))
            {
                payload["resolution"] = resolution;
            }

            if (context.Model.Options.TryGetValue("quality", out string? quality))
            {
                payload["quality"] = quality;
            }

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
            ProviderContracts.ImageResponse response = await SendJsonAsync<ProviderContracts.ImageResponse>(
                    message,
                    context,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return new GenerationBatchResult(
                await ParseImageDataAsync(response, context, request, cancellationToken).ConfigureAwait(false),
                ProviderRequestIdPolicy.Sanitize(response.Id, context.Credential),
                null
            );
        }

        private static string AspectRatio(OutputContract output)
        {
            int divisor = GreatestCommonDivisor(output.Width, output.Height);
            return $"{output.Width / divisor}:{output.Height / divisor}";
        }

        private static int GreatestCommonDivisor(int left, int right)
        {
            while (right != 0)
            {
                (left, right) = (right, left % right);
            }

            return left;
        }
    }
}
