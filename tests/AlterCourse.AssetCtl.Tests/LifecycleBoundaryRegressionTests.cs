using System.Text.Json;
using AlterCourse.AssetCtl.Cli;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Exercises lifecycle evidence and commit outcomes on temporary Linux filesystem objects.</summary>
public sealed class LifecycleBoundaryRegressionTests
{
    /// <summary>A late manifest symlink cannot redirect replacement or change its unrelated target.</summary>
    [Fact]
    public void ManifestSymlinkSubstitutionRefusesWithoutChangingTarget()
    {
        using var fixture = new LifecycleBoundaryFixture();
        string target = Path.Combine(fixture.Root, "manifest-target");
        const string predecessor = "unrelated manifest target";
        File.WriteAllText(target, predecessor);
        var observation = new ManifestMutation.Observation(
            BeforeReplacement: (_, _) =>
            {
                File.Move(fixture.ManifestPath, fixture.ManifestPath + ".previous");
                File.CreateSymbolicLink(fixture.ManifestPath, target);
            }
        );

        Assert.Throws<AssetCtlException>(() =>
            ManifestMutation.WriteCas(
                fixture.Configuration,
                fixture.Manifest,
                fixture.Manifest with
                {
                    Revision = 2,
                },
                observation
            )
        );

        Assert.Equal(predecessor, File.ReadAllText(target));
    }

    /// <summary>Selected-parent substitution cannot approve bytes found through a replacement directory link.</summary>
    [Fact]
    public void SelectedParentSubstitutionRefusesApproval()
    {
        using var fixture = new LifecycleBoundaryFixture();
        string predecessor = File.ReadAllText(fixture.ManifestPath);
        string substitute = Path.Combine(fixture.Root, "replacement-assets");
        Directory.CreateDirectory(substitute);
        File.WriteAllBytes(Path.Combine(substitute, Path.GetFileName(fixture.AssetPath)), fixture.Bytes);
        var observation = new ManifestMutation.Observation(EvidenceValidated: () =>
        {
            string parent = Path.GetDirectoryName(fixture.AssetPath)!;
            Directory.Move(parent, Path.Combine(fixture.Root, "admitted-assets"));
            Directory.CreateSymbolicLink(parent, substitute);
        });

        Assert.Throws<AssetCtlException>(() =>
            CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions(), observation)
        );

        Assert.Equal(predecessor, File.ReadAllText(fixture.ManifestPath));
    }

    /// <summary>Precommit stage faults preserve the predecessor and the primary failure.</summary>
    [Theory]
    [InlineData((int)ManifestMutation.StageOperation.Create, false)]
    [InlineData((int)ManifestMutation.StageOperation.Write, false)]
    [InlineData((int)ManifestMutation.StageOperation.Flush, false)]
    [InlineData((int)ManifestMutation.StageOperation.Close, false)]
    [InlineData((int)ManifestMutation.StageOperation.Replace, false)]
    [InlineData((int)ManifestMutation.StageOperation.Create, true)]
    [InlineData((int)ManifestMutation.StageOperation.Write, true)]
    [InlineData((int)ManifestMutation.StageOperation.Flush, true)]
    [InlineData((int)ManifestMutation.StageOperation.Close, true)]
    [InlineData((int)ManifestMutation.StageOperation.Replace, true)]
    public void StageFaultsPreserveOriginalAndPrimaryFailure(int operation, bool cleanupFails)
    {
        using var fixture = new LifecycleBoundaryFixture();
        string predecessor = File.ReadAllText(fixture.ManifestPath);
        var primary = new IOException("primary stage fault");
        var observation = new ManifestMutation.Observation(StageOperation: point =>
        {
            if ((int)point == operation)
            {
                throw primary;
            }
            if (point == ManifestMutation.StageOperation.Cleanup && cleanupFails)
            {
                throw new IOException("secondary cleanup fault");
            }
        });

        Exception? failure = Record.Exception(() =>
            ManifestMutation.WriteCas(
                fixture.Configuration,
                fixture.Manifest,
                fixture.Manifest with
                {
                    Revision = 2,
                },
                observation
            )
        );

        Assert.Same(primary, failure);
        Assert.Equal(predecessor, File.ReadAllText(fixture.ManifestPath));
        if (!cleanupFails)
        {
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(fixture.ManifestPath)!, "*.assetctl-stage-*"));
        }
    }

    /// <summary>Stage cleanup cannot remove an unrelated leaf substituted by a same-UID actor.</summary>
    [Fact]
    public void CleanupPreservesSubstitutedUnownedStage()
    {
        using var fixture = new LifecycleBoundaryFixture();
        string? unowned = null;
        var primary = new IOException("replacement fault");
        var observation = new ManifestMutation.Observation(
            BeforeReplacement: (stage, _) =>
            {
                File.Move(stage, stage + ".owned");
                File.WriteAllText(stage, "unowned leaf");
                unowned = stage;
                throw primary;
            }
        );

        Exception? failure = Record.Exception(() =>
            ManifestMutation.WriteCas(
                fixture.Configuration,
                fixture.Manifest,
                fixture.Manifest with
                {
                    Revision = 2,
                },
                observation
            )
        );

        Assert.Same(primary, failure);
        Assert.Equal("unowned leaf", File.ReadAllText(unowned!));
        Assert.Equal(1, ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Revision);
    }

    /// <summary>The replacement point cannot overwrite a manifest changed after the earlier CAS evidence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedManifestContentOrLeafRefusesStaleReplacement(bool replaceLeaf)
    {
        using var fixture = new LifecycleBoundaryFixture();
        AssetManifest changed = fixture.Manifest with { Revision = 3 };
        string current = ManifestStore.Serialize(changed);
        var observation = new ManifestMutation.Observation(
            BeforeReplacement: (_, _) =>
            {
                if (replaceLeaf)
                {
                    File.Move(fixture.ManifestPath, fixture.ManifestPath + ".previous");
                }
                File.WriteAllText(fixture.ManifestPath, current);
            }
        );

        Assert.Throws<AssetCtlException>(() =>
            ManifestMutation.WriteCas(
                fixture.Configuration,
                fixture.Manifest,
                fixture.Manifest with
                {
                    Revision = 2,
                },
                observation
            )
        );

        Assert.Equal(current, File.ReadAllText(fixture.ManifestPath));
    }

    /// <summary>Asset identity, not merely matching content, remains bound through approval.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedLeafReplacementOrSymlinkRefusesApproval(bool symbolicLink)
    {
        using var fixture = new LifecycleBoundaryFixture();
        string predecessor = File.ReadAllText(fixture.ManifestPath);
        var observation = new ManifestMutation.Observation(EvidenceValidated: () =>
        {
            string previous = fixture.AssetPath + ".previous";
            File.Move(fixture.AssetPath, previous);
            if (symbolicLink)
            {
                File.CreateSymbolicLink(fixture.AssetPath, previous);
            }
            else
            {
                File.WriteAllBytes(fixture.AssetPath, fixture.Bytes);
            }
        });

        Assert.Throws<AssetCtlException>(() =>
            CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions(), observation)
        );

        Assert.Equal(predecessor, File.ReadAllText(fixture.ManifestPath));
    }

    /// <summary>Reporting failure after replacement exposes commitment and does not claim rollback.</summary>
    [Fact]
    public void PostCommitReportingFaultReturnsCommittedDegradedResult()
    {
        using var fixture = new LifecycleBoundaryFixture();
        var observation = new ManifestMutation.Observation(Replaced: () => throw new IOException("report fault"));

        object result = CliTypes.CommandApp.ApproveObserved(
            fixture.Configuration,
            fixture.ApprovalOptions(),
            observation
        );

        JsonElement json = JsonSerializer.SerializeToElement(result, JsonOptions.Stable);
        Assert.True(json.GetProperty("committed").GetBoolean());
        Assert.True(json.GetProperty("reporting_degraded").GetBoolean());
        Assert.Equal(
            AssetLifecycle.Approved,
            ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle
        );
    }

    /// <summary>Characterizes successful approval without adversarial filesystem changes.</summary>
    [Fact]
    public void ApprovalWithoutInterleavingCommitsValidatedCandidate()
    {
        using var fixture = new LifecycleBoundaryFixture();

        object result = CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions());

        Assert.Equal(
            AssetLifecycle.Approved,
            ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle
        );
        Assert.Equal(fixture.Bytes, File.ReadAllBytes(fixture.AssetPath));
        Assert.Equal(
            "approved",
            JsonSerializer.SerializeToElement(result, JsonOptions.Stable).GetProperty("lifecycle").GetString()
        );
    }

    /// <summary>Characterizes dry-run preservation of the candidate and selected bytes.</summary>
    [Fact]
    public void DryRunPreservesCandidateAndSelectedBytes()
    {
        using var fixture = new LifecycleBoundaryFixture();
        string before = File.ReadAllText(fixture.ManifestPath);

        _ = CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions(dryRun: true));

        Assert.Equal(before, File.ReadAllText(fixture.ManifestPath));
        Assert.Equal(fixture.Bytes, File.ReadAllBytes(fixture.AssetPath));
    }

    /// <summary>Rejects approval when selected bytes cease to match the validated evidence.</summary>
    [Fact]
    public void ApprovalRefusesBytesSubstitutedAfterEvidenceValidation()
    {
        using var fixture = new LifecycleBoundaryFixture();
        var observation = new ManifestMutation.Observation(EvidenceValidated: () =>
            File.WriteAllBytes(fixture.AssetPath, [1, 2, 3])
        );

        Exception? failure = Record.Exception(() =>
            CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions(), observation)
        );

        Assert.Equal(
            AssetLifecycle.Candidate,
            ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle
        );
        Assert.IsType<AssetCtlException>(failure);
    }

    /// <summary>Preserves a committed outcome when the selected file becomes unreadable after replacement.</summary>
    [Fact]
    public void FailureReadingAssetAfterReplacementCannotReportApprovalRefusal()
    {
        using var fixture = new LifecycleBoundaryFixture();
        var observation = new ManifestMutation.Observation(Replaced: () => File.Delete(fixture.AssetPath));

        Exception? failure = Record.Exception(() =>
            CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions(), observation)
        );

        Assert.Equal(
            AssetLifecycle.Approved,
            ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle
        );
        Assert.Null(failure);
    }

    /// <summary>Protects an unrelated directory object from a late catalog-parent symlink substitution.</summary>
    [Fact]
    public void ParentSubstitutionAfterCasCannotOverwriteOutsideAdmittedCatalog()
    {
        using var fixture = new LifecycleBoundaryFixture();
        string outside = Path.Combine(fixture.Root, "outside-catalog");
        Directory.CreateDirectory(outside);
        string victim = Path.Combine(outside, Path.GetFileName(fixture.ManifestPath));
        const string predecessor = "unrelated object must survive";
        File.WriteAllText(victim, predecessor);
        AssetManifest replacement = fixture.Manifest with { Revision = fixture.Manifest.Revision + 1 };
        var observation = new ManifestMutation.Observation(
            BeforeReplacement: (stage, _) =>
            {
                // Both adversarial directories stay in the fixture sandbox. The attacker controls the
                // late path lookup's stage name as well as its destination, while the admitted parent survives.
                File.Copy(stage, Path.Combine(outside, Path.GetFileName(stage)));
                string catalog = Path.GetDirectoryName(fixture.ManifestPath)!;
                Directory.Move(catalog, Path.Combine(fixture.Root, "admitted-catalog"));
                Directory.CreateSymbolicLink(catalog, outside);
            }
        );

        _ = Record.Exception(() =>
            ManifestMutation.WriteCas(fixture.Configuration, fixture.Manifest, replacement, observation)
        );

        Assert.Equal(predecessor, File.ReadAllText(victim));
    }
}
