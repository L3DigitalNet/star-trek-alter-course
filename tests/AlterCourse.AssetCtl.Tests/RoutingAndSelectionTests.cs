using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Routing;
using AssetReference = AlterCourse.AssetCtl.Domain.DomainModels.AssetReference;
using GenerationPlan = AlterCourse.AssetCtl.Domain.DomainModels.GenerationPlan;
using RouteFallbackPolicy = AlterCourse.AssetCtl.Domain.DomainModels.RouteFallbackPolicy;
using RouteRetryPolicy = AlterCourse.AssetCtl.Domain.DomainModels.RouteRetryPolicy;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies configuration-only routing, deterministic selection, and prompt stability.</summary>
public sealed class RoutingAndSelectionTests
{
    /// <summary>Routes a new adapter through configured capabilities without provider-name branches.</summary>
    [Fact]
    public void FakeAdapterRoutesWithoutCoreProviderChanges()
    {
        var fake = new FakeGenerator("new-protocol");
        var registry = new AdapterRegistry([fake]);
        var profile = new ModelProfile(
            "configured-model",
            "vendor-model",
            new HashSet<AssetCapability> { AssetCapability.RasterGenerate, AssetCapability.ImageTransparentOutput },
            0m,
            "fixed",
            new Dictionary<string, string>(StringComparer.Ordinal)
        );
        var provider = new ProviderInstance(
            "configuration-only-name",
            fake.AdapterId,
            true,
            null,
            null,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [profile.Id] = profile }
        );
        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetRequest request = TestData.Request();
        global::AlterCourse.AssetCtl.Domain.DomainModels.EffectiveConfiguration configuration = Configuration(
            provider,
            request
        );
        global::AlterCourse.AssetCtl.Domain.DomainModels.GenerationPlan plan = new AssetRouter(registry).Plan(
            configuration,
            request
        );
        Assert.Equal("configuration-only-name", plan.SelectedTarget!.ProviderId);
        Assert.Equal("new-protocol", plan.SelectedTarget.AdapterId);
    }

    /// <summary>Explains every independent reason that makes a configured target ineligible.</summary>
    [Fact]
    public void RouterExplainsDisabledCredentialsCapabilityAndUnknownPrice()
    {
        var fake = new FakeGenerator("adapter");
        var registry = new AdapterRegistry([fake]);
        var profile = new ModelProfile(
            "model",
            "vendor",
            new HashSet<AssetCapability> { AssetCapability.RasterGenerate },
            null,
            "unknown",
            new Dictionary<string, string>(StringComparer.Ordinal)
        );
        var provider = new ProviderInstance(
            "opaque",
            fake.AdapterId,
            false,
            new Uri("https://example.test/v1"),
            "MISSING_ASSETCTL_TEST_KEY",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [profile.Id] = profile }
        );
        global::AlterCourse.AssetCtl.Domain.DomainModels.GenerationPlan plan = new AssetRouter(registry).Plan(
            Configuration(provider, TestData.Request()),
            TestData.Request()
        );
        IReadOnlyList<string> reasons = Assert.Single(plan.Targets).RejectionReasons;
        Assert.Contains("provider-disabled", reasons, StringComparer.Ordinal);
        Assert.Contains("external-generation-disabled", reasons, StringComparer.Ordinal);
        Assert.Contains("credential-missing:MISSING_ASSETCTL_TEST_KEY", reasons, StringComparer.Ordinal);
        Assert.Contains("model-capability-mismatch", reasons, StringComparer.Ordinal);
        Assert.Contains("unknown-price", reasons, StringComparer.Ordinal);
    }

    /// <summary>Keeps an unknown-price route usable only when the tracked policy explicitly accepts that risk.</summary>
    [Fact]
    public void UnknownPriceAllowKeepsTargetEligibleWithAnExplicitlyUnboundedEstimate()
    {
        var generator = new FakeGenerator("generator");
        AssetRequest request = TestData.Request();
        ProviderInstance provider = Provider("provider", generator.AdapterId, 0m, AssetCapability.RasterGenerate);
        ModelProfile model = provider.Models["profile"] with { EstimatedCostPerOutput = null };
        provider = provider with
        {
            Models = new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [model.Id] = model },
        };
        EffectiveConfiguration configuration = Configuration(provider, request) with
        {
            Policy = new AssetCtlPolicy(false, true, true, true, false, "allow"),
        };

        GenerationPlan plan = new AssetRouter(new AdapterRegistry([generator])).Plan(configuration, request);

        Assert.True(Assert.Single(plan.Targets).Eligible);
        Assert.NotNull(plan.SelectedTarget);
        Assert.Null(plan.EstimatedMaximumCost);
    }

    /// <summary>Selects an independent reviewer before any required-review generation target can spend.</summary>
    [Fact]
    public void RequiredReviewRejectsGeneratorFamilyConflictsBeforeSelection()
    {
        var sameFamilyGenerator = new FakeGenerator("openai-images");
        var independentGenerator = new FakeGenerator("recraft-images");
        var reviewer = new FakeReviewer("openai-vision-review");
        var registry = new AdapterRegistry([sameFamilyGenerator, independentGenerator, reviewer]);
        AssetRequest request = TestData.Request() with { Lifecycle = AssetLifecycle.Candidate };
        ProviderInstance sameFamily = Provider(
            "first",
            sameFamilyGenerator.AdapterId,
            0.10m,
            AssetCapability.RasterGenerate
        );
        ProviderInstance independent = Provider(
            "second",
            independentGenerator.AdapterId,
            0.10m,
            AssetCapability.RasterGenerate
        );
        ProviderInstance reviewProvider = Provider("review", reviewer.AdapterId, 0.05m, AssetCapability.ReviewSemantic);
        var route = new RouteDefinition(
            "generation",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.RasterGenerate,
            [new RouteTarget(sameFamily.Id, "profile"), new RouteTarget(independent.Id, "profile")],
            0
        );
        var reviewRoute = new RouteDefinition(
            "review",
            100,
            null,
            null,
            AssetCapability.ReviewSemantic,
            [new RouteTarget(reviewProvider.Id, "profile")],
            0
        );
        var providers = new Dictionary<string, ProviderInstance>(StringComparer.Ordinal)
        {
            [sameFamily.Id] = sameFamily,
            [independent.Id] = independent,
            [reviewProvider.Id] = reviewProvider,
        };

        GenerationPlan plan = new AssetRouter(registry).Plan(
            Configuration(providers, request, route, reviewRoute),
            request
        );

        Assert.Equal("second", plan.SelectedTarget!.ProviderId);
        Assert.Equal("review", plan.Reviewer!.ProviderId);
        Assert.Contains(
            "reviewer-family-conflict",
            plan.Targets.Single(target =>
                string.Equals(target.ProviderId, "first", StringComparison.Ordinal)
            ).RejectionReasons,
            StringComparer.Ordinal
        );
    }

    /// <summary>Fails a required-review plan before generation when no independent reviewer family exists.</summary>
    [Fact]
    public void RequiredReviewFailsPlanWhenEveryReviewerSharesTheGeneratorFamily()
    {
        var generator = new FakeGenerator("openai-images");
        var reviewer = new FakeReviewer("openai-vision-review");
        AssetRequest request = TestData.Request() with { Lifecycle = AssetLifecycle.Candidate };
        ProviderInstance provider = Provider("generator", generator.AdapterId, 0.10m, AssetCapability.RasterGenerate);
        ProviderInstance reviewProvider = Provider("review", reviewer.AdapterId, 0.05m, AssetCapability.ReviewSemantic);
        var route = new RouteDefinition(
            "generation",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.RasterGenerate,
            [new RouteTarget(provider.Id, "profile")],
            0
        );
        var reviewRoute = new RouteDefinition(
            "review",
            100,
            null,
            null,
            AssetCapability.ReviewSemantic,
            [new RouteTarget(reviewProvider.Id, "profile")],
            0
        );
        var providers = new Dictionary<string, ProviderInstance>(StringComparer.Ordinal)
        {
            [provider.Id] = provider,
            [reviewProvider.Id] = reviewProvider,
        };

        GenerationPlan plan = new AssetRouter(new AdapterRegistry([generator, reviewer])).Plan(
            Configuration(providers, request, route, reviewRoute),
            request
        );

        Assert.Null(plan.SelectedTarget);
        Assert.Null(plan.Reviewer);
        Assert.Contains(
            "independent-reviewer-unavailable",
            Assert.Single(plan.Targets).RejectionReasons,
            StringComparer.Ordinal
        );
    }

    /// <summary>Projects an offline plan before cost aggregation so unreachable external spend is excluded.</summary>
    [Fact]
    public void OfflinePlanExcludesExternalTargetsAndTheirCost()
    {
        var external = new FakeGenerator("external");
        var local = new FakeGenerator("local");
        AssetRequest request = TestData.Request();
        ProviderInstance externalProvider = Provider(
            "external",
            external.AdapterId,
            0.90m,
            AssetCapability.RasterGenerate
        ) with
        {
            Endpoint = new Uri("https://provider.example/v1"),
        };
        ProviderInstance localProvider = Provider("local", local.AdapterId, 0m, AssetCapability.RasterGenerate);
        var route = new RouteDefinition(
            "generation",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.RasterGenerate,
            [new RouteTarget(externalProvider.Id, "profile"), new RouteTarget(localProvider.Id, "profile")],
            0
        );
        var providers = new Dictionary<string, ProviderInstance>(StringComparer.Ordinal)
        {
            [externalProvider.Id] = externalProvider,
            [localProvider.Id] = localProvider,
        };
        EffectiveConfiguration configuration = Configuration(
            providers,
            request,
            route,
            new RouteDefinition("review", 1, null, null, AssetCapability.ReviewSemantic, [], 0)
        ) with
        {
            QualityTiers = new Dictionary<string, QualityTier>(StringComparer.Ordinal)
            {
                [request.QualityTier] = new(request.QualityTier, 1, 1, "disabled", true, 0),
            },
        };

        GenerationPlan plan = new AssetRouter(new AdapterRegistry([external, local])).Plan(
            configuration,
            request,
            offline: true
        );

        Assert.Equal("local", plan.SelectedTarget!.ProviderId);
        Assert.Equal(0m, plan.EstimatedMaximumCost);
        Assert.Contains(
            "offline-external-target",
            plan.Targets.Single(target =>
                string.Equals(target.ProviderId, "external", StringComparison.Ordinal)
            ).RejectionReasons,
            StringComparer.Ordinal
        );
    }

    /// <summary>Filters failed candidates and resolves score ties by creation order.</summary>
    [Fact]
    public void CandidateSelectionFiltersHardFailuresAndBreaksTiesByCreationOrder()
    {
        byte[] bytes = new byte[] { 1 };
        var mechanical = new MechanicalValidationResult(
            true,
            "image/png",
            1,
            1,
            true,
            [],
            bytes,
            new Dictionary<int, byte[]>(0)
        );
        global::AlterCourse.AssetCtl.Domain.DomainModels.SemanticReviewResult review = Review(0.9);
        (
            global::AlterCourse.AssetCtl.Domain.DomainModels.GeneratedCandidate Candidate,
            global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult Mechanical,
            global::AlterCourse.AssetCtl.Domain.DomainModels.SemanticReviewResult? Review
        ) selected = CandidateSelector.Select(
            [
                (new GeneratedCandidate(2, bytes, "image/png", null, 0), mechanical, review),
                (new GeneratedCandidate(1, bytes, "image/png", null, 0), mechanical, review),
                (
                    new GeneratedCandidate(0, bytes, "image/png", null, 0),
                    mechanical with
                    {
                        Passed = false,
                    },
                    Review(1.0)
                ),
            ],
            new QualityTier("test", 3, 1, "required", false, 0.8)
        );
        Assert.Equal(1, selected.Candidate.CreationOrder);
    }

    /// <summary>Rejects a semantically valid candidate whose score is below the tier threshold.</summary>
    [Fact]
    public void CandidateSelectionEnforcesMinimumSemanticScore()
    {
        byte[] bytes = [1];
        var mechanical = new MechanicalValidationResult(
            true,
            "image/png",
            1,
            1,
            true,
            [],
            bytes,
            new Dictionary<int, byte[]>(0)
        );
        Assert.Throws<AssetCtlException>(() =>
            CandidateSelector.Select(
                [(new GeneratedCandidate(0, bytes, "image/png", null, 0), mechanical, Review(0.79))],
                new QualityTier("test", 1, 1, "required", false, 0.80)
            )
        );
    }

    /// <summary>Includes every reachable retry, fallback target, candidate, and reviewer attempt in plan cost.</summary>
    [Fact]
    public void PlanCostConservativelyIncludesFallbacksRetriesAndReviews()
    {
        var first = new FakeGenerator("first");
        var second = new FakeGenerator("second");
        var reviewer = new FakeReviewer();
        var registry = new AdapterRegistry([first, second, reviewer]);
        AssetRequest request = TestData.Request();
        ProviderInstance firstProvider = Provider(
            "first-provider",
            first.AdapterId,
            0.10m,
            AssetCapability.RasterGenerate
        );
        ProviderInstance secondProvider = Provider(
            "second-provider",
            second.AdapterId,
            0.10m,
            AssetCapability.RasterGenerate
        );
        ProviderInstance reviewProvider = Provider(
            "review-provider",
            reviewer.AdapterId,
            0.05m,
            AssetCapability.ReviewSemantic
        );
        var retry = new RouteRetryPolicy(2, 0, 0, 0, new HashSet<ProviderErrorCategory>());
        var fallback = new RouteFallbackPolicy(true, new HashSet<ProviderErrorCategory>());
        var route = new RouteDefinition(
            "generation",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.RasterGenerate,
            [new RouteTarget(firstProvider.Id, "profile"), new RouteTarget(secondProvider.Id, "profile")],
            0,
            fallback,
            retry
        );
        var reviewRoute = new RouteDefinition(
            "review",
            100,
            null,
            null,
            AssetCapability.ReviewSemantic,
            [new RouteTarget(reviewProvider.Id, "profile")],
            0,
            fallback,
            retry
        );
        var providers = new Dictionary<string, ProviderInstance>(StringComparer.Ordinal)
        {
            [firstProvider.Id] = firstProvider,
            [secondProvider.Id] = secondProvider,
            [reviewProvider.Id] = reviewProvider,
        };
        EffectiveConfiguration configuration = Configuration(providers, request, route, reviewRoute);

        GenerationPlan plan = new AssetRouter(registry).Plan(configuration, request);

        Assert.Equal(1.50m, plan.EstimatedMaximumCost);
    }

    /// <summary>Keeps prompt ordering and its content hash deterministic.</summary>
    [Fact]
    public void PromptOrderAndHashAreStable()
    {
        var style = new StyleProfile("style", "summary", ["style required"], ["style prohibited"]);
        AssetRequest request = TestData.Request() with
        {
            References =
            [
                new AssetReference("references/z.png", new string('b', 64), "project-original"),
                new AssetReference("references/a.png", new string('a', 64), "CC-BY-4.0"),
            ],
        };
        (string Prompt, string Hash) first = PromptCompiler.Compile(request, style);
        (string Prompt, string Hash) second = PromptCompiler.Compile(request, style);
        Assert.Equal(first, second);
        string[] ordered =
        [
            "Identity:",
            "Purpose:",
            "Visual kind:",
            "Output contract:",
            "Resolved style summary:",
            "Required constraints:",
            "Prohibited content:",
            "Dimensions:",
            "Target display sizes:",
            "Reference instructions:",
            "Hard technical constraints:",
            "Lifecycle reminder:",
        ];
        int previous = -1;
        foreach (string heading in ordered)
        {
            int current = first.Prompt.IndexOf(heading, StringComparison.Ordinal);
            Assert.True(current > previous, $"{heading} was missing or out of order");
            previous = current;
        }
        Assert.True(
            first.Prompt.IndexOf("references/a.png", StringComparison.Ordinal)
                < first.Prompt.IndexOf("references/z.png", StringComparison.Ordinal)
        );
        Assert.Contains("simple silhouette", first.Prompt, StringComparison.Ordinal);
        Assert.Contains("watermark", first.Prompt, StringComparison.Ordinal);
        Assert.Contains("transparency required", first.Prompt, StringComparison.Ordinal);
        Assert.EndsWith(
            "Lifecycle reminder: placeholder assets must remain functionally clear rather than polished.\nPrompt contract version: 2",
            first.Prompt,
            StringComparison.Ordinal
        );
        Assert.Equal(
            ordered,
            first
                .Prompt.Split('\n')
                .Where(line => ordered.Any(heading => line.StartsWith(heading, StringComparison.Ordinal)))
                .Select(line => ordered.Single(heading => line.StartsWith(heading, StringComparison.Ordinal))),
            StringComparer.Ordinal
        );
    }

    /// <summary>Uses canonical LF bytes for prompts even when the host platform newline differs.</summary>
    [Fact]
    public void PromptUsesCanonicalLineFeedSeparators()
    {
        (string prompt, string hash) = PromptCompiler.Compile(
            TestData.Request(),
            new StyleProfile("style", "summary", [], [])
        );

        Assert.DoesNotContain('\r', prompt);
        Assert.Equal(ConfigurationLoader.Hash(prompt), hash);
    }

    /// <summary>Appends deterministic capability matches without duplicating explicit route entries.</summary>
    [Fact]
    public void CapabilityFallbackDiscoversEligibleProfilesAfterExplicitTargets()
    {
        var explicitAdapter = new FakeGenerator("explicit-adapter");
        var discoveredAdapter = new FakeGenerator("discovered-adapter");
        var registry = new AdapterRegistry([explicitAdapter, discoveredAdapter]);
        AssetRequest request = TestData.Request();
        ProviderInstance explicitProvider = Provider(
            "z-explicit",
            explicitAdapter.AdapterId,
            0m,
            AssetCapability.RasterGenerate
        );
        ProviderInstance discoveredProvider = Provider(
            "a-discovered",
            discoveredAdapter.AdapterId,
            0m,
            AssetCapability.RasterGenerate
        );
        var route = new RouteDefinition(
            "generation",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.RasterGenerate,
            [new RouteTarget(explicitProvider.Id, "profile")],
            0,
            new RouteFallbackPolicy(true, new HashSet<ProviderErrorCategory>())
        );
        var providers = new Dictionary<string, ProviderInstance>(StringComparer.Ordinal)
        {
            [explicitProvider.Id] = explicitProvider,
            [discoveredProvider.Id] = discoveredProvider,
        };

        GenerationPlan plan = new AssetRouter(registry).Plan(
            Configuration(
                providers,
                request,
                route,
                new RouteDefinition("review", 1, null, null, AssetCapability.ReviewSemantic, [], 0)
            ),
            request
        );

        Assert.Equal(
            ["z-explicit", "a-discovered"],
            plan.Targets.Select(target => target.ProviderId),
            StringComparer.Ordinal
        );
        Assert.Equal(2, plan.Targets.Select(target => (target.ProviderId, target.ModelProfileId)).Distinct().Count());
    }

    /// <summary>Uses capability fallback for review routes after their explicit targets.</summary>
    [Fact]
    public void ReviewerCapabilityFallbackDiscoversEligibleReviewer()
    {
        var generator = new FakeGenerator("generator");
        var reviewer = new FakeReviewer();
        var registry = new AdapterRegistry([generator, reviewer]);
        AssetRequest request = TestData.Request();
        ProviderInstance generatorProvider = Provider(
            "generator",
            generator.AdapterId,
            0m,
            AssetCapability.RasterGenerate
        );
        ProviderInstance reviewerProvider = Provider(
            "reviewer",
            reviewer.AdapterId,
            0m,
            AssetCapability.ReviewSemantic
        );
        var route = new RouteDefinition(
            "generation",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.RasterGenerate,
            [new RouteTarget(generatorProvider.Id, "profile")],
            0
        );
        var reviewRoute = new RouteDefinition(
            "review",
            100,
            null,
            null,
            AssetCapability.ReviewSemantic,
            [],
            0,
            new RouteFallbackPolicy(true, new HashSet<ProviderErrorCategory>())
        );
        var providers = new Dictionary<string, ProviderInstance>(StringComparer.Ordinal)
        {
            [generatorProvider.Id] = generatorProvider,
            [reviewerProvider.Id] = reviewerProvider,
        };

        GenerationPlan plan = new AssetRouter(registry).Plan(
            Configuration(providers, request, route, reviewRoute),
            request
        );

        Assert.Equal("reviewer", plan.Reviewer!.ProviderId);
        Assert.Equal("profile", plan.Reviewer.ModelProfileId);
    }

    /// <summary>Excludes a route whose declared operation is not required by the request.</summary>
    [Fact]
    public void RouteCapabilityParticipatesInRequestMatching()
    {
        var generator = new FakeGenerator("generator");
        ProviderInstance provider = Provider("provider", generator.AdapterId, 0m, AssetCapability.RasterGenerate);
        AssetRequest request = TestData.Request();
        var unrelatedRoute = new RouteDefinition(
            "unrelated",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.ImageVectorize,
            [new RouteTarget(provider.Id, "profile")],
            0
        );

        GenerationPlan plan = new AssetRouter(new AdapterRegistry([generator])).Plan(
            Configuration(
                new Dictionary<string, ProviderInstance>(StringComparer.Ordinal) { [provider.Id] = provider },
                request,
                unrelatedRoute,
                new RouteDefinition("review", 1, null, null, AssetCapability.ReviewSemantic, [], 0)
            ) with
            {
                QualityTiers = new Dictionary<string, QualityTier>(StringComparer.Ordinal)
                {
                    [request.QualityTier] = new(request.QualityTier, 1, 1, "disabled", true, 0),
                },
            },
            request
        );

        Assert.Empty(plan.Targets);
        Assert.Null(plan.SelectedTarget);
    }

    /// <summary>Honors the owner policy that disables the deterministic local fallback.</summary>
    [Fact]
    public void LocalFallbackPolicyDisablesLocalAdapter()
    {
        var generator = new LocalPlaceholderGenerator();
        AssetRequest request = TestData.Request();
        ProviderInstance provider = Provider(
            "configured-local",
            generator.AdapterId,
            0m,
            AssetCapability.RasterGenerate
        );
        EffectiveConfiguration configuration = Configuration(provider, request) with
        {
            Policy = new AssetCtlPolicy(false, true, false, true, false, "reject"),
        };

        GenerationPlan plan = new AssetRouter(new AdapterRegistry([generator])).Plan(configuration, request);

        Assert.Null(plan.SelectedTarget);
        Assert.Contains(
            "local-fallback-disabled",
            Assert.Single(plan.Targets).RejectionReasons,
            StringComparer.Ordinal
        );
    }

    /// <summary>Lets the protection switch, rather than a hard-coded lifecycle branch, control approved generation.</summary>
    [Fact]
    public void ApprovedProtectionPolicyCanBeExplicitlyDisabled()
    {
        var generator = new FakeGenerator("generator");
        AssetRequest request = TestData.Request() with { Lifecycle = AssetLifecycle.Approved };
        ProviderInstance provider = Provider("provider", generator.AdapterId, 0m, AssetCapability.RasterGenerate);
        EffectiveConfiguration configuration = Configuration(provider, request) with
        {
            Policy = new AssetCtlPolicy(false, false, true, true, false, "reject"),
        };

        GenerationPlan plan = new AssetRouter(new AdapterRegistry([generator])).Plan(configuration, request);

        Assert.NotNull(plan.SelectedTarget);
        Assert.DoesNotContain(
            "protected-lifecycle",
            Assert.Single(plan.Targets).RejectionReasons,
            StringComparer.Ordinal
        );
    }

    /// <summary>Enforces configured lifecycle permissions for capability-discovered providers.</summary>
    [Fact]
    public void ProviderLifecyclePermissionsRejectCandidateFallback()
    {
        var generator = new LocalPlaceholderGenerator();
        AssetRequest request = TestData.Request() with { Lifecycle = AssetLifecycle.Candidate };
        ProviderInstance provider = Provider(
            "configured-local",
            generator.AdapterId,
            0m,
            AssetCapability.RasterGenerate
        ) with
        {
            AllowedLifecycles = new HashSet<AssetLifecycle> { AssetLifecycle.Placeholder },
        };

        GenerationPlan plan = new AssetRouter(new AdapterRegistry([generator])).Plan(
            Configuration(provider, request),
            request
        );

        Assert.Null(plan.SelectedTarget);
        Assert.Contains("lifecycle-not-allowed", Assert.Single(plan.Targets).RejectionReasons, StringComparer.Ordinal);
    }

    /// <summary>Retains an affordable paid prefix and a free fallback instead of rejecting the entire plan.</summary>
    [Fact]
    public void FreeFallbackKeepsConservativePlanWithinRunBudget()
    {
        AssetRequest request = TestData.Request();
        (EffectiveConfiguration configuration, AdapterRegistry registry) = FreeFallbackConfiguration(request);

        GenerationPlan plan = new AssetRouter(registry).Plan(configuration, request);

        Assert.Equal("first-provider", plan.SelectedTarget!.ProviderId);
        Assert.Equal(0.80m, plan.EstimatedMaximumCost);
        Assert.Contains(
            "aggregate-over-budget",
            plan.Targets.Single(target =>
                string.Equals(target.ProviderId, "second-provider", StringComparison.Ordinal)
            ).RejectionReasons,
            StringComparer.Ordinal
        );
        Assert.True(
            plan.Targets.Single(target =>
                string.Equals(target.ProviderId, "free-provider", StringComparison.Ordinal)
            ).Eligible
        );
    }

    private static (EffectiveConfiguration Configuration, AdapterRegistry Registry) FreeFallbackConfiguration(
        AssetRequest request
    )
    {
        IAssetGenerator[] generators =
        [
            new FakeGenerator("first"),
            new FakeGenerator("second"),
            new LocalPlaceholderGenerator(),
        ];
        ProviderInstance[] providers =
        [
            Provider("first-provider", generators[0].AdapterId, 0.40m, AssetCapability.RasterGenerate),
            Provider("second-provider", generators[1].AdapterId, 0.40m, AssetCapability.RasterGenerate),
            Provider("free-provider", generators[2].AdapterId, 0m, AssetCapability.RasterGenerate),
        ];
        var route = new RouteDefinition(
            "generation",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.RasterGenerate,
            [
                new RouteTarget(providers[0].Id, "profile"),
                new RouteTarget(providers[1].Id, "profile"),
                new RouteTarget(providers[2].Id, "profile"),
            ],
            0,
            new RouteFallbackPolicy(false, new HashSet<ProviderErrorCategory>()),
            new RouteRetryPolicy(2, 0, 0, 0, new HashSet<ProviderErrorCategory>())
        );
        EffectiveConfiguration configuration = Configuration(
            providers.ToDictionary(provider => provider.Id, StringComparer.Ordinal),
            request,
            route,
            new RouteDefinition("review", 1, null, null, AssetCapability.ReviewSemantic, [], 0)
        ) with
        {
            Spending = new SpendingLimits(1m, 1m, 1m),
            QualityTiers = new Dictionary<string, QualityTier>(StringComparer.Ordinal)
            {
                [request.QualityTier] = new(request.QualityTier, 1, 1, "disabled", true, 0),
            },
        };

        return (configuration, new AdapterRegistry(generators));
    }

    /// <summary>Carries the configured estimate basis into the routing record.</summary>
    [Fact]
    public void PlannedTargetRecordsPricingBasis()
    {
        var generator = new FakeGenerator("generator");
        AssetRequest request = TestData.Request();
        ProviderInstance provider = Provider("provider", generator.AdapterId, 0.10m, AssetCapability.RasterGenerate);
        ModelProfile profile = provider.Models["profile"] with { PricingBasis = "quality-and-resolution" };
        provider = provider with
        {
            Models = new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [profile.Id] = profile },
        };

        GenerationPlan plan = new AssetRouter(new AdapterRegistry([generator])).Plan(
            Configuration(provider, request),
            request
        );

        Assert.Equal("quality-and-resolution", Assert.Single(plan.Targets).EstimateBasis);
    }

    private static EffectiveConfiguration Configuration(ProviderInstance provider, AssetRequest request)
    {
        var route = new RouteDefinition(
            "route",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.RasterGenerate,
            [new RouteTarget(provider.Id, provider.Models.Keys.Single())],
            0
        );
        return new EffectiveConfiguration(
            "/repo",
            new AssetCtlPaths("assets", "catalog", "styles", "work", "runs", "state", "logs"),
            new AssetCtlPolicy(false, true, true, true, false, "reject"),
            new AssetCtlLimits(1_000_000, 1_000_000, 10, 10, 10, 30, 1_000_000),
            new SpendingLimits(1, 1, 1),
            new Dictionary<string, ProviderInstance>(StringComparer.Ordinal) { [provider.Id] = provider },
            [route],
            [],
            new Dictionary<string, QualityTier>(StringComparer.Ordinal)
            {
                [request.QualityTier] = new(request.QualityTier, 1, 1, "disabled", true, 0),
            },
            new Dictionary<string, StyleProfile>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal),
            "hash"
        );
    }

    private static EffectiveConfiguration Configuration(
        IReadOnlyDictionary<string, ProviderInstance> providers,
        AssetRequest request,
        RouteDefinition route,
        RouteDefinition reviewRoute
    ) =>
        new(
            "/repo",
            new AssetCtlPaths("assets", "catalog", "styles", "work", "runs", "state", "logs"),
            new AssetCtlPolicy(false, true, true, true, false, "reject"),
            new AssetCtlLimits(1_000_000, 1_000_000, 10, 10, 10, 30, 1_000_000),
            new SpendingLimits(10, 10, 10),
            providers,
            [route],
            [reviewRoute],
            new Dictionary<string, QualityTier>(StringComparer.Ordinal)
            {
                [request.QualityTier] = new(request.QualityTier, 3, 2, "required", false, 0.8),
            },
            new Dictionary<string, StyleProfile>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal),
            "hash"
        );

    private static ProviderInstance Provider(string id, string adapterId, decimal cost, AssetCapability capability)
    {
        var capabilities = new HashSet<AssetCapability> { capability };
        if (capability is AssetCapability.RasterGenerate)
        {
            capabilities.Add(AssetCapability.ImageTransparentOutput);
        }

        var profile = new ModelProfile(
            "profile",
            "vendor",
            capabilities,
            cost,
            "fixed-output",
            new Dictionary<string, string>(StringComparer.Ordinal)
        );
        return new ProviderInstance(
            id,
            adapterId,
            true,
            null,
            null,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [profile.Id] = profile }
        );
    }

    private static SemanticReviewResult Review(double score) =>
        new(true, true, true, true, score, score, [], false, false, score, "pass", "different-provider-family");

    private sealed class FakeGenerator(string id) : IAssetGenerator
    {
        public string AdapterId => id;
        public IReadOnlySet<AssetCapability> SupportedCapabilities { get; } =
            new HashSet<AssetCapability> { AssetCapability.RasterGenerate, AssetCapability.ImageTransparentOutput };

        public IReadOnlySet<string> AllowedEndpointHosts { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) { }

        public Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }

    private sealed class FakeReviewer(string adapterId = "reviewer")
        : AlterCourse.AssetCtl.Providers.ProviderContracts.IAssetReviewer
    {
        public string AdapterId { get; } = adapterId;

        public IReadOnlySet<AssetCapability> SupportedCapabilities { get; } =
            new HashSet<AssetCapability> { AssetCapability.ReviewSemantic };

        public IReadOnlySet<string> AllowedEndpointHosts { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) { }

        public Task<SemanticReviewResult> ReviewAsync(
            ProviderExecutionContext context,
            SemanticReviewRequest request,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }
}
