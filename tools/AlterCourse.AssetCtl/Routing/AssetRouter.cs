namespace AlterCourse.AssetCtl.Routing;

using SemanticReviewPolicy = AlterCourse.AssetCtl.Domain.DomainModels.SemanticReviewPolicy;

/// <summary>Evaluates declarative routes without interpreting provider identity or provider-specific options.</summary>
internal sealed class AssetRouter(AdapterRegistry adapters)
{
    public GenerationPlan Plan(EffectiveConfiguration configuration, AssetRequest request, bool offline = false)
    {
        if (!configuration.QualityTiers.TryGetValue(request.QualityTier, out QualityTier? tier))
        {
            throw new AssetCtlException($"Unknown quality tier '{request.QualityTier}'.", 2);
        }

        global::System.Collections.Generic.IReadOnlyList<global::AlterCourse.AssetCtl.Domain.DomainModels.AssetCapability> required =
            RequiredCapabilities(request);
        List<PlannedTarget> targets = BuildTargets(configuration, request, required, tier);
        if (offline)
        {
            targets = ExcludeExternalTargets(configuration, targets);
        }

        PlannedTarget? reviewer = SelectReviewer(configuration, request.Lifecycle, tier, targets, offline);
        if (tier.ReviewPolicy is SemanticReviewPolicy.Required)
        {
            targets = EnforceRequiredReviewerIndependence(configuration, targets, reviewer);
        }

        targets = KeepAffordablePrefixWhenFreeFallbackExists(configuration, targets, reviewer, tier);

        global::AlterCourse.AssetCtl.Domain.DomainModels.PlannedTarget? selected = targets.FirstOrDefault(target =>
            target.Eligible
        );

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
            selected is not null && adapters.Descriptors[selected.AdapterId].IsLocalFallback
        );
    }

    private static List<PlannedTarget> ExcludeExternalTargets(
        EffectiveConfiguration configuration,
        IReadOnlyList<PlannedTarget> targets
    ) =>
        targets
            .Select(target =>
                configuration.Providers[target.ProviderId].Endpoint is null
                    ? target
                    : Reject(target, "offline-external-target")
            )
            .ToList();

    private PlannedTarget? SelectReviewer(
        EffectiveConfiguration configuration,
        AssetLifecycle lifecycle,
        QualityTier tier,
        IReadOnlyList<PlannedTarget> generationTargets,
        bool offline
    )
    {
        if (tier.ReviewPolicy is SemanticReviewPolicy.Disabled)
        {
            return null;
        }

        if (tier.ReviewPolicy is not SemanticReviewPolicy.Required)
        {
            return FindReviewer(configuration, lifecycle, null, offline);
        }

        foreach (PlannedTarget generator in generationTargets.Where(target => target.Eligible))
        {
            string generatorFamily = ProviderFamily(generator.AdapterId);
            PlannedTarget? reviewer = FindReviewer(configuration, lifecycle, generatorFamily, offline);
            if (reviewer is not null)
            {
                return reviewer;
            }
        }

        return null;
    }

    private static List<PlannedTarget> EnforceRequiredReviewerIndependence(
        EffectiveConfiguration configuration,
        IReadOnlyList<PlannedTarget> targets,
        PlannedTarget? reviewer
    )
    {
        if (reviewer is null)
        {
            return targets
                .Select(target => target.Eligible ? Reject(target, "independent-reviewer-unavailable") : target)
                .ToList();
        }

        string reviewerFamily = ProviderFamily(configuration.Providers[reviewer.ProviderId].AdapterId);
        return targets
            .Select(target =>
                target.Eligible
                && string.Equals(ProviderFamily(target.AdapterId), reviewerFamily, StringComparison.Ordinal)
                    ? Reject(target, "reviewer-family-conflict")
                    : target
            )
            .ToList();
    }

    private static PlannedTarget Reject(PlannedTarget target, string reason) =>
        target with
        {
            Eligible = false,
            RejectionReasons = target.RejectionReasons.Append(reason).ToArray(),
        };

    private static string ProviderFamily(string adapterId)
    {
        int separator = adapterId.IndexOf('-', StringComparison.Ordinal);
        return separator < 0 ? adapterId : adapterId[..separator];
    }

    private List<PlannedTarget> BuildTargets(
        EffectiveConfiguration configuration,
        AssetRequest request,
        IReadOnlyList<AssetCapability> required,
        QualityTier tier
    )
    {
        List<PlannedTarget> targets = [];
        foreach (RouteDefinition route in configuration.Routes.Where(route => Matches(route, request, required)))
        {
            foreach (RouteTarget target in RouteTargets(configuration, route))
            {
                targets.Add(
                    Evaluate(configuration, route, target, required, tier.Candidates, request.Lifecycle, request.Output)
                );
            }
        }

        return targets;
    }

    private List<PlannedTarget> KeepAffordablePrefixWhenFreeFallbackExists(
        EffectiveConfiguration configuration,
        List<PlannedTarget> targets,
        PlannedTarget? reviewer,
        QualityTier tier
    )
    {
        bool freeFallback = targets.Any(target =>
            target.Eligible
            && target.EstimatedMaximumCost == 0
            && adapters.Descriptors[target.AdapterId].IsLocalFallback
        );
        if (!freeFallback)
        {
            return targets.ToList();
        }

        decimal reserved = ReviewEstimate(configuration, reviewer, tier) ?? 0;
        var result = new List<PlannedTarget>(targets.Count);
        foreach (PlannedTarget target in targets)
        {
            decimal? attemptCost = AttemptCost(configuration, target, tier);
            if (
                target.Eligible
                && attemptCost is > 0
                && (
                    reserved + attemptCost > configuration.Spending.PerAssetUsd
                    || reserved + attemptCost > configuration.Spending.PerRunUsd
                )
            )
            {
                result.Add(
                    target with
                    {
                        Eligible = false,
                        RejectionReasons = target.RejectionReasons.Append("aggregate-over-budget").ToArray(),
                    }
                );
                continue;
            }

            result.Add(target);
            if (target.Eligible && attemptCost is not null)
            {
                reserved += attemptCost.Value;
            }
        }

        return result;
    }

    private static decimal? AttemptCost(EffectiveConfiguration configuration, PlannedTarget target, QualityTier tier)
    {
        if (target.EstimatedMaximumCost is null)
        {
            return null;
        }

        RouteDefinition route = configuration.Routes.Single(route =>
            string.Equals(route.Id, target.RouteId, StringComparison.Ordinal)
        );
        int attempts = route.RetryPolicy?.MaximumAttemptsPerTarget ?? tier.AttemptsPerRoute;
        return target.EstimatedMaximumCost.Value * attempts;
    }

    private static decimal? ReviewEstimate(
        EffectiveConfiguration configuration,
        PlannedTarget? reviewer,
        QualityTier tier
    )
    {
        if (reviewer is null || tier.ReviewPolicy is SemanticReviewPolicy.Disabled)
        {
            return 0;
        }

        if (reviewer.EstimatedMaximumCost is null)
        {
            return null;
        }

        RouteDefinition route = configuration.ReviewRoutes.Single(value =>
            string.Equals(value.Id, reviewer.RouteId, StringComparison.Ordinal)
        );
        int attempts = route.RetryPolicy?.MaximumAttemptsPerTarget ?? 1;
        return reviewer.EstimatedMaximumCost.Value * tier.Candidates * attempts;
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

    private PlannedTarget? FindReviewer(
        EffectiveConfiguration configuration,
        AssetLifecycle lifecycle,
        string? excludedProviderFamily,
        bool offline
    )
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
                    lifecycle,
                    null
                );
                ProviderInstance provider = configuration.Providers[evaluated.ProviderId];
                bool independent =
                    excludedProviderFamily is null
                    || !string.Equals(
                        ProviderFamily(provider.AdapterId),
                        excludedProviderFamily,
                        StringComparison.Ordinal
                    );
                if (evaluated.Eligible && independent && (!offline || provider.Endpoint is null))
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
        AssetLifecycle lifecycle,
        OutputContract? output
    )
    {
        global::AlterCourse.AssetCtl.Domain.DomainModels.ProviderInstance provider = configuration.Providers[
            target.ProviderId
        ];
        global::AlterCourse.AssetCtl.Domain.DomainModels.ModelProfile model = provider.Models[target.ModelProfileId];
        global::AlterCourse.AssetCtl.Configuration.ConfigurationTypes.IAdapterDescriptor descriptor =
            adapters.Descriptors[provider.AdapterId];
        List<string> reasons = [];
        AddAvailabilityReasons(configuration, provider, descriptor, lifecycle, reasons);
        AddCapabilityReasons(required, model, descriptor, reasons);
        descriptor.ValidateOptions(model.Options);
        string? outputRejection = descriptor.OutputContractRejection(model, output);
        if (outputRejection is not null)
        {
            reasons.Add($"output-contract-unsupported:{outputRejection}");
        }
        decimal? estimate = model.EstimatedCostPerOutput * candidates;
        AddPolicyReasons(configuration, lifecycle, estimate, reasons);

        return new PlannedTarget(
            route.Id,
            provider.Id,
            model.Id,
            provider.AdapterId,
            reasons.Count == 0,
            reasons,
            estimate,
            model.PricingBasis
        );
    }

    private static void AddAvailabilityReasons(
        EffectiveConfiguration configuration,
        ProviderInstance provider,
        IAdapterDescriptor descriptor,
        AssetLifecycle lifecycle,
        List<string> reasons
    )
    {
        if (!provider.Enabled)
        {
            reasons.Add("provider-disabled");
        }

        if (provider.AllowedLifecycles?.Contains(lifecycle) == false)
        {
            reasons.Add("lifecycle-not-allowed");
        }

        if (descriptor.IsLocalFallback && !configuration.Policy.LocalPlaceholderFallback)
        {
            reasons.Add("local-fallback-disabled");
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

        if (
            lifecycle is AssetLifecycle.Deprecated
            || (lifecycle is AssetLifecycle.Approved && configuration.Policy.ProtectApprovedAssets)
        )
        {
            reasons.Add("protected-lifecycle");
        }
    }

    private static bool Matches(RouteDefinition route, AssetRequest request, IReadOnlyList<AssetCapability> required) =>
        (route.Lifecycle is null || route.Lifecycle == request.Lifecycle)
        && (route.Format is null || route.Format == request.Output.Format)
        && required.Contains(route.Capability);

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
