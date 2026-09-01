namespace AlterCourse.AssetCtl.Routing;

using SemanticReviewPolicy = AlterCourse.AssetCtl.Domain.DomainModels.SemanticReviewPolicy;

/// <summary>Evaluates declarative routes without interpreting provider identity or provider-specific options.</summary>
internal sealed class AssetRouter(AdapterRegistry adapters)
{
    public GenerationPlan Plan(EffectiveConfiguration configuration, AssetRequest request)
    {
        if (!configuration.QualityTiers.TryGetValue(request.QualityTier, out QualityTier? tier))
        {
            throw new AssetCtlException($"Unknown quality tier '{request.QualityTier}'.", 2);
        }

        global::System.Collections.Generic.IReadOnlyList<global::AlterCourse.AssetCtl.Domain.DomainModels.AssetCapability> required =
            RequiredCapabilities(request);
        List<PlannedTarget> targets = BuildTargets(configuration, request, required, tier);

        global::AlterCourse.AssetCtl.Domain.DomainModels.PlannedTarget? selected = targets.FirstOrDefault(target =>
            target.Eligible
        );
        PlannedTarget? reviewer = string.Equals(tier.SemanticReview, "disabled", StringComparison.Ordinal)
            ? null
            : FindReviewer(configuration, request.Lifecycle);

        if (string.Equals(tier.SemanticReview, "required", StringComparison.Ordinal) && reviewer is null)
        {
            selected = null;
        }

        decimal? estimatedMaximumCost = EstimateMaximumCost(configuration, targets, reviewer, tier);
        return new GenerationPlan(
            request,
            required,
            targets,
            selected,
            reviewer,
            tier.Candidates,
            tier.AttemptsPerRoute,
            estimatedMaximumCost,
            string.Equals(selected?.AdapterId, "local-placeholder", StringComparison.Ordinal)
        );
    }

    private List<PlannedTarget> BuildTargets(
        EffectiveConfiguration configuration,
        AssetRequest request,
        IReadOnlyList<AssetCapability> required,
        QualityTier tier
    )
    {
        List<PlannedTarget> targets = [];
        foreach (RouteDefinition route in configuration.Routes.Where(route => Matches(route, request)))
        {
            foreach (RouteTarget target in RouteTargets(configuration, route))
            {
                targets.Add(Evaluate(configuration, route, target, required, tier.Candidates, request.Lifecycle));
            }
        }

        return targets;
    }

    private static decimal? EstimateMaximumCost(
        EffectiveConfiguration configuration,
        IReadOnlyList<PlannedTarget> targets,
        PlannedTarget? reviewer,
        QualityTier tier
    )
    {
        int remainingAttempts = configuration.Limits.MaximumTotalAttempts;
        decimal total = 0;
        foreach (PlannedTarget target in targets.Where(target => target.Eligible))
        {
            RouteDefinition route = configuration.Routes.Single(route =>
                string.Equals(route.Id, target.RouteId, StringComparison.Ordinal)
            );
            int attempts = Math.Min(
                remainingAttempts,
                route.RetryPolicy?.MaximumAttemptsPerTarget ?? tier.AttemptsPerRoute
            );
            if (attempts == 0)
            {
                break;
            }

            if (target.EstimatedMaximumCost is null)
            {
                return null;
            }

            total += target.EstimatedMaximumCost.Value * attempts;
            remainingAttempts -= attempts;
        }

        if (tier.ReviewPolicy is not SemanticReviewPolicy.Disabled && reviewer is not null)
        {
            if (reviewer.EstimatedMaximumCost is null)
            {
                return null;
            }

            RouteDefinition reviewRoute = configuration.ReviewRoutes.Single(route =>
                string.Equals(route.Id, reviewer.RouteId, StringComparison.Ordinal)
            );
            int reviewAttempts = reviewRoute.RetryPolicy?.MaximumAttemptsPerTarget ?? 1;
            total += reviewer.EstimatedMaximumCost.Value * tier.Candidates * reviewAttempts;
        }

        return total;
    }

    private PlannedTarget? FindReviewer(EffectiveConfiguration configuration, AssetLifecycle lifecycle)
    {
        foreach (RouteDefinition route in configuration.ReviewRoutes)
        {
            foreach (RouteTarget target in RouteTargets(configuration, route))
            {
                PlannedTarget evaluated = Evaluate(
                    configuration,
                    route,
                    target,
                    [AssetCapability.ReviewSemantic],
                    1,
                    lifecycle
                );
                if (evaluated.Eligible)
                {
                    return evaluated;
                }
            }
        }

        return null;
    }

    private static IEnumerable<RouteTarget> RouteTargets(EffectiveConfiguration configuration, RouteDefinition route)
    {
        var seen = new HashSet<(string ProviderId, string ModelProfileId)>();
        foreach (RouteTarget target in route.Targets)
        {
            if (seen.Add((target.ProviderId, target.ModelProfileId)))
            {
                yield return target;
            }
        }

        if (route.FallbackPolicy?.CapabilityMatch != true)
        {
            yield break;
        }

        foreach (
            ProviderInstance provider in configuration.Providers.Values.OrderBy(
                value => value.Id,
                StringComparer.Ordinal
            )
        )
        {
            foreach (ModelProfile model in provider.Models.Values.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                if (seen.Add((provider.Id, model.Id)))
                {
                    yield return new RouteTarget(provider.Id, model.Id);
                }
            }
        }
    }

    private PlannedTarget Evaluate(
        EffectiveConfiguration configuration,
        RouteDefinition route,
        RouteTarget target,
        IReadOnlyList<AssetCapability> required,
        int candidates,
        AssetLifecycle lifecycle
    )
    {
        global::AlterCourse.AssetCtl.Domain.DomainModels.ProviderInstance provider = configuration.Providers[
            target.ProviderId
        ];
        global::AlterCourse.AssetCtl.Domain.DomainModels.ModelProfile model = provider.Models[target.ModelProfileId];
        global::AlterCourse.AssetCtl.Configuration.ConfigurationTypes.IAdapterDescriptor descriptor =
            adapters.Descriptors[provider.AdapterId];
        List<string> reasons = [];
        AddAvailabilityReasons(configuration, provider, reasons);
        AddCapabilityReasons(required, model, descriptor, reasons);
        descriptor.ValidateOptions(model.Options);
        decimal? estimate = model.EstimatedCostPerOutput * candidates;
        AddPolicyReasons(configuration, lifecycle, estimate, reasons);

        return new PlannedTarget(
            route.Id,
            provider.Id,
            model.Id,
            provider.AdapterId,
            reasons.Count == 0,
            reasons,
            estimate
        );
    }

    private static void AddAvailabilityReasons(
        EffectiveConfiguration configuration,
        ProviderInstance provider,
        List<string> reasons
    )
    {
        if (!provider.Enabled)
        {
            reasons.Add("provider-disabled");
        }

        if (provider.Endpoint is not null && !configuration.Policy.ExternalGenerationEnabled)
        {
            reasons.Add("external-generation-disabled");
        }

        if (
            provider.CredentialEnvironmentVariable is not null
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(provider.CredentialEnvironmentVariable))
        )
        {
            reasons.Add($"credential-missing:{provider.CredentialEnvironmentVariable}");
        }
    }

    private static void AddCapabilityReasons(
        IReadOnlyList<AssetCapability> required,
        ModelProfile model,
        IAdapterDescriptor descriptor,
        List<string> reasons
    )
    {
        if (!required.All(model.Capabilities.Contains))
        {
            reasons.Add("model-capability-mismatch");
        }

        if (!required.All(descriptor.SupportedCapabilities.Contains))
        {
            reasons.Add("adapter-capability-mismatch");
        }
    }

    private static void AddPolicyReasons(
        EffectiveConfiguration configuration,
        AssetLifecycle lifecycle,
        decimal? estimate,
        List<string> reasons
    )
    {
        if (
            estimate is null
            && string.Equals(configuration.Policy.UnknownPricePolicy, "reject", StringComparison.Ordinal)
        )
        {
            reasons.Add("unknown-price");
        }
        else if (estimate > configuration.Spending.PerAssetUsd || estimate > configuration.Spending.PerRunUsd)
        {
            reasons.Add("over-budget");
        }

        if (lifecycle is AssetLifecycle.Approved or AssetLifecycle.Deprecated)
        {
            reasons.Add("protected-lifecycle");
        }
    }

    private static bool Matches(RouteDefinition route, AssetRequest request) =>
        (route.Lifecycle is null || route.Lifecycle == request.Lifecycle)
        && (route.Format is null || route.Format == request.Output.Format);

    private static List<AssetCapability> RequiredCapabilities(AssetRequest request)
    {
        var result = new List<AssetCapability>
        {
            request.Output.Format == AssetFormat.Svg ? AssetCapability.VectorGenerate : AssetCapability.RasterGenerate,
        };
        if (request.References.Count != 0)
        {
            result.Add(AssetCapability.ImageReferenceInput);
        }

        if (request.Output.TransparencyRequired)
        {
            result.Add(AssetCapability.ImageTransparentOutput);
        }

        return result;
    }
}
