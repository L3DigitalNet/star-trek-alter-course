using System.Text.Json;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Review;
using AlterCourse.AssetCtl.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>
/// Pins the receipt reporting boundary against real filesystem faults through Program/CommandApp dispatch.
/// Permission cases use mode 0555 directories, which deny writes only to a non-root Linux user; DeniedDirectory
/// fails the test instead of skipping when the process can still write, so a root run cannot claim coverage.
/// </summary>
[Collection("Diagnostic process boundary")]
public sealed class ReceiptFailureBoundaryTests
{
    /// <summary>
    /// After a known commit, a denied primary sink still reaches the fallback (exit 0), and two failed sinks report
    /// the commit with exit 9 rather than an unexpected internal failure.
    /// </summary>
    [Theory]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 9)]
    [InlineData(false, true, 9)]
    public async Task PermissionDeniedReceiptPreservesPublication(bool denyPrimary, bool denyFallback, int expectedExit)
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        EffectiveConfiguration configuration = fixture.Configuration();
        using DeniedDirectory? primary = BlockPrimary(fixture, configuration, denyPrimary);
        using DeniedDirectory? fallback = denyFallback
            ? new DeniedDirectory(FallbackRoot(fixture, configuration))
            : null;

        int exit = await Program.RunProcessAsync(Arguments(fixture), (_, _) => NullLoggerFactory.Instance);

        Assert.Equal(expectedExit, exit);
        Assert.NotNull(fixture.Load().Integrity);
        Assert.True(File.Exists(Path.Combine(fixture.Root, fixture.Load().Request.Output.Path)));
        Assert.DoesNotContain("unexpected internal failure", fixture.Error.ToString(), StringComparison.Ordinal);
        if (denyFallback)
        {
            Assert.Empty(fixture.Output.ToString());
            Assert.Contains(
                "mutation committed; receipt-output-unavailable",
                fixture.Error.ToString(),
                StringComparison.Ordinal
            );
        }
        else
        {
            using var result = JsonDocument.Parse(fixture.Output.ToString());
            string path = result.RootElement.GetProperty("receipt_path").GetString()!;
            Assert.StartsWith(
                Path.Combine(configuration.Paths.WorkRoot, "receipt-fallback"),
                path,
                StringComparison.Ordinal
            );
            string receipt = await File.ReadAllTextAsync(Path.Combine(fixture.Root, path));
            Assert.Contains("primary receipt write failed: local-io-failed", receipt, StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.Root, receipt, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A failure receipt that cannot be written, or that only reaches the fallback, never replaces the original
    /// pre-commit refusal. Lifecycle refusal (8) comes from boundary validation and the no-route refusal (5) from plan
    /// construction, so both failure-receipt catch sites are covered.
    /// </summary>
    [Theory]
    [InlineData(true, false, 8)]
    [InlineData(true, true, 8)]
    [InlineData(false, true, 8)]
    [InlineData(true, true, 5)]
    [InlineData(false, true, 5)]
    public async Task FailureReceiptSinkFailurePreservesRefusal(bool denyPrimary, bool denyFallback, int expectedExit)
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        await PrepareRefusal(fixture, expectedExit);
        byte[] before = await File.ReadAllBytesAsync(fixture.ManifestPath);
        EffectiveConfiguration configuration = fixture.Configuration();
        // Calibration: with writable sinks the same invocation must already produce expectedExit, so the faulted run
        // below proves preservation of the genuine refusal rather than of some unrelated earlier failure.
        Assert.Equal(expectedExit, await RunGenerate(fixture));
        Directory.Delete(Path.Combine(fixture.Root, configuration.Paths.ReceiptRoot), recursive: true);
        Assert.False(Directory.Exists(FallbackRoot(fixture, configuration)));
        using DeniedDirectory? primary = BlockPrimary(fixture, configuration, denyPrimary);
        using DeniedDirectory? fallback = denyFallback
            ? new DeniedDirectory(FallbackRoot(fixture, configuration))
            : null;

        int exit = await RunGenerate(fixture);

        Assert.Equal(expectedExit, exit);
        Assert.Equal(before, await File.ReadAllBytesAsync(fixture.ManifestPath));
        Assert.Empty(fixture.Output.ToString());
        Assert.DoesNotContain(fixture.Root, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("unexpected internal failure", fixture.Error.ToString(), StringComparison.Ordinal);
        if (!denyFallback)
        {
            string receipt = await File.ReadAllTextAsync(
                Assert.Single(Directory.GetFiles(FallbackRoot(fixture, configuration), "*.json"))
            );
            Assert.Contains("\"primary_receipt_failure\": \"local-io-failed\"", receipt, StringComparison.Ordinal);
            Assert.Contains("lifecycle-policy-failed", receipt, StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.Root, receipt, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Candidate retention failure keeps the original refusal and still writes the attempt evidence, without claiming
    /// a retained path that was never written.
    /// </summary>
    [Fact]
    public async Task RetentionIoFailurePreservesRefusalAndReceiptEvidence()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        EffectiveConfiguration configuration = fixture.Configuration();
        string receiptRoot = Path.Combine(fixture.Root, configuration.Paths.ReceiptRoot);
        // Calibration: the same invalid candidates without retention yield the genuine refusal and its receipt reason.
        (Exception genuine, string genuineReason) = await RunInvalidCandidates(fixture, configuration, retain: false);
        Directory.Delete(receiptRoot, recursive: true);

        (Exception failure, string reason) = await RunInvalidCandidates(fixture, configuration, retain: true);

        Assert.IsType(genuine.GetType(), failure);
        Assert.Equal((genuine as AssetCtlException)?.ExitCode, (failure as AssetCtlException)?.ExitCode);
        Assert.Equal((genuine as ProviderException)?.Category, (failure as ProviderException)?.Category);
        Assert.Equal(genuineReason, reason);
        Assert.NotEqual("local-io-failed", reason, StringComparer.Ordinal);
        Assert.Null(fixture.Load().Integrity);
        using var parsed = JsonDocument.Parse(
            await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(receiptRoot, "*.json")))
        );
        Assert.True(parsed.RootElement.GetProperty("attempts").GetArrayLength() > 1);
        JsonElement[] candidates = [.. parsed.RootElement.GetProperty("candidates").EnumerateArray()];
        Assert.NotEmpty(candidates);
        Assert.All(
            candidates,
            candidate =>
            {
                Assert.Equal(64, candidate.GetProperty("sha256").GetString()!.Length);
                Assert.Equal(JsonValueKind.Null, candidate.GetProperty("retained_path").ValueKind);
            }
        );
    }

    private static async Task PrepareRefusal(DiagnosticBoundaryRegressionTests.ProcessFixture fixture, int expectedExit)
    {
        if (expectedExit == 8)
        {
            AssetManifest manifest = fixture.Load();
            await File.WriteAllTextAsync(
                    fixture.ManifestPath,
                    ManifestStore.Serialize(
                        manifest with
                        {
                            Request = manifest.Request with { Lifecycle = AssetLifecycle.Deprecated },
                            // A deprecated manifest without its record is refused at load (exit 1), never reaching
                            // the lifecycle boundary this case targets.
                            Deprecation = new DeprecationRecord(
                                "maintainer",
                                new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero),
                                "superseded"
                            ),
                        }
                    )
                )
                .ConfigureAwait(false);
        }
        else
        {
            // Pricing the only offline route above the 0.00 spend limits leaves the router no eligible target.
            string providers = Path.Combine(fixture.Root, "config/assets/providers.yaml");
            string original = await File.ReadAllTextAsync(providers).ConfigureAwait(false);
            string priced = original.Replace(
                "estimated_cost_per_output: 0.00",
                "estimated_cost_per_output: 0.01",
                StringComparison.Ordinal
            );
            Assert.NotEqual(original, priced, StringComparer.Ordinal);
            await File.WriteAllTextAsync(providers, priced).ConfigureAwait(false);
        }
    }

    private static async Task<(Exception Failure, string Reason)> RunInvalidCandidates(
        DiagnosticBoundaryRegressionTests.ProcessFixture fixture,
        EffectiveConfiguration configuration,
        bool retain
    )
    {
        string policy = Path.Combine(fixture.Root, "config/assets/assetctl.yaml");
        string original = await File.ReadAllTextAsync(policy).ConfigureAwait(false);
        string updated = original.Replace(
            $"retain_unselected_candidates: {(!retain).ToString().ToLowerInvariant()}",
            $"retain_unselected_candidates: {retain.ToString().ToLowerInvariant()}",
            StringComparison.Ordinal
        );
        Assert.Contains(
            $"retain_unselected_candidates: {retain.ToString().ToLowerInvariant()}",
            updated,
            StringComparison.Ordinal
        );
        await File.WriteAllTextAsync(policy, updated).ConfigureAwait(false);
        using var client = new HttpClient();
        var registry = new AdapterRegistry([
            new InvalidCandidateGenerator(Path.Combine(fixture.Root, configuration.Paths.WorkRoot)),
            new RecraftImageAdapter(client),
            new OpenAiImageAdapter(client),
            new XaiImageAdapter(client),
            new OpenAiVisionReviewer(client),
        ]);
        var router = new AssetRouter(registry);
        var app = new AlterCourse.AssetCtl.Cli.CliTypes.CommandApp(
            new ConfigurationLoader(registry.Descriptors),
            router,
            new GenerationOrchestrator(registry, router),
            NullLogger<AlterCourse.AssetCtl.Cli.CliTypes.CommandApp>.Instance
        );

        Exception failure = Assert.IsAssignableFrom<Exception>(
            await Record
                .ExceptionAsync(() => app.RunAsync(Arguments(fixture), CancellationToken.None))
                .ConfigureAwait(false)
        );
        string receipt = Assert.Single(
            Directory.GetFiles(Path.Combine(fixture.Root, configuration.Paths.ReceiptRoot), "*.json")
        );
        using var parsed = JsonDocument.Parse(await File.ReadAllTextAsync(receipt).ConfigureAwait(false));
        return (failure, parsed.RootElement.GetProperty("selection").GetProperty("reason").GetString()!);
    }

    /// <summary>
    /// A permission-denied publication before commit is an operation failure: nothing is committed, a failure receipt
    /// records the local I/O class, and the outcome is never the post-commit receipt disposition. The exit code itself
    /// stays with the existing raw local-I/O classification, which this receipt boundary does not own.
    /// </summary>
    [Fact]
    public async Task PermissionDeniedPublicationWritesFailureReceipt()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        EffectiveConfiguration configuration = fixture.Configuration();
        // Deny the existing Godot asset root while the output's own directory is still missing, so the publisher's
        // path-based Directory.CreateDirectory raises UnauthorizedAccessException; denying an existing output
        // directory instead reaches the handle-based admission, which already classifies EACCES as exit 7.
        string outputDirectory = Path.GetDirectoryName(fixture.Load().Request.Output.Path)!;
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, outputDirectory)));
        using var denied = new DeniedDirectory(Path.Combine(fixture.Root, Path.GetDirectoryName(outputDirectory)!));

        int exit = await Program.RunProcessAsync(Arguments(fixture), (_, _) => NullLoggerFactory.Instance);

        Assert.NotEqual(0, exit);
        Assert.NotEqual(9, exit);
        Assert.Null(fixture.Load().Integrity);
        Assert.Empty(fixture.Output.ToString());
        string receipt = await File.ReadAllTextAsync(
            Assert.Single(Directory.GetFiles(Path.Combine(fixture.Root, configuration.Paths.ReceiptRoot), "*.json"))
        );
        using var parsed = JsonDocument.Parse(receipt);
        JsonElement publication = parsed.RootElement.GetProperty("publication");
        Assert.False(publication.GetProperty("published").GetBoolean());
        Assert.Equal("local-io-failed", publication.GetProperty("failure").GetString());
    }

    /// <summary>
    /// A permission-denied step after provider spend but before commit (success-path candidate retention) still
    /// writes the failure receipt listing the billed attempts, classified as local I/O, while the command keeps its
    /// existing exit code 1.
    /// </summary>
    [Fact]
    public async Task PermissionDeniedPostSpendStepRecordsAttempts()
    {
        // Calibration: the same offline run with a writable work root commits (exit 0), so the faulted run below
        // fails only because of the injected denial, after the generation attempt has already been made.
        using (var calibration = new DiagnosticBoundaryRegressionTests.ProcessFixture())
        {
            Assert.Equal(0, await RunGenerate(calibration));
            Assert.NotNull(calibration.Load().Integrity);
        }

        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        EffectiveConfiguration configuration = fixture.Configuration();
        // The work root holds only candidate retention, publication staging, and the receipt fallback; the lock
        // lives under state_root and the primary receipt under receipt_root, so denying it makes the first
        // post-spend write (retaining the selected candidate under <work_root>/<run_id>) raise
        // UnauthorizedAccessException before publication starts, while the primary receipt sink stays writable.
        using var denied = new DeniedDirectory(Path.Combine(fixture.Root, configuration.Paths.WorkRoot));

        int exit = await RunGenerate(fixture);

        Assert.Equal(1, exit);
        Assert.Null(fixture.Load().Integrity);
        Assert.Empty(fixture.Output.ToString());
        string receiptRoot = Path.Combine(fixture.Root, configuration.Paths.ReceiptRoot);
        Assert.True(Directory.Exists(receiptRoot), "No failure receipt was written for the billed attempt.");
        using var parsed = JsonDocument.Parse(
            await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(receiptRoot, "*.json")))
        );
        // attempts[0] is the invocation record; anything beyond it is provider attempt evidence.
        Assert.True(parsed.RootElement.GetProperty("attempts").GetArrayLength() > 1);
        Assert.Equal(
            "local-io-failed",
            parsed.RootElement.GetProperty("publication").GetProperty("failure").GetString()
        );
        Assert.Equal("local-io-failed", parsed.RootElement.GetProperty("selection").GetProperty("reason").GetString());
        Assert.False(parsed.RootElement.GetProperty("publication").GetProperty("published").GetBoolean());
    }

    private static Task<int> RunGenerate(DiagnosticBoundaryRegressionTests.ProcessFixture fixture) =>
        Program.RunProcessAsync(Arguments(fixture), (_, _) => NullLoggerFactory.Instance);

    private static string[] Arguments(DiagnosticBoundaryRegressionTests.ProcessFixture fixture) =>
        ["generate", "--output", "json", "--asset-id", fixture.AssetId, "--offline"];

    private static string FallbackRoot(
        DiagnosticBoundaryRegressionTests.ProcessFixture fixture,
        EffectiveConfiguration configuration
    ) => Path.Combine(fixture.Root, configuration.Paths.WorkRoot, "receipt-fallback");

    // A denied primary exercises UnauthorizedAccessException; otherwise a regular file at the receipt root
    // exercises the IOException class, so each theory covers both exception families on the primary sink.
    private static DeniedDirectory? BlockPrimary(
        DiagnosticBoundaryRegressionTests.ProcessFixture fixture,
        EffectiveConfiguration configuration,
        bool deny
    )
    {
        string primaryPath = Path.Combine(fixture.Root, configuration.Paths.ReceiptRoot);
        if (deny)
        {
            return new DeniedDirectory(primaryPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(primaryPath)!);
        File.WriteAllText(primaryPath, "blocked");
        return null;
    }

    private sealed class DeniedDirectory : IDisposable
    {
        private readonly string _path;
        private readonly UnixFileMode _original;

        public DeniedDirectory(string path)
        {
            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException("Receipt permission regressions require non-root Linux.");
            }
            _path = path;
            Directory.CreateDirectory(path);
            _original = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            try
            {
                // Root bypasses mode bits; failing here keeps a root run from passing without exercising EACCES.
                Assert.Throws<UnauthorizedAccessException>(() =>
                    File.WriteAllText(Path.Combine(path, "permission-probe"), "probe")
                );
            }
            catch
            {
                File.SetUnixFileMode(path, _original);
                throw;
            }
        }

        public void Dispose()
        {
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(_path, _original);
            }
        }
    }

    // Replaces the offline placeholder: emits undecodable bytes so every candidate fails mechanical validation, and
    // places a regular file at <work_root>/<run_id>, the failure-retention directory, so retention fails with
    // IOException. Blocking only that run directory leaves every other work-root user (publication staging, the
    // receipt fallback) intact, so the refusal stays the one the calibration run observes.
    private sealed class InvalidCandidateGenerator(string workRoot) : IAssetGenerator
    {
        private readonly LocalPlaceholderGenerator _descriptor = new();
        public string AdapterId => _descriptor.AdapterId;
        public bool IsLocalFallback => true;
        public IReadOnlySet<AssetCapability> SupportedCapabilities => _descriptor.SupportedCapabilities;

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) =>
            _descriptor.ValidateOptions(options);

        public async Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        )
        {
            Directory.CreateDirectory(workRoot);
            string runDirectory = Path.Combine(workRoot, context.RunId);
            if (!File.Exists(runDirectory))
            {
                await File.WriteAllTextAsync(runDirectory, "blocked retention", cancellationToken)
                    .ConfigureAwait(false);
            }
            return new GenerationBatchResult(
                [new GeneratedCandidate(0, "invalid image"u8.ToArray(), "image/png", null, 0m)],
                null,
                0m
            );
        }
    }
}
