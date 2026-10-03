using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using AlterCourse.AssetCtl.Cli;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Routing;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Exercises the real command handlers and reuse path with isolated, credential-free selections.</summary>
public sealed class SelectedAssetRouteTests
{
    /// <summary>All selected command routes reject an oversized local file before accepting integrity or format.</summary>
    [Theory]
    [InlineData("validate-config")]
    [InlineData("verify")]
    [InlineData("status")]
    [InlineData("approve")]
    public void SelectedCommandRoutesRejectOneByteExcess(string route)
    {
        using var fixture = new LifecycleBoundaryFixture(new byte[9], maximumBytes: 8);
        string manifest = File.ReadAllText(fixture.ManifestPath);

        Assert.Throws<AssetCtlException>(() => InvokeSelectedRoute(route, fixture));

        Assert.Equal(manifest, File.ReadAllText(fixture.ManifestPath));
        if (string.Equals(route, "validate-config", StringComparison.Ordinal))
        {
            Assert.False(Directory.Exists(Path.Combine(fixture.Root, fixture.Configuration.Paths.StateRoot)));
        }
    }

    /// <summary>Format validation still follows byte/integrity admission on the command routes that decode assets.</summary>
    [Theory]
    [InlineData("validate-config")]
    [InlineData("verify")]
    [InlineData("approve")]
    public void AdmittedMalformedSelectionStillFailsMechanicalValidation(string route)
    {
        using var fixture = new LifecycleBoundaryFixture("bad svg"u8.ToArray(), maximumBytes: 8);

        Assert.Throws<AssetCtlException>(() => InvokeSelectedRoute(route, fixture));

        Assert.Equal(
            AssetLifecycle.Candidate,
            ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle
        );
    }

    /// <summary>Approval cannot replace decoded-pixel limits with compressed-byte admission alone.</summary>
    [Fact]
    public void ApprovalPreservesDecodedPixelAdmission()
    {
        using var fixture = new LifecycleBoundaryFixture();
        EffectiveConfiguration constrained = fixture.Configuration with
        {
            Limits = fixture.Configuration.Limits with { MaximumDecodedPixels = 1 },
        };

        Assert.Throws<AssetCtlException>(() =>
            CliTypes.CommandApp.ApproveObserved(constrained, fixture.ApprovalOptions())
        );

        Assert.Equal(
            AssetLifecycle.Candidate,
            ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle
        );
    }

    /// <summary>Recorded length and hash mismatches are both integrity failures after admitted reads.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IntegrityMismatchIsNotAdmitted(bool lengthMismatch)
    {
        using var fixture = new LifecycleBoundaryFixture(new byte[8], maximumBytes: 8);
        AssetManifest changed = fixture.Manifest with
        {
            Integrity = lengthMismatch
                ? fixture.Manifest.Integrity! with
                {
                    ByteLength = 7,
                }
                : fixture.Manifest.Integrity! with
                {
                    Sha256 = new string('a', 64),
                },
        };

        Assert.Throws<AssetCtlException>(() => ManifestStore.VerifyIntegrity(fixture.Configuration, changed));
    }

    /// <summary>Reuse declines an oversized selected file rather than returning an idempotent hit.</summary>
    [Fact]
    public void GenerationReuseRejectsOversizedSelectedBytes()
    {
        using var fixture = new LifecycleBoundaryFixture(new byte[9], maximumBytes: 8);
        AssetManifest manifest = fixture.Manifest with
        {
            Generation = new GenerationProvenance(
                DateTimeOffset.UtcNow,
                "run",
                "route",
                "provider",
                "adapter",
                "profile",
                "model",
                "development",
                "prompt",
                "prompt-hash",
                "request-hash",
                "config-hash",
                null,
                0,
                0
            ),
        };
        MethodInfo method = typeof(GenerationOrchestrator).GetMethod(
            "TryExisting",
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

        object? result = method.Invoke(null, [fixture.Configuration, manifest, false, CancellationToken.None]);

        Assert.Null(result);
    }

    /// <summary>Generation and lifecycle entry points share the catalog lock before their writes.</summary>
    [Fact]
    public async Task SupportedWritersCannotCompeteWithActiveLifecycleCommit()
    {
        using var fixture = new LifecycleBoundaryFixture();
        var registry = new AdapterRegistry([]);
        var generation = new GenerationOrchestrator(registry, new AssetRouter(registry));
        Task<object>? generationAttempt = null;
        Exception? lifecycleFailure = null;
        var observation = new ManifestMutation.Observation(
            BeforeReplacement: (_, _) =>
            {
                lifecycleFailure = Record.Exception(() =>
                    CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions())
                );
                // GenerateAsync reaches the same lock synchronously, so this controlled interleaving
                // needs neither a background thread nor a timing-dependent sleep.
                generationAttempt = generation.GenerateAsync(
                    fixture.Configuration,
                    fixture.Manifest,
                    false,
                    true,
                    true,
                    CancellationToken.None
                );
            }
        );

        _ = CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions(), observation);

        Assert.Equal(7, Assert.IsType<AssetCtlException>(lifecycleFailure).ExitCode);
        AssetCtlException generationFailure = await Assert.ThrowsAsync<AssetCtlException>(() => generationAttempt!);
        Assert.Equal(7, generationFailure.ExitCode);
        Assert.Equal(
            AssetLifecycle.Approved,
            ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle
        );
    }

    /// <summary>Repeat approval is idempotent and an invalid confirmation cannot alter the candidate.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApprovalRepeatAndInvalidConfirmationPreserveSupportedSemantics(bool invalidConfirmation)
    {
        using var fixture = new LifecycleBoundaryFixture();
        if (invalidConfirmation)
        {
            var options = CliOptions.Parse([
                "--asset-id",
                fixture.Manifest.Request.Id,
                "--approved-by",
                "owner",
                "--approval-note",
                "review",
                "--confirm-approved-asset",
                "wrong-id",
            ]);
            Assert.Equal(
                8,
                Assert
                    .Throws<AssetCtlException>(() =>
                        CliTypes.CommandApp.ApproveObserved(fixture.Configuration, options)
                    )
                    .ExitCode
            );
            Assert.Equal(
                AssetLifecycle.Candidate,
                ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle
            );
        }
        else
        {
            _ = CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions());
            string before = File.ReadAllText(fixture.ManifestPath);
            object repeated = CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions());
            Assert.True(
                JsonSerializer.SerializeToElement(repeated, JsonOptions.Stable).GetProperty("unchanged").GetBoolean()
            );
            Assert.Equal(before, File.ReadAllText(fixture.ManifestPath));
        }
    }

    private static object? InvokeSelectedRoute(string route, LifecycleBoundaryFixture fixture)
    {
        if (string.Equals(route, "validate-config", StringComparison.Ordinal))
        {
            CliTypes.CommandApp.ValidateManifestState(fixture.Configuration, fixture.Manifest);
            return null;
        }

        if (string.Equals(route, "approve", StringComparison.Ordinal))
        {
            return CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions());
        }

        string methodName = string.Equals(route, "verify", StringComparison.Ordinal) ? "Verify" : "Status";
        MethodInfo method = typeof(CliTypes.CommandApp).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static
        )!;
        try
        {
            return method.Invoke(
                null,
                string.Equals(route, "verify", StringComparison.Ordinal)
                    ? [fixture.Configuration, CliOptions.Parse(["--asset-id", fixture.Manifest.Request.Id])]
                    : [fixture.Configuration]
            );
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
