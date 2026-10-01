using System.Text.Json;
using AlterCourse.AssetCtl.Cli;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Exercises lifecycle evidence and commit outcomes on temporary Linux filesystem objects.</summary>
public sealed class LifecycleBoundaryRegressionTests
{
    /// <summary>Characterizes successful approval without adversarial filesystem changes.</summary>
    [Fact]
    public void ApprovalWithoutInterleavingCommitsValidatedCandidate()
    {
        using var fixture = new LifecycleBoundaryFixture();

        object result = CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions());

        Assert.Equal(AssetLifecycle.Approved, ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle);
        Assert.Equal(fixture.Bytes, File.ReadAllBytes(fixture.AssetPath));
        Assert.Equal("approved", JsonSerializer.SerializeToElement(result, JsonOptions.Stable).GetProperty("lifecycle").GetString());
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
        var observation = new ManifestMutation.Observation(
            EvidenceValidated: () => File.WriteAllBytes(fixture.AssetPath, [1, 2, 3])
        );

        Exception? failure = Record.Exception(() =>
            CliTypes.CommandApp.ApproveObserved(fixture.Configuration, fixture.ApprovalOptions(), observation)
        );

        Assert.Equal(AssetLifecycle.Candidate, ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle);
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

        Assert.Equal(AssetLifecycle.Approved, ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath).Request.Lifecycle);
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

        _ = Record.Exception(() => ManifestMutation.WriteCas(fixture.Configuration, fixture.Manifest, replacement, observation));

        Assert.Equal(predecessor, File.ReadAllText(victim));
    }
}
