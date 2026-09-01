using System.Security.Cryptography;
using System.Text.Json;
using AlterCourse.AssetCtl.Catalog;
using AlterCourse.AssetCtl.Routing;
using AlterCourse.AssetCtl.Validation;

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
        if (manifest.Request.Lifecycle is AssetLifecycle.Approved or AssetLifecycle.Deprecated)
        {
            throw new AssetCtlException("Approved and deprecated assets cannot be generated or overwritten.", 8);
        }

        if (manifest.Generation is not null && manifest.Integrity is not null && !force)
        {
            try
            {
                ManifestStore.VerifyIntegrity(configuration, manifest);
                byte[] existing = await File.ReadAllBytesAsync(
                        Path.Combine(configuration.RepositoryRoot, manifest.Request.Output.Path),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult existingValidation =
                    MechanicalValidator.Validate(
                        manifest.Request,
                        existing,
                        configuration.Limits.MaximumDownloadBytes,
                        configuration.Limits.MaximumDecodedPixels
                    );
                if (
                    existingValidation.Passed
                    && string.Equals(
                        manifest.Generation.EffectiveConfigSha256,
                        configuration.EffectiveHash,
                        StringComparison.Ordinal
                    )
                )
                {
                    return Result(manifest, null, existing: true);
                }
            }
            catch (AssetCtlException)
            {
                // Integrity drift is not an idempotent hit; generation may repair a mutable placeholder or candidate.
            }
        }

        global::AlterCourse.AssetCtl.Domain.DomainModels.GenerationPlan plan = router.Plan(
            configuration,
            manifest.Request
        );
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

        if (dryRun)
        {
            return new { dry_run = true, plan };
        }

        global::AlterCourse.AssetCtl.Domain.DomainModels.StyleProfile style = configuration.Styles[
            manifest.Request.StyleProfile
        ];
        (string prompt, string promptHash) = PromptCompiler.Compile(manifest.Request, style);
        string requestHash = ConfigurationLoader.Hash(JsonSerializer.Serialize(manifest.Request, JsonOptions.Stable));
        string runId = Guid.NewGuid().ToString();
        (string FileName, string MediaType, byte[] Bytes)[] references = manifest
            .Request.References.Select(reference => LoadReference(configuration, reference))
            .ToArray();
        global::AlterCourse.AssetCtl.Domain.DomainModels.QualityTier tier = configuration.QualityTiers[
            manifest.Request.QualityTier
        ];
        (TargetOutcome? outcome, List<object> attempts) = await GenerateFromRoutesAsync(
                configuration,
                manifest,
                plan,
                prompt,
                references,
                tier,
                runId,
                offline,
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

        return Publish(configuration, manifest, plan, tier, outcome, attempts, prompt, promptHash, requestHash, runId);
    }

    private async Task<(TargetOutcome? Outcome, List<object> Attempts)> GenerateFromRoutesAsync(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        GenerationPlan plan,
        string prompt,
        IReadOnlyList<(string FileName, string MediaType, byte[] Bytes)> references,
        QualityTier tier,
        string runId,
        bool offline,
        CancellationToken cancellationToken
    )
    {
        List<object> attempts = [];
        IEnumerable<PlannedTarget> eligibleTargets = plan.Targets.Where(target =>
            target.Eligible && (!offline || configuration.Providers[target.ProviderId].Endpoint is null)
        );
        foreach (PlannedTarget target in eligibleTargets)
        {
            ProviderInstance provider = configuration.Providers[target.ProviderId];
            ModelProfile model = provider.Models[target.ModelProfileId];
            string credential = provider.CredentialEnvironmentVariable is null
                ? string.Empty
                : Environment.GetEnvironmentVariable(provider.CredentialEnvironmentVariable) ?? string.Empty;
            ProviderExecutionContext context = new(
                provider,
                model,
                credential,
                configuration.Limits.DefaultHttpTimeoutSeconds,
                configuration.Limits.MaximumDownloadBytes,
                runId
            );
            try
            {
                GenerationBatchResult batch = await InvokeWithRetry(
                        adapters.Generator(provider.AdapterId),
                        context,
                        new NormalizedGenerationRequest(manifest.Request, prompt, plan.CandidateCount, references),
                        plan.AttemptsPerRoute,
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
                        offline,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                (
                    GeneratedCandidate Candidate,
                    MechanicalValidationResult Mechanical,
                    SemanticReviewResult? Review
                ) selected = CandidateSelector.Select(evaluated, tier);
                attempts.Add(
                    new
                    {
                        target.ProviderId,
                        target.ModelProfileId,
                        status = "selected",
                        candidates = evaluated.Select(item => new
                        {
                            item.Candidate.CreationOrder,
                            mechanical = item.Mechanical.Passed,
                            semantic = item.Review?.Decision,
                        }),
                    }
                );
                return (new TargetOutcome(target, provider, model, batch, selected), attempts);
            }
            catch (ProviderException exception)
            {
                attempts.Add(
                    new
                    {
                        target.ProviderId,
                        target.ModelProfileId,
                        status = "provider-failure",
                        category = exception.Category.ToString(),
                        diagnostic = Redactor.Sanitize(exception.Message),
                    }
                );
            }
            catch (AssetCtlException exception) when (exception.ExitCode == 1)
            {
                attempts.Add(
                    new
                    {
                        target.ProviderId,
                        target.ModelProfileId,
                        status = "validation-failure",
                        diagnostic = Redactor.Sanitize(exception.Message),
                    }
                );
            }
        }

        return (null, attempts);
    }

    private async Task<
        List<(GeneratedCandidate Candidate, MechanicalValidationResult Mechanical, SemanticReviewResult? Review)>
    > EvaluateCandidatesAsync(
        EffectiveConfiguration configuration,
        AssetRequest request,
        GenerationPlan plan,
        GenerationBatchResult batch,
        bool offline,
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
            SemanticReviewResult? review =
                mechanical.Passed && plan.Reviewer is not null && !offline
                    ? await Review(adapters, configuration, plan.Reviewer, request, mechanical, cancellationToken)
                        .ConfigureAwait(false)
                    : null;
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
        string runId
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
        AssetManifest generated = manifest with
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
                outcome.Target.EstimatedMaximumCost ?? 0,
                selected.Candidate.ActualCostUsd ?? outcome.Batch.ActualCostUsd
            ),
            MechanicalValidation = selected.Mechanical,
            SemanticReview = selected.Review,
            Integrity = integrity,
        };
        AtomicPublisher.Publish(
            configuration,
            generated.Request.Output.Path,
            bytes,
            generated.ManifestPath,
            ManifestStore.Serialize(generated)
        );
        string receipt = ReceiptWriter.Write(
            configuration,
            new
            {
                command = "generate",
                run_id = runId,
                asset_id = generated.Request.Id,
                effective_configuration_hash = configuration.EffectiveHash,
                contributing_file_hashes = configuration.FileHashes,
                plan,
                attempts,
                selected = selected.Candidate.CreationOrder,
                published = true,
            }
        );
        return Result(generated, receipt, existing: false);
    }

    private static async Task<GenerationBatchResult> InvokeWithRetry(
        IAssetGenerator generator,
        ProviderExecutionContext context,
        NormalizedGenerationRequest request,
        int maximumAttempts,
        CancellationToken cancellationToken
    )
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await generator.GenerateAsync(context, request, cancellationToken).ConfigureAwait(false);
            }
            catch (ProviderException exception) when (exception.Retryable && attempt < maximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<SemanticReviewResult> Review(
        AdapterRegistry adapters,
        EffectiveConfiguration configuration,
        PlannedTarget target,
        AssetRequest request,
        MechanicalValidationResult mechanical,
        CancellationToken cancellationToken
    )
    {
        global::AlterCourse.AssetCtl.Domain.DomainModels.ProviderInstance provider = configuration.Providers[
            target.ProviderId
        ];
        global::AlterCourse.AssetCtl.Domain.DomainModels.ModelProfile model = provider.Models[target.ModelProfileId];
        string credential =
            Environment.GetEnvironmentVariable(provider.CredentialEnvironmentVariable!)
            ?? throw new AssetCtlException(
                $"Credential variable {provider.CredentialEnvironmentVariable} is absent.",
                3
            );
        ProviderExecutionContext context = new(
            provider,
            model,
            credential,
            configuration.Limits.DefaultHttpTimeoutSeconds,
            configuration.Limits.MaximumDownloadBytes,
            Guid.NewGuid().ToString()
        );
        return await adapters
            .Reviewer(provider.AdapterId)
            .ReviewAsync(
                context,
                new SemanticReviewRequest(
                    request,
                    mechanical.NormalizedBytes,
                    mechanical.MediaType,
                    mechanical.TargetPreviews,
                    SemanticReviewSchema.Json
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static (string FileName, string MediaType, byte[] Bytes) LoadReference(
        EffectiveConfiguration configuration,
        AssetReference reference
    )
    {
        string path = PathPolicy.ResolveUnder(
            configuration.RepositoryRoot,
            reference.Path,
            "reference",
            allowMissing: false
        );
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

        return (
            Path.GetFileName(path),
            Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg",
            bytes
        );
    }

    private static object Result(AssetManifest manifest, string? receipt, bool existing) =>
        new
        {
            asset_id = manifest.Request.Id,
            repository_path = manifest.Request.Output.Path,
            godot_path = "res://" + manifest.Request.Output.Path["src/AlterCourse.Godot/".Length..],
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
        (GeneratedCandidate Candidate, MechanicalValidationResult Mechanical, SemanticReviewResult? Review) Selected
    );
}
