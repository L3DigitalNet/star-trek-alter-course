using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlterCourse.AssetCtl.Cli;
using AlterCourse.AssetCtl.Providers;
using AlterCourse.AssetCtl.Routing;
using AlterCourse.AssetCtl.Validation;
using SkiaSharp;
using RouteRetryPolicy = AlterCourse.AssetCtl.Domain.DomainModels.RouteRetryPolicy;
using SemanticReviewPolicy = AlterCourse.AssetCtl.Domain.DomainModels.SemanticReviewPolicy;

namespace AlterCourse.AssetCtl.Generation;

internal sealed class GenerationOrchestrator(AdapterRegistry adapters, AssetRouter router)
{
    public async Task<object> GenerateAsync(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        bool force,
        bool dryRun,
        bool offline,
        CancellationToken cancellationToken
    )
    {
        using var assetLock = AssetLock.Acquire(configuration, manifest.Request.Id);
        manifest = ManifestMutation.ReloadForMutation(configuration, manifest);
        string runId = Guid.NewGuid().ToString();
        var attempts = new List<object>
        {
            new
            {
                event_type = "invocation",
                command = "generate",
                force,
                dry_run = dryRun,
                offline,
            },
        };
        var failureCandidates = new List<ReceiptCandidateEvidence>();
        ValidateGenerationBoundary(configuration, manifest, attempts, failureCandidates, runId);

        object? existingResult = await TryExistingAsync(configuration, manifest, force, cancellationToken)
            .ConfigureAwait(false);
        if (existingResult is not null)
        {
            return existingResult;
        }

        GenerationPlan plan;
        try
        {
            plan = BuildPlan(configuration, manifest.Request, offline);
        }
        catch (Exception exception) when (exception is AssetCtlException or IOException)
        {
            WriteFailureReceipt(configuration, manifest, null, attempts, failureCandidates, runId, exception);
            throw;
        }

        if (dryRun)
        {
            return new { dry_run = true, plan };
        }

        return await GenerateNewAsync(
                configuration,
                manifest,
                plan,
                runId,
                attempts,
                failureCandidates,
                offline,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task<object> GenerateNewAsync(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        GenerationPlan plan,
        string runId,
        List<object> attempts,
        List<ReceiptCandidateEvidence> failureCandidates,
        bool offline,
        CancellationToken cancellationToken
    )
    {
        try
        {
            GenerationInputs inputs = PrepareInputs(configuration, manifest.Request);
            var spend = new SpendGuard(configuration);
            var attemptBudget = new AttemptBudget(configuration.Limits.MaximumTotalAttempts);
            TargetOutcome? outcome = await GenerateFromRoutesAsync(
                    configuration,
                    manifest,
                    plan,
                    inputs.Prompt,
                    inputs.References,
                    inputs.Tier,
                    runId,
                    offline,
                    spend,
                    attemptBudget,
                    attempts,
                    failureCandidates,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (outcome is null)
            {
                throw new AssetCtlException(
                    "Every eligible generation route failed provider, validation, or review policy.",
                    4
                );
            }

            return Publish(
                configuration,
                manifest,
                plan,
                inputs.Tier,
                outcome,
                attempts,
                inputs.Prompt,
                inputs.PromptHash,
                inputs.RequestHash,
                runId,
                plan.EstimatedMaximumCost!.Value
            );
        }
        catch (Exception exception) when (exception is AssetCtlException or ProviderException or IOException)
        {
            WriteFailureReceiptUnlessWritten(
                configuration,
                manifest,
                plan,
                attempts,
                failureCandidates,
                runId,
                exception
            );
            throw;
        }
    }

    private GenerationPlan BuildPlan(EffectiveConfiguration configuration, AssetRequest request, bool offline)
    {
        GenerationPlan plan = router.Plan(configuration, request);
        if (offline)
        {
            plan = plan with
            {
                SelectedTarget = plan.Targets.FirstOrDefault(target =>
                    target.Eligible && configuration.Providers[target.ProviderId].Endpoint is null
                ),
            };
        }

        if (plan.SelectedTarget is null)
        {
            throw new AssetCtlException(
                "No eligible generation target. Inspect assetctl plan for rejection reasons.",
                5
            );
        }

        if (plan.EstimatedMaximumCost is null)
        {
            throw new AssetCtlException("Generation cost cannot be conservatively bounded.", 6);
        }

        if (
            plan.EstimatedMaximumCost > configuration.Spending.PerAssetUsd
            || plan.EstimatedMaximumCost > configuration.Spending.PerRunUsd
        )
        {
            throw new AssetCtlException("Generation plan exceeds the configured spend limit.", 6);
        }

        return plan;
    }

    private static void ValidateGenerationBoundary(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        IReadOnlyList<object> attempts,
        IReadOnlyList<ReceiptCandidateEvidence> candidates,
        string runId
    )
    {
        try
        {
            if (manifest.Request.Lifecycle is AssetLifecycle.Approved or AssetLifecycle.Deprecated)
            {
                throw new AssetCtlException("Approved and deprecated assets cannot be generated or overwritten.", 8);
            }

            // Resolve against the physical configured asset root before any provider or idempotency work.
            PathPolicy.ResolveOutputPath(configuration, manifest.Request.Output.Path, allowMissing: true);
        }
        catch (AssetCtlException exception)
        {
            WriteFailureReceipt(configuration, manifest, null, attempts, candidates, runId, exception);
            throw;
        }
    }

    private static GenerationInputs PrepareInputs(EffectiveConfiguration configuration, AssetRequest request)
    {
        StyleProfile style = configuration.Styles[request.StyleProfile];
        (string prompt, string promptHash) = PromptCompiler.Compile(request, style);
        string requestHash = ConfigurationLoader.Hash(JsonSerializer.Serialize(request, JsonOptions.Stable));
        return new GenerationInputs(
            prompt,
            promptHash,
            requestHash,
            LoadReferences(configuration, request.References),
            configuration.QualityTiers[request.QualityTier]
        );
    }

    private static async Task<object?> TryExistingAsync(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        bool force,
        CancellationToken cancellationToken
    )
    {
        if (manifest.Generation is null || manifest.Integrity is null || force)
        {
            return null;
        }

        try
        {
            ManifestStore.VerifyIntegrity(configuration, manifest);
            byte[] existing = await File.ReadAllBytesAsync(
                    Path.Combine(configuration.RepositoryRoot, manifest.Request.Output.Path),
                    cancellationToken
                )
                .ConfigureAwait(false);
            MechanicalValidationResult validation = MechanicalValidator.Validate(
                manifest.Request,
                existing,
                configuration.Limits.MaximumDownloadBytes,
                configuration.Limits.MaximumDecodedPixels
            );
            StyleProfile style = configuration.Styles[manifest.Request.StyleProfile];
            (_, string promptHash) = PromptCompiler.Compile(manifest.Request, style);
            string requestHash = ConfigurationLoader.Hash(
                JsonSerializer.Serialize(manifest.Request, JsonOptions.Stable)
            );
            QualityTier tier = configuration.QualityTiers[manifest.Request.QualityTier];
            bool reviewCurrent = CurrentReviewSatisfies(configuration, manifest, tier);
            return
                validation.Passed
                && reviewCurrent
                && string.Equals(manifest.Generation.RequestSha256, requestHash, StringComparison.Ordinal)
                && string.Equals(manifest.Generation.PromptSha256, promptHash, StringComparison.Ordinal)
                && string.Equals(
                    manifest.Generation.EffectiveConfigSha256,
                    configuration.EffectiveHash,
                    StringComparison.Ordinal
                )
                ? Result(configuration, manifest, null, existing: true)
                : null;
        }
        catch (AssetCtlException)
        {
            // Integrity drift is not an idempotent hit; generation may repair a mutable placeholder or candidate.
            return null;
        }
    }

    private static bool CurrentReviewSatisfies(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        QualityTier tier
    )
    {
        if (tier.ReviewPolicy is SemanticReviewPolicy.Disabled)
        {
            return true;
        }

        if (manifest.SemanticReview is null)
        {
            return tier.ReviewPolicy is SemanticReviewPolicy.WhenAvailable && tier.AllowUnreviewedPlaceholder;
        }

        SemanticReviewResult review = manifest.SemanticReview;
        if (review.HasHardFailure || review.OverallScore < tier.MinimumSemanticScore)
        {
            return false;
        }

        if (
            manifest.Generation is null
            || review.ReviewerProvider is null
            || review.ReviewerModelProfile is null
            || review.EvidenceSha256 is null
            || !configuration.Providers.TryGetValue(review.ReviewerProvider, out ProviderInstance? provider)
            || !provider.Models.ContainsKey(review.ReviewerModelProfile)
        )
        {
            return false;
        }

        // The tracked manifest retains the all-field digest but only a durable review summary. Request,
        // configuration, output, and score are checked independently; Git plus owner approval trusts the digest.
        return review.EvidenceSha256.Length == 64;
    }

    private async Task<TargetOutcome?> GenerateFromRoutesAsync(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        GenerationPlan plan,
        string prompt,
        IReadOnlyList<(string FileName, string MediaType, byte[] Bytes)> references,
        QualityTier tier,
        string runId,
        bool offline,
        SpendGuard spend,
        AttemptBudget attemptBudget,
        List<object> attempts,
        List<ReceiptCandidateEvidence> failureCandidates,
        CancellationToken cancellationToken
    )
    {
        IEnumerable<PlannedTarget> eligibleTargets = plan.Targets.Where(target =>
            target.Eligible && (!offline || configuration.Providers[target.ProviderId].Endpoint is null)
        );
        foreach (PlannedTarget target in eligibleTargets)
        {
            RouteDefinition route = configuration.Routes.Single(route =>
                string.Equals(route.Id, target.RouteId, StringComparison.Ordinal)
            );
            try
            {
                TargetOutcome outcome = await GenerateTargetAsync(
                        configuration,
                        manifest,
                        plan,
                        prompt,
                        references,
                        tier,
                        runId,
                        offline,
                        target,
                        spend,
                        attemptBudget,
                        attempts,
                        failureCandidates,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                return outcome;
            }
            catch (ProviderException exception)
            {
                RecordFallback(attempts, route, target, exception.Category, exception.Message);
            }
            catch (AssetCtlException exception) when (exception.ExitCode == 1)
            {
                RecordFallback(attempts, route, target, ProviderErrorCategory.Validation, exception.Message);
            }
        }

        return null;
    }

    private static void RecordFallback(
        List<object> events,
        RouteDefinition route,
        PlannedTarget target,
        ProviderErrorCategory category,
        string diagnostic
    )
    {
        events.Add(
            new
            {
                event_type = "target-failure",
                target.ProviderId,
                target.ModelProfileId,
                category,
                diagnostic = SanitizeReceiptDiagnostic(diagnostic),
            }
        );
        if (!AllowsFallback(route, category))
        {
            throw new ProviderException(category, SanitizeReceiptDiagnostic(diagnostic));
        }

        events.Add(
            new
            {
                event_type = "fallback",
                route = route.Id,
                category,
            }
        );
    }

    private async Task<TargetOutcome> GenerateTargetAsync(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        GenerationPlan plan,
        string prompt,
        IReadOnlyList<(string FileName, string MediaType, byte[] Bytes)> references,
        QualityTier tier,
        string runId,
        bool offline,
        PlannedTarget target,
        SpendGuard spend,
        AttemptBudget attemptBudget,
        List<object> attempts,
        List<ReceiptCandidateEvidence> failureCandidates,
        CancellationToken cancellationToken
    )
    {
        ProviderInstance provider = configuration.Providers[target.ProviderId];
        ModelProfile model = provider.Models[target.ModelProfileId];
        ProviderExecutionContext context = CreateContext(configuration, provider, model, runId);
        RouteDefinition route = configuration.Routes.Single(route =>
            string.Equals(route.Id, target.RouteId, StringComparison.Ordinal)
        );
        GenerationBatchResult batch = await InvokeWithRetry(
                adapters.Generator(provider.AdapterId),
                context,
                new NormalizedGenerationRequest(manifest.Request, prompt, plan.CandidateCount, references),
                route,
                spend,
                attemptBudget,
                attempts,
                target,
                cancellationToken
            )
            .ConfigureAwait(false);
        List<(
            GeneratedCandidate Candidate,
            MechanicalValidationResult Mechanical,
            SemanticReviewResult? Review
        )> evaluated = await EvaluateCandidatesAsync(
                configuration,
                manifest.Request,
                plan,
                batch,
                tier,
                offline,
                provider.AdapterId,
                spend,
                attemptBudget,
                attempts,
                failureCandidates,
                cancellationToken
            )
            .ConfigureAwait(false);
        (GeneratedCandidate Candidate, MechanicalValidationResult Mechanical, SemanticReviewResult? Review) selected =
            CandidateSelector.Select(evaluated, tier);
        RecordSelection(attempts, target, evaluated);
        return new TargetOutcome(target, provider, model, batch, evaluated, selected);
    }

    private static ProviderExecutionContext CreateContext(
        EffectiveConfiguration configuration,
        ProviderInstance provider,
        ModelProfile model,
        string runId
    )
    {
        string credential = provider.CredentialEnvironmentVariable is null
            ? string.Empty
            : Environment.GetEnvironmentVariable(provider.CredentialEnvironmentVariable) ?? string.Empty;
        return new ProviderExecutionContext(
            provider,
            model,
            credential,
            configuration.Limits.DefaultHttpTimeoutSeconds,
            configuration.Limits.MaximumDownloadBytes,
            configuration.Limits.MaximumDownloadBytes,
            runId
        );
    }

    private static void RecordSelection(
        List<object> events,
        PlannedTarget target,
        IEnumerable<(
            GeneratedCandidate Candidate,
            MechanicalValidationResult Mechanical,
            SemanticReviewResult? Review
        )> evaluated
    ) =>
        events.Add(
            new
            {
                event_type = "selection",
                target.ProviderId,
                target.ModelProfileId,
                status = "selected",
                candidates = evaluated.Select(item => new
                {
                    item.Candidate.CreationOrder,
                    mechanical = item.Mechanical.Passed,
                    semantic = item.Review?.Decision,
                }),
                selection_reason = "mechanical-pass, semantic-policy-pass, score-descending, readability, creation-order",
            }
        );

    private async Task<
        List<(GeneratedCandidate Candidate, MechanicalValidationResult Mechanical, SemanticReviewResult? Review)>
    > EvaluateCandidatesAsync(
        EffectiveConfiguration configuration,
        AssetRequest request,
        GenerationPlan plan,
        GenerationBatchResult batch,
        QualityTier tier,
        bool offline,
        string generatorAdapterId,
        SpendGuard spend,
        AttemptBudget attemptBudget,
        List<object> attempts,
        List<ReceiptCandidateEvidence> failureCandidates,
        CancellationToken cancellationToken
    )
    {
        List<(
            GeneratedCandidate Candidate,
            MechanicalValidationResult Mechanical,
            SemanticReviewResult? Review
        )> evaluated = [];
        foreach (GeneratedCandidate candidate in batch.Candidates.Take(plan.CandidateCount))
        {
            MechanicalValidationResult mechanical = MechanicalValidator.Validate(
                request,
                candidate.Bytes,
                configuration.Limits.MaximumDownloadBytes,
                configuration.Limits.MaximumDecodedPixels
            );
            SemanticReviewResult? review = null;
            var failureEvidence = new ReceiptCandidateEvidence(candidate, mechanical, null);
            failureCandidates.Add(failureEvidence);
            if (mechanical.Passed && plan.Reviewer is not null && !offline)
            {
                try
                {
                    review = await Review(
                            adapters,
                            configuration,
                            plan.Reviewer,
                            generatorAdapterId,
                            request,
                            mechanical,
                            spend,
                            attemptBudget,
                            attempts,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    failureCandidates[^1] = failureEvidence with { Review = review };
                }
                catch (ProviderException)
                    when (tier.ReviewPolicy is SemanticReviewPolicy.WhenAvailable && tier.AllowUnreviewedPlaceholder)
                {
                    attempts.Add(new { event_type = "review-unavailable", candidate = candidate.CreationOrder });
                }
            }
            evaluated.Add((candidate, mechanical, review));
        }

        return evaluated;
    }

    private static object Publish(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        GenerationPlan plan,
        QualityTier tier,
        TargetOutcome outcome,
        List<object> attempts,
        string prompt,
        string promptHash,
        string requestHash,
        string runId,
        decimal estimatedCostUsd
    )
    {
        (GeneratedCandidate Candidate, MechanicalValidationResult Mechanical, SemanticReviewResult? Review) selected =
            outcome.Selected;
        byte[] bytes = selected.Mechanical.NormalizedBytes;
        IntegrityRecord integrity = new(
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            bytes.LongLength,
            selected.Mechanical.MediaType
        );
        AssetManifest generated = GeneratedManifest(
            configuration,
            manifest,
            tier,
            outcome,
            selected,
            integrity,
            prompt,
            promptHash,
            requestHash,
            runId,
            estimatedCostUsd
        );
        RetainCandidates(configuration, runId, outcome);
        ManifestMutation.EnsureCurrent(configuration, manifest);
        string receipt = PublishAndWriteReceipt(
            configuration,
            generated,
            plan,
            outcome,
            attempts,
            integrity,
            requestHash,
            promptHash,
            runId,
            bytes
        );
        return Result(configuration, generated, receipt, existing: false);
    }

    private static string PublishAndWriteReceipt(
        EffectiveConfiguration configuration,
        AssetManifest generated,
        GenerationPlan plan,
        TargetOutcome outcome,
        List<object> attempts,
        IntegrityRecord integrity,
        string requestHash,
        string promptHash,
        string runId,
        byte[] bytes
    )
    {
        AtomicPublisher.PublicationResult publication;
        try
        {
            publication = AtomicPublisher.Publish(
                configuration,
                generated.Request.Output.Path,
                bytes,
                generated.ManifestPath,
                ManifestStore.Serialize(generated)
            );
        }
        catch (Exception exception) when (exception is AssetCtlException or IOException)
        {
            WriteReceiptWithFallback(
                configuration,
                runId,
                CreateReceipt(
                    configuration,
                    generated,
                    plan,
                    outcome,
                    attempts,
                    integrity,
                    requestHash,
                    promptHash,
                    runId,
                    null,
                    SanitizeReceiptDiagnostic(exception.Message)
                )
            );
            exception.Data["AlterCourse.AssetCtl.ReceiptWritten"] = true;
            throw;
        }

        return WritePublishedReceipt(
            configuration,
            generated,
            plan,
            outcome,
            attempts,
            integrity,
            requestHash,
            promptHash,
            runId,
            publication
        );
    }

    private static string WritePublishedReceipt(
        EffectiveConfiguration configuration,
        AssetManifest generated,
        GenerationPlan plan,
        TargetOutcome outcome,
        List<object> attempts,
        IntegrityRecord integrity,
        string requestHash,
        string promptHash,
        string runId,
        AtomicPublisher.PublicationResult publication
    )
    {
        object receipt = CreateReceipt(
            configuration,
            generated,
            plan,
            outcome,
            attempts,
            integrity,
            requestHash,
            promptHash,
            runId,
            publication,
            null
        );
        try
        {
            return ReceiptWriter.Write(configuration, runId, receipt);
        }
        catch (Exception exception) when (exception is AssetCtlException or IOException)
        {
            // Publication is authoritative once AtomicPublisher returns; a provenance sink failure cannot undo it.
            return WriteFallbackReceipt(
                configuration,
                runId,
                CreateReceipt(
                    configuration,
                    generated,
                    plan,
                    outcome,
                    attempts,
                    integrity,
                    requestHash,
                    promptHash,
                    runId,
                    publication,
                    $"primary receipt write failed: {SanitizeReceiptDiagnostic(exception.Message)}"
                )
            );
        }
    }

    private static AssetManifest GeneratedManifest(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        QualityTier tier,
        TargetOutcome outcome,
        (GeneratedCandidate Candidate, MechanicalValidationResult Mechanical, SemanticReviewResult? Review) selected,
        IntegrityRecord integrity,
        string prompt,
        string promptHash,
        string requestHash,
        string runId,
        decimal estimatedCostUsd
    ) =>
        manifest with
        {
            Revision = manifest.Revision + 1,
            Generation = new GenerationProvenance(
                DateTimeOffset.UtcNow,
                runId,
                outcome.Target.RouteId,
                outcome.Provider.Id,
                outcome.Provider.AdapterId,
                outcome.Model.Id,
                outcome.Model.VendorModel,
                tier.Id,
                prompt,
                promptHash,
                requestHash,
                configuration.EffectiveHash,
                selected.Candidate.ProviderRequestId ?? outcome.Batch.ProviderRequestId,
                estimatedCostUsd,
                selected.Candidate.ActualCostUsd ?? outcome.Batch.ActualCostUsd
            ),
            MechanicalValidation = selected.Mechanical,
            SemanticReview = selected.Review,
            Integrity = integrity,
        };

    private static object CreateReceipt(
        EffectiveConfiguration configuration,
        AssetManifest generated,
        GenerationPlan plan,
        TargetOutcome outcome,
        List<object> attempts,
        IntegrityRecord integrity,
        string requestHash,
        string promptHash,
        string runId,
        AtomicPublisher.PublicationResult? publication,
        string? failure
    ) =>
        new
        {
            command = "generate",
            invocation = attempts.Count == 0 ? null : attempts[0],
            run_id = runId,
            asset_id = generated.Request.Id,
            semantic_request = generated.Request,
            request_sha256 = requestHash,
            prompt_sha256 = promptHash,
            effective_configuration_hash = configuration.EffectiveHash,
            contributing_file_hashes = configuration.FileHashes,
            estimated_cost_basis = plan.Targets.Select(target => new
            {
                target.RouteId,
                target.ProviderId,
                target.ModelProfileId,
                target.EstimatedMaximumCost,
            }),
            estimated_maximum_cost_usd = plan.EstimatedMaximumCost,
            known_actual_cost_usd = outcome.Batch.ActualCostUsd,
            plan,
            attempts,
            candidates = ReceiptCandidates(configuration, runId, outcome),
            selection = new
            {
                candidate = outcome.Selected.Candidate.CreationOrder,
                reason = "mechanical-pass, semantic-policy-pass, score-descending, readability, creation-order",
                selected_sha256 = integrity.Sha256,
            },
            publication = PublicationReceipt(configuration, generated, publication, failure),
            authoritative = false,
        };

    private static object ReceiptCandidates(
        EffectiveConfiguration configuration,
        string runId,
        TargetOutcome outcome
    ) =>
        outcome.Evaluated.Select(item => new
        {
            item.Candidate.CreationOrder,
            temporary_path = ShouldRetain(configuration, outcome, item.Candidate)
                ? CandidatePath(configuration, runId, item.Candidate)
                : null,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(item.Candidate.Bytes)),
            item.Candidate.MediaType,
            item.Candidate.ProviderRequestId,
            item.Candidate.ActualCostUsd,
            mechanical = item.Mechanical,
            semantic = item.Review,
        });

    private static void RetainCandidates(EffectiveConfiguration configuration, string runId, TargetOutcome outcome)
    {
        foreach (
            (
                GeneratedCandidate Candidate,
                MechanicalValidationResult Mechanical,
                SemanticReviewResult? Review
            ) item in outcome.Evaluated.Where(item => ShouldRetain(configuration, outcome, item.Candidate))
        )
        {
            string path = Path.Combine(
                configuration.RepositoryRoot,
                CandidatePath(configuration, runId, item.Candidate)
            );
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, item.Candidate.Bytes);
        }
    }

    private static string CandidatePath(
        EffectiveConfiguration configuration,
        string runId,
        GeneratedCandidate candidate
    )
    {
        string extension = string.Equals(candidate.MediaType, "image/svg+xml", StringComparison.Ordinal)
            ? ".svg"
            : ".png";
        return Path.Combine(configuration.Paths.WorkRoot, runId, $"candidate-{candidate.CreationOrder}{extension}");
    }

    private static object PublicationReceipt(
        EffectiveConfiguration configuration,
        AssetManifest generated,
        AtomicPublisher.PublicationResult? publication,
        string? failure
    ) =>
        new
        {
            published = publication?.Published ?? false,
            recovered_pending_transactions = publication?.RecoveredPendingTransactions ?? 0,
            active_transactions_skipped = publication?.ActiveTransactionsSkipped ?? 0,
            rollback = publication?.Rollback ?? "completed-or-no-change",
            failure,
            repository_path = publication?.Published == true ? generated.Request.Output.Path : null,
            godot_path = publication?.Published == true
                ? CliTypes.ToGodotPath(configuration, generated.Request.Output.Path)
                : null,
            manifest_path = publication?.Published == true ? generated.ManifestPath : null,
        };

    private static bool ShouldRetain(
        EffectiveConfiguration configuration,
        TargetOutcome outcome,
        GeneratedCandidate candidate
    ) =>
        configuration.Policy.RetainUnselectedCandidates
        || candidate.CreationOrder == outcome.Selected.Candidate.CreationOrder;

    private static void WriteFailureReceipt(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        GenerationPlan? plan,
        IReadOnlyList<object> attempts,
        IReadOnlyList<ReceiptCandidateEvidence> candidates,
        string runId,
        Exception exception
    )
    {
        RetainFailureCandidates(configuration, runId, candidates);
        string requestHash = ConfigurationLoader.Hash(JsonSerializer.Serialize(manifest.Request, JsonOptions.Stable));
        object receipt = new
        {
            command = "generate",
            invocation = attempts.Count == 0 ? null : attempts[0],
            run_id = runId,
            asset_id = manifest.Request.Id,
            semantic_request = manifest.Request,
            request_sha256 = requestHash,
            effective_configuration_hash = configuration.EffectiveHash,
            contributing_file_hashes = configuration.FileHashes,
            estimated_cost_basis = plan?.Targets.Select(target => new
            {
                target.RouteId,
                target.ProviderId,
                target.ModelProfileId,
                target.EstimatedMaximumCost,
            }),
            estimated_maximum_cost_usd = plan?.EstimatedMaximumCost,
            plan,
            attempts,
            candidates = FailureReceiptCandidates(configuration, runId, candidates),
            selection = new
            {
                status = "failure",
                reason = SanitizeReceiptDiagnostic(exception.Message),
                candidate = (int?)null,
                selected_sha256 = (string?)null,
            },
            publication = new
            {
                published = false,
                recovered_pending_transactions = 0,
                active_transactions_skipped = 0,
                rollback = "completed-or-no-change",
                failure = SanitizeReceiptDiagnostic(exception.Message),
                repository_path = (string?)null,
                godot_path = (string?)null,
                manifest_path = (string?)null,
            },
            authoritative = false,
        };
        WriteReceiptWithFallback(configuration, runId, receipt);
    }

    private static void WriteReceiptWithFallback(EffectiveConfiguration configuration, string runId, object receipt)
    {
        try
        {
            ReceiptWriter.Write(configuration, runId, receipt);
        }
        catch (Exception exception) when (exception is AssetCtlException or IOException)
        {
            try
            {
                WriteFallbackReceipt(
                    configuration,
                    runId,
                    new
                    {
                        receipt,
                        primary_receipt_failure = SanitizeReceiptDiagnostic(exception.Message),
                        authoritative = false,
                    }
                );
            }
            catch (Exception fallbackException) when (fallbackException is AssetCtlException or IOException)
            {
                // A provenance sink must not replace the provider, validation, budget, or publication failure.
            }
        }
    }

    private static string WriteFallbackReceipt(EffectiveConfiguration configuration, string runId, object receipt)
    {
        string workRoot = PathPolicy.ResolveUnder(
            configuration.RepositoryRoot,
            configuration.Paths.WorkRoot,
            "work_root",
            allowMissing: true
        );
        string root = Path.Combine(workRoot, "receipt-fallback");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, runId + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(receipt, JsonOptions.Indented), new UTF8Encoding(false));
        return Path.GetRelativePath(configuration.RepositoryRoot, path);
    }

    private static async Task<GenerationBatchResult> InvokeWithRetry(
        IAssetGenerator generator,
        ProviderExecutionContext context,
        NormalizedGenerationRequest request,
        RouteDefinition route,
        SpendGuard spend,
        AttemptBudget attemptBudget,
        List<object> events,
        PlannedTarget target,
        CancellationToken cancellationToken
    )
    {
        RouteRetryPolicy retry =
            route.RetryPolicy ?? new RouteRetryPolicy(1, 0, 0, 0, new HashSet<ProviderErrorCategory>());
        for (int attempt = 1; ; attempt++)
        {
            int physicalAttempt = attemptBudget.Take();
            DateTimeOffset started = DateTimeOffset.UtcNow;
            var stopwatch = global::System.Diagnostics.Stopwatch.StartNew();
            try
            {
                ReserveCost(spend, context.Model, request.CandidateCount, events, physicalAttempt, target);
                GenerationBatchResult result = await generator
                    .GenerateAsync(context, request, cancellationToken)
                    .ConfigureAwait(false);
                stopwatch.Stop();
                RecordGenerationSuccess(events, route, target, physicalAttempt, attempt, started, stopwatch, result);
                return result;
            }
            catch (ProviderException exception)
            {
                stopwatch.Stop();
                RecordAttemptFailure(
                    events,
                    "physical-attempt",
                    route,
                    target,
                    physicalAttempt,
                    attempt,
                    started,
                    stopwatch,
                    exception
                );
                if (
                    attempt >= retry.MaximumAttemptsPerTarget
                    || !exception.Retryable
                    || !retry.ErrorCategories.Contains(exception.Category)
                )
                {
                    throw;
                }

                TimeSpan delay = RetryDelay(retry, exception, context.RunId, attempt);
                events.Add(
                    new
                    {
                        event_type = "retry",
                        physical_attempt = physicalAttempt,
                        route = route.Id,
                        target.ProviderId,
                        target.ModelProfileId,
                        category = exception.Category,
                        delay_milliseconds = (long)delay.TotalMilliseconds,
                    }
                );
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void RecordGenerationSuccess(
        List<object> events,
        RouteDefinition route,
        PlannedTarget target,
        int physicalAttempt,
        int attempt,
        DateTimeOffset started,
        global::System.Diagnostics.Stopwatch stopwatch,
        GenerationBatchResult result
    ) =>
        events.Add(
            new
            {
                event_type = "physical-attempt",
                physical_attempt = physicalAttempt,
                route = route.Id,
                target.ProviderId,
                target.ModelProfileId,
                attempt,
                status = "success",
                started_at = started,
                duration_milliseconds = stopwatch.ElapsedMilliseconds,
                provider_request_id = result.ProviderRequestId,
                known_actual_cost_usd = result.ActualCostUsd,
                candidate_count = result.Candidates.Count,
            }
        );

    private static void RecordAttemptFailure(
        List<object> events,
        string eventType,
        RouteDefinition route,
        PlannedTarget target,
        int physicalAttempt,
        int attempt,
        DateTimeOffset started,
        global::System.Diagnostics.Stopwatch stopwatch,
        ProviderException exception
    ) =>
        events.Add(
            new
            {
                event_type = eventType,
                physical_attempt = physicalAttempt,
                route = route.Id,
                target.ProviderId,
                target.ModelProfileId,
                attempt,
                status = "failure",
                started_at = started,
                duration_milliseconds = stopwatch.ElapsedMilliseconds,
                category = exception.Category,
                diagnostic = SanitizeReceiptDiagnostic(exception.Message),
            }
        );

    private static string SanitizeReceiptDiagnostic(string diagnostic)
    {
        string sanitized = Redactor.Sanitize(diagnostic);
        int query = sanitized.IndexOf('?', StringComparison.Ordinal);
        if (query < 0)
        {
            return sanitized;
        }

        // Provider diagnostics can embed a signed URL inside prose, so whole-value URL redaction is insufficient.
        int end = sanitized.IndexOfAny([' ', '\r', '\n', '"', '\''], query);
        return end < 0 ? sanitized[..query] + "?[REDACTED]" : sanitized[..query] + "?[REDACTED]" + sanitized[end..];
    }

    private static TimeSpan RetryDelay(RouteRetryPolicy retry, ProviderException exception, string runId, int attempt)
    {
        double exponential = retry.InitialDelayMilliseconds * Math.Pow(2, attempt - 1);
        double bounded = Math.Min(exponential, retry.MaximumDelayMilliseconds);
        TimeSpan? retryAfter = ProviderContracts.RetryAfterDelay(exception);
        if (retryAfter is not null)
        {
            bounded = Math.Min(retryAfter.Value.TotalMilliseconds, retry.MaximumDelayMilliseconds);
        }

        if (retry.JitterRatio > 0 && bounded > 0)
        {
            byte[] seed = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{runId}:{attempt}"));
            double unit = BitConverter.ToUInt64(seed, 0) / (double)ulong.MaxValue;
            bounded *= 1 + ((unit * 2) - 1) * retry.JitterRatio;
        }

        return TimeSpan.FromMilliseconds(Math.Clamp(bounded, 0, retry.MaximumDelayMilliseconds));
    }

    private static async Task<SemanticReviewResult> Review(
        AdapterRegistry adapters,
        EffectiveConfiguration configuration,
        PlannedTarget target,
        string generatorAdapterId,
        AssetRequest request,
        MechanicalValidationResult mechanical,
        SpendGuard spend,
        AttemptBudget attemptBudget,
        List<object> events,
        CancellationToken cancellationToken
    )
    {
        global::AlterCourse.AssetCtl.Domain.DomainModels.ProviderInstance provider = configuration.Providers[
            target.ProviderId
        ];
        global::AlterCourse.AssetCtl.Domain.DomainModels.ModelProfile model = provider.Models[target.ModelProfileId];
        ProviderExecutionContext context = CreateContext(configuration, provider, model, Guid.NewGuid().ToString());
        StyleProfile style = configuration.Styles[request.StyleProfile];
        RouteDefinition route = configuration.ReviewRoutes.Single(route =>
            string.Equals(route.Id, target.RouteId, StringComparison.Ordinal)
        );
        SemanticReviewResult result = await InvokeReviewWithRetry(
                adapters.Reviewer(provider.AdapterId),
                context,
                CreateReviewRequest(request, mechanical, style),
                route,
                spend,
                attemptBudget,
                events,
                target,
                cancellationToken
            )
            .ConfigureAwait(false);
        string independence = string.Equals(
            ProviderFamily(provider.AdapterId),
            ProviderFamily(generatorAdapterId),
            StringComparison.Ordinal
        )
            ? "same-provider-family"
            : "different-provider-family";
        result = result with
        {
            Independence = independence,
            ReviewerProvider = provider.Id,
            ReviewerModelProfile = model.Id,
        };
        return result with
        {
            EvidenceSha256 = ReviewEvidence.Compute(
                request,
                mechanical.NormalizedBytes,
                configuration.EffectiveHash,
                provider.Id,
                model.Id,
                result
            ),
        };
    }

    private static SemanticReviewRequest CreateReviewRequest(
        AssetRequest request,
        MechanicalValidationResult mechanical,
        StyleProfile style
    ) =>
        new(
            request,
            mechanical.NormalizedBytes,
            mechanical.MediaType,
            mechanical.TargetPreviews,
            SemanticReviewSchema.Json,
            style.Summary,
            style.Required.Concat(request.Required).ToArray(),
            style.Prohibited.Concat(request.Prohibited).ToArray()
        );

    private static string ProviderFamily(string adapterId)
    {
        int separator = adapterId.IndexOf('-', StringComparison.Ordinal);
        return separator < 0 ? adapterId : adapterId[..separator];
    }

    private static async Task<SemanticReviewResult> InvokeReviewWithRetry(
        IAssetReviewer reviewer,
        ProviderExecutionContext context,
        SemanticReviewRequest request,
        RouteDefinition route,
        SpendGuard spend,
        AttemptBudget attemptBudget,
        List<object> events,
        PlannedTarget target,
        CancellationToken cancellationToken
    )
    {
        RouteRetryPolicy retry =
            route.RetryPolicy ?? new RouteRetryPolicy(1, 0, 0, 0, new HashSet<ProviderErrorCategory>());
        for (int attempt = 1; ; attempt++)
        {
            int physicalAttempt = attemptBudget.Take();
            DateTimeOffset started = DateTimeOffset.UtcNow;
            var stopwatch = global::System.Diagnostics.Stopwatch.StartNew();
            try
            {
                ReserveCost(spend, context.Model, 1, events, physicalAttempt, target);
                SemanticReviewResult result = await reviewer
                    .ReviewAsync(context, request, cancellationToken)
                    .ConfigureAwait(false);
                stopwatch.Stop();
                RecordReviewSuccess(events, route, target, physicalAttempt, attempt, started, stopwatch);
                return result;
            }
            catch (ProviderException exception)
            {
                stopwatch.Stop();
                RecordAttemptFailure(
                    events,
                    "review-attempt",
                    route,
                    target,
                    physicalAttempt,
                    attempt,
                    started,
                    stopwatch,
                    exception
                );
                if (
                    attempt >= retry.MaximumAttemptsPerTarget
                    || !exception.Retryable
                    || !retry.ErrorCategories.Contains(exception.Category)
                )
                {
                    throw;
                }

                TimeSpan delay = RetryDelay(retry, exception, context.RunId, attempt);
                events.Add(
                    new
                    {
                        event_type = "review-retry",
                        physical_attempt = physicalAttempt,
                        route = route.Id,
                        target.ProviderId,
                        target.ModelProfileId,
                        category = exception.Category,
                        delay_milliseconds = (long)delay.TotalMilliseconds,
                    }
                );
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void RecordReviewSuccess(
        List<object> events,
        RouteDefinition route,
        PlannedTarget target,
        int physicalAttempt,
        int attempt,
        DateTimeOffset started,
        global::System.Diagnostics.Stopwatch stopwatch
    ) =>
        events.Add(
            new
            {
                event_type = "review-attempt",
                physical_attempt = physicalAttempt,
                route = route.Id,
                target.ProviderId,
                target.ModelProfileId,
                attempt,
                status = "success",
                started_at = started,
                duration_milliseconds = stopwatch.ElapsedMilliseconds,
            }
        );

    private static bool AllowsFallback(RouteDefinition route, ProviderErrorCategory category) =>
        route.FallbackPolicy is not null && route.FallbackPolicy.AllowedErrorCategories.Contains(category);

    private static void RecordCostReservation(
        List<object> events,
        string eventType,
        int physicalAttempt,
        PlannedTarget target,
        decimal? reservedUsd
    ) =>
        events.Add(
            new
            {
                event_type = eventType,
                physical_attempt = physicalAttempt,
                target.ProviderId,
                target.ModelProfileId,
                reserved_usd = reservedUsd,
            }
        );

    private static void ReserveCost(
        SpendGuard spend,
        ModelProfile model,
        int outputs,
        List<object> events,
        int physicalAttempt,
        PlannedTarget target
    )
    {
        string operation = outputs == 1 ? "semantic review or generation attempt" : "generation attempt";
        spend.Reserve(model.EstimatedCostPerOutput, outputs, operation);
        RecordCostReservation(
            events,
            "cost-reservation",
            physicalAttempt,
            target,
            model.EstimatedCostPerOutput * outputs
        );
    }

    private static void WriteFailureReceiptUnlessWritten(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        GenerationPlan plan,
        IReadOnlyList<object> attempts,
        IReadOnlyList<ReceiptCandidateEvidence> candidates,
        string runId,
        Exception exception
    )
    {
        if (!exception.Data.Contains("AlterCourse.AssetCtl.ReceiptWritten"))
        {
            WriteFailureReceipt(configuration, manifest, plan, attempts, candidates, runId, exception);
        }
    }

    private static IEnumerable<object> FailureReceiptCandidates(
        EffectiveConfiguration configuration,
        string runId,
        IReadOnlyList<ReceiptCandidateEvidence> candidates
    ) =>
        candidates.Select(
            (item, index) =>
                new
                {
                    item.Candidate.CreationOrder,
                    retained_path = configuration.Policy.RetainUnselectedCandidates
                        ? FailureCandidatePath(configuration, runId, item.Candidate, index)
                        : null,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(item.Candidate.Bytes)),
                    item.Candidate.MediaType,
                    item.Candidate.ProviderRequestId,
                    item.Candidate.ActualCostUsd,
                    mechanical = item.Mechanical,
                    semantic = item.Review,
                    score = item.Review?.OverallScore,
                }
        );

    private static void RetainFailureCandidates(
        EffectiveConfiguration configuration,
        string runId,
        IReadOnlyList<ReceiptCandidateEvidence> candidates
    )
    {
        if (!configuration.Policy.RetainUnselectedCandidates)
        {
            return;
        }

        for (int index = 0; index < candidates.Count; index++)
        {
            ReceiptCandidateEvidence item = candidates[index];
            string path = Path.Combine(
                configuration.RepositoryRoot,
                FailureCandidatePath(configuration, runId, item.Candidate, index)
            );
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, item.Candidate.Bytes);
        }
    }

    private static string FailureCandidatePath(
        EffectiveConfiguration configuration,
        string runId,
        GeneratedCandidate candidate,
        int index
    )
    {
        string extension = string.Equals(candidate.MediaType, "image/svg+xml", StringComparison.Ordinal)
            ? ".svg"
            : ".png";
        return Path.Combine(
            configuration.Paths.WorkRoot,
            runId,
            $"failure-candidate-{index}-{candidate.CreationOrder}{extension}"
        );
    }

    private static (string FileName, string MediaType, byte[] Bytes)[] LoadReferences(
        EffectiveConfiguration configuration,
        IReadOnlyList<AssetReference> references
    ) => references.Select(reference => LoadReference(configuration, reference)).ToArray();

    private static (string FileName, string MediaType, byte[] Bytes) LoadReference(
        EffectiveConfiguration configuration,
        AssetReference reference
    )
    {
        string path = PathPolicy.ResolveReferencePath(configuration, reference.Path, allowMissing: false);
        byte[] bytes = File.ReadAllBytes(path);
        if (
            bytes.LongLength > configuration.Limits.MaximumReferenceBytes
            || !string.Equals(
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                reference.Sha256,
                StringComparison.Ordinal
            )
        )
        {
            throw new AssetCtlException($"Reference '{reference.Path}' exceeds limits or has changed.", 1);
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        SKEncodedImageFormat expectedFormat = extension switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            _ => throw new AssetCtlException($"Reference '{reference.Path}' has an unsupported media extension.", 1),
        };
        using var codec = SKCodec.Create(new SKMemoryStream(bytes));
        if (
            codec is null
            || codec.EncodedFormat != expectedFormat
            || codec.Info.AlphaType == SKAlphaType.Unknown
            || !OutputContractPolicy.AllowsDimensions(
                codec.Info.Width,
                codec.Info.Height,
                configuration.Limits.MaximumDecodedPixels
            )
        )
        {
            throw new AssetCtlException($"Reference '{reference.Path}' failed media or dimension policy.", 1);
        }

        var decodeInfo = new SKImageInfo(
            codec.Info.Width,
            codec.Info.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Unpremul
        );
        using var bitmap = new SKBitmap(decodeInfo);
        if (codec.GetPixels(decodeInfo, bitmap.GetPixels()) is not SKCodecResult.Success)
        {
            throw new AssetCtlException($"Reference '{reference.Path}' failed full image decode.", 1);
        }

        string mediaType = expectedFormat == SKEncodedImageFormat.Png ? "image/png" : "image/jpeg";
        return (Path.GetFileName(path), mediaType, bytes);
    }

    private static object Result(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        string? receipt,
        bool existing
    ) =>
        new
        {
            asset_id = manifest.Request.Id,
            repository_path = manifest.Request.Output.Path,
            godot_path = CliTypes.ToGodotPath(configuration, manifest.Request.Output.Path),
            lifecycle = manifest.Request.Lifecycle.ToString().ToLowerInvariant(),
            validation = manifest.MechanicalValidation?.Passed == false ? "fail" : "pass",
            estimated_cost_usd = manifest.Generation?.EstimatedCostUsd ?? 0,
            receipt_path = receipt,
            existing,
        };

    private sealed record TargetOutcome(
        PlannedTarget Target,
        ProviderInstance Provider,
        ModelProfile Model,
        GenerationBatchResult Batch,
        IReadOnlyList<(
            GeneratedCandidate Candidate,
            MechanicalValidationResult Mechanical,
            SemanticReviewResult? Review
        )> Evaluated,
        (GeneratedCandidate Candidate, MechanicalValidationResult Mechanical, SemanticReviewResult? Review) Selected
    );

    private sealed record ReceiptCandidateEvidence(
        GeneratedCandidate Candidate,
        MechanicalValidationResult Mechanical,
        SemanticReviewResult? Review
    );

    private sealed record GenerationInputs(
        string Prompt,
        string PromptHash,
        string RequestHash,
        IReadOnlyList<(string FileName, string MediaType, byte[] Bytes)> References,
        QualityTier Tier
    );

    private sealed class AttemptBudget(int maximumAttempts)
    {
        private int used;

        public int Take()
        {
            // Generation and review share this counter so a fallback tree cannot multiply per-route retry limits.
            if (used >= maximumAttempts)
            {
                throw new AssetCtlException("The global maximum physical-attempt limit was reached.", 4);
            }

            return ++used;
        }
    }
}
