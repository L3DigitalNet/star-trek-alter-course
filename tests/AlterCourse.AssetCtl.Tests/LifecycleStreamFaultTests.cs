using AlterCourse.AssetCtl.Publishing;
using Microsoft.Win32.SafeHandles;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Injects faults in real stage streams while retaining exclusive descriptor ownership.</summary>
public sealed class LifecycleStreamFaultTests
{
    /// <summary>A failed stage stream constructor closes its native handle and cleans only its owned leaf.</summary>
    [Fact]
    public void StageConstructorFailurePreservesOriginalAndDisposesHandle()
    {
        using var fixture = new LifecycleBoundaryFixture();
        string predecessor = File.ReadAllText(fixture.ManifestPath);
        SafeFileHandle? captured = null;
        var primary = new IOException("stage constructor fault");
        var observation = new ManifestMutation.Observation(CreateStageStream: handle =>
        {
            captured = handle;
            throw primary;
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
        Assert.True(captured!.IsClosed);
        Assert.Equal(predecessor, File.ReadAllText(fixture.ManifestPath));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(fixture.ManifestPath)!, "*.assetctl-stage-*"));
    }

    /// <summary>Actual partial-write, flush, and disposed-stream faults retain the predecessor and primary error.</summary>
    [Theory]
    [InlineData((int)ManifestMutation.StageOperation.Write, false)]
    [InlineData((int)ManifestMutation.StageOperation.Flush, false)]
    [InlineData((int)ManifestMutation.StageOperation.Close, false)]
    [InlineData((int)ManifestMutation.StageOperation.Write, true)]
    [InlineData((int)ManifestMutation.StageOperation.Flush, true)]
    [InlineData((int)ManifestMutation.StageOperation.Close, true)]
    public void RealStageStreamFaultsPreservePrimaryAndCloseOwnedDescriptor(int operation, bool secondaryCloseFault)
    {
        using var fixture = new LifecycleBoundaryFixture();
        string predecessor = File.ReadAllText(fixture.ManifestPath);
        var primary = new IOException("primary stream fault");
        SafeFileHandle? captured = null;
        var observation = new ManifestMutation.Observation(CreateStageStream: handle =>
        {
            captured = handle;
            return new FaultingStream(handle, (ManifestMutation.StageOperation)operation, primary, secondaryCloseFault);
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
        Assert.True(captured!.IsClosed);
        Assert.Equal(predecessor, File.ReadAllText(fixture.ManifestPath));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(fixture.ManifestPath)!, "*.assetctl-stage-*"));
    }

    /// <summary>Exclusive staging never truncates a pre-existing unrelated stage leaf.</summary>
    [Fact]
    public void ExclusiveStageCreationPreservesExistingLeaf()
    {
        using var fixture = new LifecycleBoundaryFixture();
        using var parent = PublishingTypes.StateFile.DirectoryHandle.OpenExisting(
            Path.GetDirectoryName(fixture.ManifestPath)!,
            "fixture"
        );
        string path = Path.Combine(Path.GetDirectoryName(fixture.ManifestPath)!, "existing.stage");
        File.WriteAllText(path, "unrelated stage");

        Assert.Throws<AssetCtlException>(() => parent.CreateFile("existing.stage", "fixture"));

        Assert.Equal("unrelated stage", File.ReadAllText(path));
    }

    private sealed class FaultingStream(
        SafeFileHandle handle,
        ManifestMutation.StageOperation operation,
        IOException primary,
        bool secondaryCloseFault
    ) : FileStream(handle, FileAccess.Write, bufferSize: 1)
    {
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (operation == ManifestMutation.StageOperation.Write)
            {
                base.Write(buffer[..1]);
                throw primary;
            }

            base.Write(buffer);
        }

        public override void Flush(bool flushToDisk)
        {
            if (operation == ManifestMutation.StageOperation.Flush)
            {
                throw primary;
            }
            base.Flush(flushToDisk);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
            {
                return;
            }
            if (operation == ManifestMutation.StageOperation.Close)
            {
                throw primary;
            }
            if (secondaryCloseFault)
            {
                throw new IOException("secondary close fault");
            }
        }
    }
}
