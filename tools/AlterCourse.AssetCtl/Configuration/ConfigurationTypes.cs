using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Json.Schema;
using YamlDotNet.RepresentationModel;
using OutputTransparency = AlterCourse.AssetCtl.Domain.DomainModels.OutputTransparency;
using RouteFallbackPolicy = AlterCourse.AssetCtl.Domain.DomainModels.RouteFallbackPolicy;
using RouteRetryPolicy = AlterCourse.AssetCtl.Domain.DomainModels.RouteRetryPolicy;
using SchemaDocumentStatus = AlterCourse.AssetCtl.Domain.DomainModels.SchemaDocumentStatus;
using SemanticReviewPolicy = AlterCourse.AssetCtl.Domain.DomainModels.SemanticReviewPolicy;

namespace AlterCourse.AssetCtl.Configuration;

internal static class ConfigurationTypes
{
    /// <summary>Loads and cross-validates tracked AssetCtl configuration without consulting environment values except for credential presence.</summary>
    public sealed class ConfigurationLoader(IReadOnlyDictionary<string, IAdapterDescriptor> adapters)
    {
        public EffectiveConfiguration Load(string repositoryRoot)
        {
            repositoryRoot = Path.GetFullPath(repositoryRoot);
            var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            global::YamlDotNet.RepresentationModel.YamlMappingNode root = Load(
                repositoryRoot,
                "config/assets/assetctl.yaml",
                hashes
            );
            root.RequireOnly("assetctl", "schema_version", "paths", "policy", "limits", "spending");
            RequireVersion(root, "assetctl");

            global::AlterCourse.AssetCtl.Domain.DomainModels.AssetCtlPaths paths = ReadPaths(
                root.Mapping("paths", "assetctl")
            );
            global::AlterCourse.AssetCtl.Domain.DomainModels.AssetCtlPolicy policy = ReadPolicy(
                root.Mapping("policy", "assetctl")
            );
            global::AlterCourse.AssetCtl.Domain.DomainModels.AssetCtlLimits limits = ReadLimits(
                root.Mapping("limits", "assetctl")
            );
            global::AlterCourse.AssetCtl.Domain.DomainModels.SpendingLimits spending = ReadSpending(
                root.Mapping("spending", "assetctl")
            );
            ApplyLocalOverride(repositoryRoot, hashes, ref policy, ref spending);
            ValidatePaths(repositoryRoot, paths);

            global::System.Collections.Generic.Dictionary<
                string,
                global::AlterCourse.AssetCtl.Domain.DomainModels.ProviderInstance
            > providers = ReadProviders(repositoryRoot, hashes, policy);
            (IReadOnlyList<RouteDefinition> routes, IReadOnlyList<RouteDefinition> reviewRoutes) = ReadRoutes(
                repositoryRoot,
                hashes,
                providers,
                limits
            );
            global::System.Collections.Generic.Dictionary<
                string,
                global::AlterCourse.AssetCtl.Domain.DomainModels.QualityTier
            > tiers = ReadQualityTiers(repositoryRoot, hashes, limits);
            global::System.Collections.Generic.Dictionary<
                string,
                global::AlterCourse.AssetCtl.Domain.DomainModels.StyleProfile
            > styles = ReadStyles(repositoryRoot, hashes, paths);
            string effectiveHash = Hash(string.Join('\n', hashes.Select(pair => $"{pair.Key}:{pair.Value}")));
            return new EffectiveConfiguration(
                repositoryRoot,
                paths,
                policy,
                limits,
                spending,
                EffectiveConfiguration.ReadOnly(providers),
                routes,
                reviewRoutes,
                EffectiveConfiguration.ReadOnly(tiers),
                EffectiveConfiguration.ReadOnly(styles),
                EffectiveConfiguration.ReadOnly(hashes),
                effectiveHash
            );
        }

        private static AssetCtlPaths ReadPaths(YamlMappingNode node)
        {
            node.RequireOnly(
                "paths",
                "godot_asset_root",
                "catalog_root",
                "style_root",
                "work_root",
                "receipt_root",
                "state_root",
                "log_root"
            );
            return new AssetCtlPaths(
                node.Scalar("godot_asset_root", "paths"),
                node.Scalar("catalog_root", "paths"),
                node.Scalar("style_root", "paths"),
                node.Scalar("work_root", "paths"),
                node.Scalar("receipt_root", "paths"),
                node.Scalar("state_root", "paths"),
                node.Scalar("log_root", "paths")
            );
        }

        private static AssetCtlPolicy ReadPolicy(YamlMappingNode node)
        {
            node.RequireOnly(
                "policy",
                "external_generation_enabled",
                "unknown_price",
                "protect_approved_assets",
                "local_placeholder_fallback",
                "require_https_endpoints",
                "allow_remote_reference_urls",
                "retain_unselected_candidates"
            );
            string unknownPrice = node.Scalar("unknown_price", "policy");
            if (unknownPrice is not ("reject" or "allow"))
            {
                throw new AssetCtlException("policy.unknown_price: expected reject or allow.", 2);
            }

            return new AssetCtlPolicy(
                node.Boolean("external_generation_enabled", "policy"),
                node.Boolean("protect_approved_assets", "policy"),
                node.Boolean("local_placeholder_fallback", "policy"),
                node.Boolean("require_https_endpoints", "policy"),
                node.Boolean("allow_remote_reference_urls", "policy"),
                unknownPrice,
                node.Boolean("retain_unselected_candidates", "policy")
            );
        }

        private static AssetCtlLimits ReadLimits(YamlMappingNode node)
        {
            node.RequireOnly(
                "limits",
                "maximum_download_bytes",
                "maximum_reference_bytes",
                "maximum_candidates_per_request",
                "maximum_total_attempts",
                "default_http_timeout_seconds",
                "maximum_http_timeout_seconds",
                "maximum_decoded_pixels"
            );
            var result = new AssetCtlLimits(
                node.Long("maximum_download_bytes", "limits"),
                node.Long("maximum_reference_bytes", "limits"),
                node.Integer("maximum_candidates_per_request", "limits"),
                node.Integer("maximum_total_attempts", "limits"),
                node.Integer("default_http_timeout_seconds", "limits"),
                node.Integer("maximum_http_timeout_seconds", "limits"),
                node.Long("maximum_decoded_pixels", "limits")
            );
            if (
                result.MaximumDownloadBytes <= 0
                || result.MaximumReferenceBytes <= 0
                || result.MaximumCandidatesPerRequest is < 1 or > 100
                || result.MaximumTotalAttempts is < 1 or > 100
                || result.DefaultHttpTimeoutSeconds <= 0
                || result.DefaultHttpTimeoutSeconds > result.MaximumHttpTimeoutSeconds
                || result.MaximumDecodedPixels <= 0
            )
            {
                throw new AssetCtlException("limits: values are outside safety bounds.", 2);
            }

            return result;
        }

        private static SpendingLimits ReadSpending(YamlMappingNode node)
        {
            node.RequireOnly(
                "spending",
                "maximum_estimated_cost_per_asset_usd",
                "maximum_estimated_cost_per_run_usd",
                "maximum_estimated_cost_per_day_usd"
            );
            var result = new SpendingLimits(
                node.Decimal("maximum_estimated_cost_per_asset_usd", "spending"),
                node.Decimal("maximum_estimated_cost_per_run_usd", "spending"),
                node.Decimal("maximum_estimated_cost_per_day_usd", "spending")
            );
            if (result.PerAssetUsd < 0 || result.PerRunUsd < 0 || result.PerDayUsd < 0)
            {
                throw new AssetCtlException("spending: limits cannot be negative.", 2);
            }

            return result;
        }

        private static void ApplyLocalOverride(
            string root,
            IDictionary<string, string> hashes,
            ref AssetCtlPolicy policy,
            ref SpendingLimits spending
        )
        {
            const string relative = ".assetctl/config.local.yaml";
            string path = PathPolicy.ResolveUnder(root, relative, relative, allowMissing: true);
            if (!File.Exists(path))
            {
                return;
            }

            global::YamlDotNet.RepresentationModel.YamlMappingNode document = Load(root, relative, hashes);
            document.RequireOnly("local override", "schema_version", "policy", "spending");
            RequireVersion(document, "local override");
            YamlMappingNode? policyNode = document.OptionalMapping("policy", "local override");
            if (policyNode is not null)
            {
                policy = ApplyPolicyOverride(policy, policyNode);
            }

            YamlMappingNode? spendingNode = document.OptionalMapping("spending", "local override");
            if (spendingNode is not null)
            {
                spending = ApplySpendingOverride(spending, spendingNode);
            }
        }

        private static AssetCtlPolicy ApplyPolicyOverride(AssetCtlPolicy policy, YamlMappingNode node)
        {
            node.RequireOnly("local override.policy", "external_generation_enabled", "local_placeholder_fallback");
            return policy with
            {
                ExternalGenerationEnabled =
                    node.OptionalBoolean("external_generation_enabled", "local override.policy")
                    ?? policy.ExternalGenerationEnabled,
                LocalPlaceholderFallback =
                    node.OptionalBoolean("local_placeholder_fallback", "local override.policy")
                    ?? policy.LocalPlaceholderFallback,
            };
        }

        private static SpendingLimits ApplySpendingOverride(SpendingLimits spending, YamlMappingNode node)
        {
            node.RequireOnly(
                "local override.spending",
                "maximum_estimated_cost_per_asset_usd",
                "maximum_estimated_cost_per_run_usd",
                "maximum_estimated_cost_per_day_usd"
            );
            SpendingLimits result = spending with
            {
                PerAssetUsd = OptionalDecimal(node, "maximum_estimated_cost_per_asset_usd", spending.PerAssetUsd),
                PerRunUsd = OptionalDecimal(node, "maximum_estimated_cost_per_run_usd", spending.PerRunUsd),
                PerDayUsd = OptionalDecimal(node, "maximum_estimated_cost_per_day_usd", spending.PerDayUsd),
            };
            if (result.PerAssetUsd < 0 || result.PerRunUsd < 0 || result.PerDayUsd < 0)
            {
                throw new AssetCtlException("local override spending limits cannot be negative.", 2);
            }

            return result;
        }

        private static decimal OptionalDecimal(YamlMappingNode node, string key, decimal fallback)
        {
            return node.OptionalDecimal(key, "local override.spending") ?? fallback;
        }

        private Dictionary<string, ProviderInstance> ReadProviders(
            string root,
            IDictionary<string, string> hashes,
            AssetCtlPolicy policy
        )
        {
            global::YamlDotNet.RepresentationModel.YamlMappingNode document = Load(
                root,
                "config/assets/providers.yaml",
                hashes
            );
            document.RequireOnly("providers", "schema_version", "providers");
            RequireVersion(document, "providers");
            global::YamlDotNet.RepresentationModel.YamlMappingNode providersNode = document.Mapping(
                "providers",
                "providers"
            );
            var providers = new Dictionary<string, ProviderInstance>(StringComparer.Ordinal);
            foreach (
                global::System.Collections.Generic.KeyValuePair<
                    global::YamlDotNet.RepresentationModel.YamlNode,
                    global::YamlDotNet.RepresentationModel.YamlNode
                > pair in providersNode.Children
            )
            {
                string id = pair.Key.AsScalar("providers key");
                if (!providers.TryAdd(id, ReadProvider(id, pair.Value.AsMapping($"providers.{id}"), policy)))
                {
                    throw new AssetCtlException($"providers.{id}: duplicate provider.", 2);
                }
            }

            return providers;
        }

        private ProviderInstance ReadProvider(string id, YamlMappingNode node, AssetCtlPolicy policy)
        {
            string path = $"providers.{id}";
            node.RequireOnly(
                path,
                "adapter",
                "enabled",
                "endpoint",
                "endpoint_hosts",
                "credentials",
                "downloads",
                "allowed_lifecycles",
                "models"
            );
            string adapterId = node.Scalar("adapter", path);
            if (!adapters.TryGetValue(adapterId, out IAdapterDescriptor? adapter))
            {
                throw new AssetCtlException($"{path}.adapter: unregistered adapter '{adapterId}'.", 2);
            }

            HashSet<string> endpointHosts = ReadEndpointHosts(node.OptionalSequence("endpoint_hosts", path), path);
            Uri? endpoint = ReadEndpoint(node.OptionalScalar("endpoint", path), endpointHosts, policy, path);

            string? credential = ReadCredential(node.OptionalMapping("credentials", path), path);
            if (adapter.RequiresNetwork && (endpoint is null || credential is null))
            {
                throw new AssetCtlException(
                    $"{path}: network adapters require both endpoint and credentials.environment_variable.",
                    2
                );
            }

            HashSet<string> allowedHosts = ReadAllowedHosts(node.OptionalMapping("downloads", path), path);
            Dictionary<string, ModelProfile> models = ReadModels(node.Mapping("models", path), path, adapter);
            IReadOnlySet<AssetLifecycle> allowedLifecycles = ReadAllowedLifecycles(node, path);

            return new ProviderInstance(
                id,
                adapterId,
                node.Boolean("enabled", path),
                endpoint,
                credential,
                allowedHosts,
                models,
                allowedLifecycles,
                endpointHosts
            );
        }

        private static Uri? ReadEndpoint(
            string? value,
            HashSet<string> endpointHosts,
            AssetCtlPolicy policy,
            string path
        )
        {
            if (value is null)
            {
                if (endpointHosts.Count != 0)
                {
                    throw new AssetCtlException(
                        $"{path}.endpoint_hosts: local providers cannot authorize remote hosts.",
                        2
                    );
                }

                return null;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint))
            {
                throw new AssetCtlException($"{path}.endpoint: invalid absolute URI.", 2);
            }

            if (
                policy.RequireHttpsEndpoints
                && !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            )
            {
                throw new AssetCtlException($"{path}.endpoint: HTTPS is required.", 2);
            }

            ProviderEndpointPolicy.Validate(endpoint, endpointHosts, $"{path}.endpoint");
            return endpoint;
        }

        private static HashSet<string> ReadEndpointHosts(YamlSequenceNode? values, string path)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values is null)
            {
                return result;
            }

            foreach (string host in YamlValues.Strings(values, $"{path}.endpoint_hosts"))
            {
                ProviderEndpointPolicy.ValidateHost(host, $"{path}.endpoint_hosts");
                if (!result.Add(host))
                {
                    throw new AssetCtlException($"{path}.endpoint_hosts: duplicate host '{host}'.", 2);
                }
            }

            if (result.Count == 0)
            {
                throw new AssetCtlException($"{path}.endpoint_hosts: at least one host is required.", 2);
            }

            return result;
        }

        private static HashSet<AssetLifecycle> ReadAllowedLifecycles(YamlMappingNode node, string path)
        {
            YamlSequenceNode? values = node.OptionalSequence("allowed_lifecycles", path);
            if (values is null)
            {
                return Enum.GetValues<AssetLifecycle>().ToHashSet();
            }

            var result = new HashSet<AssetLifecycle>();
            foreach (string value in YamlValues.Strings(values, $"{path}.allowed_lifecycles"))
            {
                AssetLifecycle lifecycle = ParseLifecycle(value, $"{path}.allowed_lifecycles")!.Value;
                if (!result.Add(lifecycle))
                {
                    throw new AssetCtlException($"{path}.allowed_lifecycles: duplicate lifecycle '{value}'.", 2);
                }
            }

            if (result.Count == 0)
            {
                throw new AssetCtlException($"{path}.allowed_lifecycles: at least one lifecycle is required.", 2);
            }

            return result;
        }

        private static string? ReadCredential(YamlMappingNode? credentials, string path)
        {
            if (credentials is null)
            {
                return null;
            }

            credentials.RequireOnly($"{path}.credentials", "api_key");
            YamlMappingNode apiKey = credentials.Mapping("api_key", $"{path}.credentials");
            apiKey.RequireOnly($"{path}.credentials.api_key", "source", "name");
            if (
                !string.Equals(
                    apiKey.Scalar("source", $"{path}.credentials.api_key"),
                    "environment",
                    StringComparison.Ordinal
                )
            )
            {
                throw new AssetCtlException(
                    $"{path}.credentials.api_key: only environment references are supported.",
                    2
                );
            }

            string credential = apiKey.Scalar("name", $"{path}.credentials.api_key");
            if (
                !System.Text.RegularExpressions.Regex.IsMatch(
                    credential,
                    "^[A-Z][A-Z0-9_]{1,127}$",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100)
                )
            )
            {
                throw new AssetCtlException($"{path}.credentials.api_key.name: invalid environment variable name.", 2);
            }

            return credential;
        }

        private static HashSet<string> ReadAllowedHosts(YamlMappingNode? downloads, string path)
        {
            HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
            if (downloads is null)
            {
                return result;
            }

            downloads.RequireOnly($"{path}.downloads", "allowed_hosts");
            string downloadsPath = $"{path}.downloads";
            result.UnionWith(
                YamlValues.Strings(downloads.Sequence("allowed_hosts", downloadsPath), $"{downloadsPath}.allowed_hosts")
            );
            return result;
        }

        private static Dictionary<string, ModelProfile> ReadModels(
            YamlMappingNode nodes,
            string path,
            IAdapterDescriptor adapter
        )
        {
            Dictionary<string, ModelProfile> result = new(StringComparer.Ordinal);
            foreach (KeyValuePair<YamlNode, YamlNode> pair in nodes.Children)
            {
                string modelId = pair.Key.AsScalar($"{path}.models key");
                ModelProfile model = ReadModel(path, modelId, pair.Value.AsMapping($"{path}.models.{modelId}"));
                AssetCapability[] unsupported = model.Capabilities.Except(adapter.SupportedCapabilities).ToArray();
                if (unsupported.Length != 0)
                {
                    throw new AssetCtlException(
                        $"{path}.models.{modelId}: adapter does not support {string.Join(", ", unsupported)}.",
                        2
                    );
                }

                try
                {
                    adapter.ValidateOptions(model.Options);
                }
                catch (ProviderException exception)
                {
                    throw new AssetCtlException($"{path}.models.{modelId}.options: {exception.Message}", 2);
                }

                result.Add(modelId, model);
            }

            return result;
        }

        private static ModelProfile ReadModel(string providerPath, string id, YamlMappingNode node)
        {
            string path = $"{providerPath}.models.{id}";
            node.RequireOnly(path, "model", "capabilities", "economics", "options");
            var capabilities = new HashSet<AssetCapability>();
            foreach (string value in YamlValues.Strings(node.Sequence("capabilities", path), $"{path}.capabilities"))
            {
                capabilities.Add(ParseCapability(value, $"{path}.capabilities"));
            }

            (decimal? cost, string pricingBasis) = ReadEconomics(node.Mapping("economics", path), path);
            Dictionary<string, string> options = ReadOptions(node.OptionalMapping("options", path), path);
            return new ModelProfile(id, node.Scalar("model", path), capabilities, cost, pricingBasis, options);
        }

        private static (decimal? Cost, string PricingBasis) ReadEconomics(YamlMappingNode economics, string path)
        {
            economics.RequireOnly(
                $"{path}.economics",
                "currency",
                "estimated_cost_per_output",
                "pricing_basis",
                "effective_date"
            );
            if (!string.Equals(economics.Scalar("currency", $"{path}.economics"), "USD", StringComparison.Ordinal))
            {
                throw new AssetCtlException($"{path}.economics.currency: only USD is supported.", 2);
            }

            _ = economics.Date("effective_date", $"{path}.economics");
            decimal? cost = economics.OptionalDecimal("estimated_cost_per_output", $"{path}.economics");
            if (cost is not null)
            {
                if (cost < 0)
                {
                    throw new AssetCtlException($"{path}.economics.estimated_cost_per_output: cannot be negative.", 2);
                }
            }

            string pricingBasis = economics.OptionalScalar("pricing_basis", $"{path}.economics") ?? "fixed-output";
            if (
                pricingBasis
                is not ("fixed-output" or "provider-calculated" or "quality-and-resolution" or "token-usage")
            )
            {
                throw new AssetCtlException(
                    $"{path}.economics.pricing_basis: unknown pricing basis '{pricingBasis}'.",
                    2
                );
            }

            return (cost, pricingBasis);
        }

        private static Dictionary<string, string> ReadOptions(YamlMappingNode? optionNode, string modelPath)
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            if (optionNode is not null)
            {
                foreach (
                    global::System.Collections.Generic.KeyValuePair<
                        global::YamlDotNet.RepresentationModel.YamlNode,
                        global::YamlDotNet.RepresentationModel.YamlNode
                    > pair in optionNode.Children
                )
                {
                    string key = pair.Key.AsScalar($"{modelPath}.options key");
                    options.Add(key, pair.Value.AsScalar($"{modelPath}.options.{key}"));
                }
            }

            return options;
        }

        private static (IReadOnlyList<RouteDefinition>, IReadOnlyList<RouteDefinition>) ReadRoutes(
            string root,
            IDictionary<string, string> hashes,
            IReadOnlyDictionary<string, ProviderInstance> providers,
            AssetCtlLimits limits
        )
        {
            global::YamlDotNet.RepresentationModel.YamlMappingNode document = Load(
                root,
                "config/assets/routing.yaml",
                hashes
            );
            document.RequireOnly("routing", "schema_version", "routes", "review_routes");
            RequireVersion(document, "routing");
            List<RouteDefinition> routes = ReadRouteList(
                document.Sequence("routes", "routing"),
                false,
                providers,
                limits
            );
            List<RouteDefinition> reviewRoutes = ReadRouteList(
                document.Sequence("review_routes", "routing"),
                true,
                providers,
                limits
            );
            string? duplicate = routes
                .Select(route => route.Id)
                .Intersect(reviewRoutes.Select(route => route.Id), StringComparer.Ordinal)
                .FirstOrDefault();
            if (duplicate is not null)
            {
                throw new AssetCtlException(
                    $"routing: route id '{duplicate}' is duplicated across routes and review_routes.",
                    2
                );
            }

            return (routes, reviewRoutes);
        }

        private static List<RouteDefinition> ReadRouteList(
            YamlSequenceNode sequence,
            bool review,
            IReadOnlyDictionary<string, ProviderInstance> providers,
            AssetCtlLimits limits
        )
        {
            var result = new List<RouteDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < sequence.Children.Count; index++)
            {
                string path = review ? $"review_routes[{index}]" : $"routes[{index}]";
                YamlMappingNode node = sequence.Children[index].AsMapping(path);
                node.RequireOnly(
                    path,
                    "id",
                    "priority",
                    "lifecycle",
                    "format",
                    "capability",
                    "targets",
                    "fallback",
                    "retry"
                );
                string id = node.Scalar("id", path);
                if (!ids.Add(id))
                {
                    throw new AssetCtlException($"{path}.id: duplicate route '{id}'.", 2);
                }

                global::AlterCourse.AssetCtl.Domain.DomainModels.RouteTarget[] targets = YamlValues
                    .Strings(node.Sequence("targets", path), path)
                    .Select(value => ParseTarget(value, path, providers))
                    .ToArray();
                result.Add(
                    new RouteDefinition(
                        id,
                        node.Integer("priority", path),
                        ParseLifecycle(node.OptionalScalar("lifecycle", path), path),
                        ParseFormat(node.OptionalScalar("format", path), path),
                        ParseCapability(node.Scalar("capability", path), path),
                        targets,
                        index,
                        ReadFallback(node.Mapping("fallback", path), path),
                        ReadRetry(node.Mapping("retry", path), path, limits)
                    )
                );
            }

            return result.OrderByDescending(route => route.Priority).ThenBy(route => route.ConfigurationOrder).ToList();
        }

        private static RouteFallbackPolicy ReadFallback(YamlMappingNode node, string path)
        {
            string fallbackPath = $"{path}.fallback";
            node.RequireOnly(fallbackPath, "capability_match", "allowed_error_categories");
            return new RouteFallbackPolicy(
                node.Boolean("capability_match", fallbackPath),
                ReadErrorCategories(node.Sequence("allowed_error_categories", fallbackPath), fallbackPath)
            );
        }

        private static RouteRetryPolicy ReadRetry(YamlMappingNode node, string path, AssetCtlLimits limits)
        {
            string retryPath = $"{path}.retry";
            node.RequireOnly(
                retryPath,
                "maximum_attempts_per_target",
                "initial_delay_milliseconds",
                "maximum_delay_milliseconds",
                "jitter_ratio",
                "error_categories"
            );
            int attempts = node.Integer("maximum_attempts_per_target", retryPath);
            int initialDelay = node.Integer("initial_delay_milliseconds", retryPath);
            int maximumDelay = node.Integer("maximum_delay_milliseconds", retryPath);
            double jitter = node.Double("jitter_ratio", retryPath);
            if (
                attempts < 1
                || attempts > limits.MaximumTotalAttempts
                || initialDelay < 0
                || maximumDelay < initialDelay
                || jitter is < 0 or > 1
            )
            {
                throw new AssetCtlException(
                    $"{retryPath}: retry metadata exceeds maximum_total_attempts or delay safety bounds.",
                    2
                );
            }

            return new RouteRetryPolicy(
                attempts,
                initialDelay,
                maximumDelay,
                jitter,
                ReadErrorCategories(node.Sequence("error_categories", retryPath), retryPath)
            );
        }

        private static HashSet<ProviderErrorCategory> ReadErrorCategories(YamlSequenceNode sequence, string path)
        {
            var result = new HashSet<ProviderErrorCategory>();
            foreach (string value in YamlValues.Strings(sequence, path))
            {
                if (!result.Add(ParseErrorCategory(value, path)))
                {
                    throw new AssetCtlException($"{path}: duplicate error category '{value}'.", 2);
                }
            }

            return result;
        }

        private static RouteTarget ParseTarget(
            string value,
            string path,
            IReadOnlyDictionary<string, ProviderInstance> providers
        )
        {
            string[] parts = value.Split('/', 2);
            if (
                parts.Length != 2
                || !providers.TryGetValue(parts[0], out ProviderInstance? provider)
                || !provider.Models.ContainsKey(parts[1])
            )
            {
                throw new AssetCtlException($"{path}.targets: unknown target '{value}'.", 2);
            }

            return new RouteTarget(parts[0], parts[1]);
        }

        private static Dictionary<string, QualityTier> ReadQualityTiers(
            string root,
            IDictionary<string, string> hashes,
            AssetCtlLimits limits
        )
        {
            global::YamlDotNet.RepresentationModel.YamlMappingNode document = Load(
                root,
                "config/assets/quality-tiers.yaml",
                hashes
            );
            document.RequireOnly("quality-tiers", "schema_version", "quality_tiers");
            RequireVersion(document, "quality-tiers");
            var result = new Dictionary<string, QualityTier>(StringComparer.Ordinal);
            foreach (
                global::System.Collections.Generic.KeyValuePair<
                    global::YamlDotNet.RepresentationModel.YamlNode,
                    global::YamlDotNet.RepresentationModel.YamlNode
                > pair in document.Mapping("quality_tiers", "quality-tiers").Children
            )
            {
                string id = pair.Key.AsScalar("quality_tiers key");
                string path = $"quality_tiers.{id}";
                YamlMappingNode node = pair.Value.AsMapping(path);
                node.RequireOnly(
                    path,
                    "candidates",
                    "attempts_per_route",
                    "semantic_review",
                    "allow_unreviewed_placeholder",
                    "minimum_semantic_score"
                );
                int candidates = node.Integer("candidates", path);
                int attempts = node.Integer("attempts_per_route", path);
                if (
                    candidates < 1
                    || candidates > limits.MaximumCandidatesPerRequest
                    || attempts < 1
                    || attempts > limits.MaximumTotalAttempts
                )
                {
                    throw new AssetCtlException($"{path}: candidate or attempt count exceeds root limits.", 2);
                }

                double score = node.OptionalDouble("minimum_semantic_score", path) ?? 0;
                if (score is < 0 or > 1)
                {
                    throw new AssetCtlException($"{path}.minimum_semantic_score: expected 0..1.", 2);
                }

                result.Add(
                    id,
                    new QualityTier(
                        id,
                        candidates,
                        attempts,
                        SemanticReviewValue(ParseSemanticReviewPolicy(node.Scalar("semantic_review", path), path)),
                        node.Boolean("allow_unreviewed_placeholder", path),
                        score
                    )
                );
            }

            return result;
        }

        private static Dictionary<string, StyleProfile> ReadStyles(
            string root,
            IDictionary<string, string> hashes,
            AssetCtlPaths paths
        )
        {
            string styleRoot = PathPolicy.ResolveUnder(root, paths.StyleRoot, "style root", allowMissing: false);
            var result = new Dictionary<string, StyleProfile>(StringComparer.Ordinal);
            foreach (
                string path in Directory
                    .EnumerateFiles(styleRoot, "*.yaml", SearchOption.TopDirectoryOnly)
                    .Order(StringComparer.Ordinal)
            )
            {
                string relative = Path.GetRelativePath(root, path);
                global::YamlDotNet.RepresentationModel.YamlMappingNode document = Load(root, relative, hashes);
                document.RequireOnly(relative, "schema_version", "id", "summary", "required", "prohibited");
                RequireVersion(document, relative);
                string id = document.Scalar("id", relative);
                if (
                    !result.TryAdd(
                        id,
                        new StyleProfile(
                            id,
                            document.Scalar("summary", relative),
                            YamlValues.Strings(document.OptionalSequence("required", relative), relative),
                            YamlValues.Strings(document.OptionalSequence("prohibited", relative), relative)
                        )
                    )
                )
                {
                    throw new AssetCtlException($"{relative}: duplicate style id '{id}'.", 2);
                }
            }

            return result;
        }

        private static void ValidatePaths(string root, AssetCtlPaths paths)
        {
            foreach (
                (string, string) pair in new[]
                {
                    (paths.GodotAssetRoot, "godot_asset_root"),
                    (paths.CatalogRoot, "catalog_root"),
                    (paths.StyleRoot, "style_root"),
                    (paths.WorkRoot, "work_root"),
                    (paths.ReceiptRoot, "receipt_root"),
                    (paths.StateRoot, "state_root"),
                    (paths.LogRoot, "log_root"),
                }
            )
            {
                _ = PathPolicy.ResolveUnder(root, pair.Item1, pair.Item2, allowMissing: true);
            }
        }

        private static YamlMappingNode Load(string root, string relativePath, IDictionary<string, string> hashes)
        {
            string path = PathPolicy.ResolveUnder(root, relativePath, relativePath, allowMissing: false);
            byte[] bytes = File.ReadAllBytes(path);
            hashes.Add(relativePath, Convert.ToHexStringLower(SHA256.HashData(bytes)));
            return StrictYaml.LoadMapping(path);
        }

        private static void RequireVersion(YamlMappingNode node, string path)
        {
            if (!string.Equals(node.Scalar("schema_version", path), "1", StringComparison.Ordinal))
            {
                throw new AssetCtlException($"{path}.schema_version: unsupported version; expected '1'.", 2);
            }
        }

        public static string Hash(string text) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        public static AssetCapability ParseCapability(string value, string path) =>
            value switch
            {
                "raster.generate" => AssetCapability.RasterGenerate,
                "vector.generate" => AssetCapability.VectorGenerate,
                "image.edit" => AssetCapability.ImageEdit,
                "image.reference-input" => AssetCapability.ImageReferenceInput,
                "image.transparent-output" => AssetCapability.ImageTransparentOutput,
                "image.background-remove" => AssetCapability.ImageBackgroundRemove,
                "image.vectorize" => AssetCapability.ImageVectorize,
                "review.semantic" => AssetCapability.ReviewSemantic,
                "review.reference-comparison" => AssetCapability.ReviewReferenceComparison,
                _ => throw new AssetCtlException($"{path}: unknown capability '{value}'.", 2),
            };

        public static SemanticReviewPolicy ParseSemanticReviewPolicy(string value, string path) =>
            value switch
            {
                "disabled" => SemanticReviewPolicy.Disabled,
                "when-available" => SemanticReviewPolicy.WhenAvailable,
                "required" => SemanticReviewPolicy.Required,
                _ => throw new AssetCtlException(
                    $"{path}.semantic_review: expected disabled, when-available, or required; found '{value}'.",
                    2
                ),
            };

        public static OutputTransparency ParseOutputTransparency(string value, string path) =>
            value switch
            {
                "required" => OutputTransparency.Required,
                "optional" => OutputTransparency.Optional,
                _ => throw new AssetCtlException(
                    $"{path}.transparency: expected required or optional; found '{value}'.",
                    2
                ),
            };

        private static string SemanticReviewValue(SemanticReviewPolicy value) =>
            value switch
            {
                SemanticReviewPolicy.Disabled => "disabled",
                SemanticReviewPolicy.WhenAvailable => "when-available",
                SemanticReviewPolicy.Required => "required",
                _ => throw new ArgumentOutOfRangeException(nameof(value)),
            };

        private static ProviderErrorCategory ParseErrorCategory(string value, string path) =>
            value switch
            {
                "invalid-request" => ProviderErrorCategory.InvalidRequest,
                "authentication" => ProviderErrorCategory.Authentication,
                "authorization" => ProviderErrorCategory.Authorization,
                "insufficient-balance" => ProviderErrorCategory.InsufficientBalance,
                "rate-limit" => ProviderErrorCategory.RateLimit,
                "transient-network" => ProviderErrorCategory.TransientNetwork,
                "provider-server" => ProviderErrorCategory.ProviderServer,
                "timeout" => ProviderErrorCategory.Timeout,
                "malformed-response" => ProviderErrorCategory.MalformedResponse,
                "unsafe-download" => ProviderErrorCategory.UnsafeDownload,
                "unsupported-output" => ProviderErrorCategory.UnsupportedOutput,
                "validation" => ProviderErrorCategory.Validation,
                _ => throw new AssetCtlException($"{path}: unknown error category '{value}'.", 2),
            };

        private static AssetLifecycle? ParseLifecycle(string? value, string path) =>
            value switch
            {
                null => null,
                "placeholder" => AssetLifecycle.Placeholder,
                "candidate" => AssetLifecycle.Candidate,
                "approved" => AssetLifecycle.Approved,
                "deprecated" => AssetLifecycle.Deprecated,
                _ => throw new AssetCtlException($"{path}.lifecycle: unknown lifecycle '{value}'.", 2),
            };

        private static AssetFormat? ParseFormat(string? value, string path) =>
            value switch
            {
                null => null,
                "svg" => AssetFormat.Svg,
                "png" => AssetFormat.Png,
                _ => throw new AssetCtlException($"{path}.format: unknown format '{value}'.", 2),
            };
    }

    public interface IAdapterDescriptor
    {
        public string AdapterId { get; }

        public IReadOnlySet<AssetCapability> SupportedCapabilities { get; }

        public bool IsLocalFallback => false;

        public bool RequiresNetwork => false;

        public string? OutputContractRejection(ModelProfile model, OutputContract? output) => null;

        public void ValidateOptions(IReadOnlyDictionary<string, string> options);
    }

    public static class PathPolicy
    {
        /// <summary>Resolve a repository-relative path while rejecting absolute paths, traversal, and an existing symlink escape.</summary>
        public static string ResolveUnder(string root, string relativePath, string field, bool allowMissing)
        {
            if (
                Path.IsPathRooted(relativePath)
                || relativePath
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Contains("..", StringComparer.Ordinal)
            )
            {
                throw new AssetCtlException($"{field}: absolute paths and parent traversal are prohibited.", 2);
            }

            root = Path.GetFullPath(root);
            string candidate = Path.GetFullPath(relativePath, root);
            if (!IsContained(root, candidate))
            {
                throw new AssetCtlException($"{field}: path escapes the repository.", 2);
            }

            string current = root;
            foreach (string segment in Path.GetRelativePath(root, candidate).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, segment);
                if (!File.Exists(current) && !Directory.Exists(current))
                {
                    if (allowMissing)
                    {
                        break;
                    }

                    throw new AssetCtlException($"{field}: path does not exist.", 2);
                }

                global::System.IO.FileAttributes attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    string? resolved = File.ResolveLinkTarget(current, returnFinalTarget: true)?.FullName;
                    if (resolved is null || !IsContained(root, Path.GetFullPath(resolved)))
                    {
                        throw new AssetCtlException($"{field}: symlink escapes the repository.", 2);
                    }
                }
            }

            return candidate;
        }

        public static string ResolveUnderConfiguredRoot(
            string repositoryRoot,
            string configuredRoot,
            string repositoryRelativePath,
            string field,
            bool allowMissing
        )
        {
            string allowedRoot = ResolveUnder(repositoryRoot, configuredRoot, $"{field} root", allowMissing: true);
            string candidate = ResolveUnder(repositoryRoot, repositoryRelativePath, field, allowMissing);
            if (!IsContained(allowedRoot, candidate))
            {
                throw new AssetCtlException($"{field}: path is outside configured root '{configuredRoot}'.", 2);
            }

            EnsurePhysicalContainment(allowedRoot, candidate, field, allowMissing);

            return candidate;
        }

        /// <summary>Require every existing path component to resolve beneath the physical configured root.</summary>
        private static void EnsurePhysicalContainment(
            string configuredRoot,
            string candidate,
            string field,
            bool allowMissing
        )
        {
            string physicalRoot = ResolvePhysicalPath(configuredRoot);
            string current = configuredRoot;
            foreach (
                string segment in Path.GetRelativePath(configuredRoot, candidate).Split(Path.DirectorySeparatorChar)
            )
            {
                current = Path.Combine(current, segment);
                if (!File.Exists(current) && !Directory.Exists(current))
                {
                    if (allowMissing)
                    {
                        break;
                    }

                    throw new AssetCtlException($"{field}: path does not exist.", 2);
                }

                string physicalCurrent = ResolvePhysicalPath(current);
                if (!IsContained(physicalRoot, physicalCurrent))
                {
                    throw new AssetCtlException($"{field}: symlink escapes configured root.", 2);
                }
            }
        }

        private static string ResolvePhysicalPath(string path)
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return Path.GetFullPath(info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path);
        }

        public static string ResolveManifestPath(
            EffectiveConfiguration configuration,
            string repositoryRelativePath,
            bool allowMissing
        ) =>
            ResolveUnderConfiguredRoot(
                configuration.RepositoryRoot,
                configuration.Paths.CatalogRoot,
                repositoryRelativePath,
                "manifest path",
                allowMissing
            );

        public static string ResolveOutputPath(
            EffectiveConfiguration configuration,
            string repositoryRelativePath,
            bool allowMissing
        ) =>
            ResolveUnderConfiguredRoot(
                configuration.RepositoryRoot,
                configuration.Paths.GodotAssetRoot,
                repositoryRelativePath,
                "output path",
                allowMissing
            );

        public static string ResolveReferencePath(
            EffectiveConfiguration configuration,
            string repositoryRelativePath,
            bool allowMissing
        ) => ResolveUnder(configuration.RepositoryRoot, repositoryRelativePath, "reference path", allowMissing);

        private static bool IsContained(string root, string candidate) =>
            string.Equals(root, candidate, StringComparison.Ordinal)
            || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    public static class JsonSchemaDocumentValidator
    {
        private const string SupportedDraft = "https://json-schema.org/draft/2020-12/schema";

        public static IReadOnlyList<SchemaDocumentStatus> ValidateTrackedSchemas(string repositoryRoot)
        {
            string schemaRoot = PathPolicy.ResolveUnder(
                repositoryRoot,
                "config/assets/schemas",
                "schema root",
                allowMissing: false
            );
            var results = new List<SchemaDocumentStatus>();
            foreach (string path in Directory.EnumerateFiles(schemaRoot, "*.json").Order(StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(repositoryRoot, path);
                try
                {
                    string contents = File.ReadAllText(path);
                    using var parsed = JsonDocument.Parse(
                        contents,
                        new JsonDocumentOptions
                        {
                            AllowTrailingCommas = false,
                            CommentHandling = JsonCommentHandling.Disallow,
                        }
                    );
                    JsonElement document = parsed.RootElement;
                    string? dialect =
                        document.TryGetProperty("$schema", out JsonElement dialectElement)
                        && dialectElement.ValueKind == JsonValueKind.String
                            ? dialectElement.GetString()
                            : null;
                    if (!string.Equals(dialect, SupportedDraft, StringComparison.Ordinal))
                    {
                        throw new AssetCtlException($"{relative}.$schema: expected '{SupportedDraft}'.", 2);
                    }

                    EvaluationResults evaluation = MetaSchemas.Draft202012.Evaluate(
                        document,
                        new EvaluationOptions { OutputFormat = OutputFormat.List }
                    );
                    if (!evaluation.IsValid)
                    {
                        throw new AssetCtlException($"{relative}: invalid draft 2020-12 schema: {evaluation}", 2);
                    }

                    _ = JsonSchema.FromText(contents);
                }
                catch (JsonException exception)
                {
                    throw new AssetCtlException($"{relative}: invalid JSON Schema document: {exception.Message}", 2);
                }

                results.Add(new SchemaDocumentStatus(relative, SupportedDraft, true));
            }

            if (results.Count == 0)
            {
                throw new AssetCtlException("config/assets/schemas: no tracked JSON Schema documents found.", 2);
            }

            return results;
        }
    }
}
