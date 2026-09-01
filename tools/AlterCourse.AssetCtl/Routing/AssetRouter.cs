namespace AlterCourse.AssetCtl.Routing;

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
        List<PlannedTarget> targets = [];
        foreach (
            global::AlterCourse.AssetCtl.Domain.DomainModels.RouteDefinition route in configuration.Routes.Where(
                route => Matches(route, request)
            )
        )
        {
            foreach (global::AlterCourse.AssetCtl.Domain.DomainModels.RouteTarget target in route.Targets)
            {
                targets.Add(Evaluate(configuration, route, target, required, tier.Candidates, request.Lifecycle));
            }
        }

        global::AlterCourse.AssetCtl.Domain.DomainModels.PlannedTarget? selected = targets.FirstOrDefault(target =>
            target.Eligible
        );
        PlannedTarget? reviewer = null;
        if (!string.Equals(tier.SemanticReview, "disabled", StringComparison.Ordinal))
        {
            foreach (
                global::AlterCourse.AssetCtl.Domain.DomainModels.RouteDefinition route in configuration.ReviewRoutes
            )
            {
                foreach (global::AlterCourse.AssetCtl.Domain.DomainModels.RouteTarget target in route.Targets)
                {
                    global::AlterCourse.AssetCtl.Domain.DomainModels.PlannedTarget evaluated = Evaluate(
                        configuration,
                        route,
                        target,
                        [AssetCapability.ReviewSemantic],
                        1,
                        request.Lifecycle
                    );
                    if (evaluated.Eligible)
                    {
                        reviewer = evaluated;
                        break;
                    }
                }

                if (reviewer is not null)
                {
                    break;
                }
            }
        }

        if (string.Equals(tier.SemanticReview, "required", StringComparison.Ordinal) && reviewer is null)
        {
            selected = null;
        }

        return new GenerationPlan(
            request,
            required,
            targets,
            selected,
            reviewer,
            tier.Candidates,
            tier.AttemptsPerRoute,
            selected?.EstimatedMaximumCost,
            string.Equals(selected?.AdapterId, "local-placeholder", StringComparison.Ordinal)
        );
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

        if (!required.All(model.Capabilities.Contains))
        {
            reasons.Add("model-capability-mismatch");
        }

        if (!required.All(descriptor.SupportedCapabilities.Contains))
        {
            reasons.Add("adapter-capability-mismatch");
        }

        descriptor.ValidateOptions(model.Options);
        decimal? estimate = model.EstimatedCostPerOutput * candidates;
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
