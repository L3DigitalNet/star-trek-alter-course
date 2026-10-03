using System.Security.Cryptography;
using System.Text;
using AlterCourse.AssetCtl.Catalog;
using StateFile = AlterCourse.AssetCtl.Publishing.PublishingTypes.StateFile;

namespace AlterCourse.AssetCtl.Publishing;

internal static class ManifestMutation
{
    // These per-call seams expose filesystem interleavings without process-wide mutable hooks.
    // Normal callers leave them absent and retain the same operation ordering.
    internal sealed record Observation(
        Action? EvidenceValidated = null,
        Action<string, string>? BeforeReplacement = null,
        Action? Replaced = null,
        Action<StageOperation>? StageOperation = null,
        Func<Microsoft.Win32.SafeHandles.SafeFileHandle, FileStream>? CreateStageStream = null
    );

    public static AssetManifest ReloadForMutation(EffectiveConfiguration configuration, AssetManifest observed)
    {
        AssetManifest current = ManifestStore.Load(configuration, observed.ManifestPath);
        EnsureSameVersion(observed, current);
        return current;
    }

    public static void EnsureCurrent(EffectiveConfiguration configuration, AssetManifest expected)
    {
        AssetManifest current = ManifestStore.Load(configuration, expected.ManifestPath);
        EnsureSameVersion(expected, current);
    }

    internal enum StageOperation
    {
        Create,
        Write,
        Flush,
        Close,
        Replace,
        Cleanup,
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    internal readonly record struct Outcome(bool ReportingDegraded);

    public static Outcome WriteCas(
        EffectiveConfiguration configuration,
        AssetManifest expected,
        AssetManifest replacement,
        Observation? observation = null,
        LifecycleBoundary? boundary = null
    )
    {
        using LifecycleBoundary? ownedBoundary = boundary is null
            ? new LifecycleBoundary(configuration, expected)
            : null;
        LifecycleBoundary admitted = boundary ?? ownedBoundary!;
        string leaf = admitted.ManifestLeaf + ".assetctl-stage-" + Guid.NewGuid().ToString("N");
        string stagePath = Path.Combine(Path.GetDirectoryName(admitted.ManifestPath)!, leaf);
        StateFile.FileIdentity? stageIdentity = null;
        bool committed = false;
        bool degraded = false;
        try
        {
            observation?.StageOperation?.Invoke(StageOperation.Create);
            byte[] serialized = new UTF8Encoding(false).GetBytes(ManifestStore.Serialize(replacement));
            WriteStage(admitted.Parent, leaf, serialized, observation, out stageIdentity);
            observation?.BeforeReplacement?.Invoke(stagePath, admitted.ManifestPath);
            observation?.StageOperation?.Invoke(StageOperation.Replace);
            // Both the revision snapshot and selected evidence are checked after the final test
            // interleaving. AssetLock serializes supported writers through this rename; arbitrary
            // same-UID writes can still race a non-atomic read/check/rename sequence.
            admitted.EnsureCurrent();
            EnsureStage(admitted.Parent, leaf, stageIdentity!.Value, serialized);
            admitted.Parent.MoveTo(leaf, admitted.Parent, admitted.ManifestLeaf, "lifecycle replacement");
            // The successful rename is commitment. Reporting and cleanup cannot convert it into refusal.
            committed = true;
            try
            {
                observation?.Replaced?.Invoke();
            }
            catch (Exception)
            {
                degraded = true;
            }
        }
        finally
        {
            if (!committed && stageIdentity is not null)
            {
                CleanupStage(admitted.Parent, leaf, stageIdentity.Value, observation);
            }
        }

        return new Outcome(degraded);
    }

    private static void WriteStage(
        StateFile.DirectoryHandle parent,
        string leaf,
        byte[] serialized,
        Observation? observation,
        out StateFile.FileIdentity? identity
    )
    {
        identity = null;
        // Capture ownership from the native handle before constructing a stream. Accessing
        // FileStream.SafeFileHandle can itself flush, so it cannot establish cleanup ownership.
        FileStream stream = parent.CreateFile(leaf, "lifecycle stage", out identity, observation?.CreateStageStream);
        try
        {
            observation?.StageOperation?.Invoke(StageOperation.Write);
            stream.Write(serialized);
            observation?.StageOperation?.Invoke(StageOperation.Flush);
            stream.Flush(flushToDisk: true);
            observation?.StageOperation?.Invoke(StageOperation.Close);
        }
        catch
        {
            // Dispose may flush and fail too. Preserve the write/flush fault that caused refusal.
            try
            {
                stream.Dispose();
            }
            catch (Exception) { }
            throw;
        }

        stream.Dispose();
    }

    private static void EnsureStage(
        StateFile.DirectoryHandle parent,
        string leaf,
        StateFile.FileIdentity identity,
        byte[] serialized
    )
    {
        using FileStream stage = parent.OpenReadFile(leaf, "lifecycle stage");
        if (
            StateFile.Identity(stage) != identity
            || !SelectedAssetReader
                .Read(stage, global::AlterCourse.AssetCtl.Configuration.YamlValues.MaximumBytes)
                .AsSpan()
                .SequenceEqual(serialized)
        )
        {
            throw new AssetCtlException("Lifecycle stage changed before replacement.", 7);
        }
    }

    private static void CleanupStage(
        StateFile.DirectoryHandle parent,
        string leaf,
        StateFile.FileIdentity identity,
        Observation? observation
    )
    {
        try
        {
            observation?.StageOperation?.Invoke(StageOperation.Cleanup);
            using FileStream remaining = parent.OpenReadFile(leaf, "lifecycle stage cleanup");
            if (StateFile.Identity(remaining) == identity)
            {
                parent.DeleteFile(leaf, "lifecycle stage cleanup");
            }
        }
        catch (Exception)
        {
            // Cleanup is best effort and identity-bound. An unrelated substituted stage
            // survives, and a cleanup fault never obscures the active precommit failure.
        }
    }

    internal static void EnsureSameVersion(AssetManifest expected, AssetManifest current)
    {
        string expectedHash = Hash(ManifestStore.Serialize(expected));
        string currentHash = Hash(ManifestStore.Serialize(current));
        if (
            expected.Revision != current.Revision
            || !string.Equals(expectedHash, currentHash, StringComparison.Ordinal)
        )
        {
            throw new AssetCtlException($"Asset '{expected.Request.Id}' changed during lifecycle mutation.", 7);
        }
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
