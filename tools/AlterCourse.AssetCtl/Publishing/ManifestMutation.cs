using System.Security.Cryptography;
using System.Text;

namespace AlterCourse.AssetCtl.Publishing;

internal static class ManifestMutation
{
    // These per-call seams expose filesystem interleavings without process-wide mutable hooks.
    // Normal callers leave them absent and retain the same operation ordering.
    internal sealed record Observation(
        Action? EvidenceValidated = null,
        Action<string, string>? BeforeReplacement = null,
        Action? Replaced = null
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

    public static void WriteCas(
        EffectiveConfiguration configuration,
        AssetManifest expected,
        AssetManifest replacement,
        Observation? observation = null
    )
    {
        string path = PathPolicy.ResolveUnder(
            configuration.RepositoryRoot,
            expected.ManifestPath,
            "manifest",
            allowMissing: false
        );
        string stage = path + ".assetctl-stage-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(stage, ManifestStore.Serialize(replacement), new UTF8Encoding(false));
        try
        {
            // This comparison occurs while the per-asset lock is held; every lifecycle writer must share that lock.
            EnsureCurrent(configuration, expected);
            observation?.BeforeReplacement?.Invoke(stage, path);
            File.Move(stage, path, overwrite: true);
            observation?.Replaced?.Invoke();
        }
        finally
        {
            if (File.Exists(stage))
            {
                File.Delete(stage);
            }
        }
    }

    private static void EnsureSameVersion(AssetManifest expected, AssetManifest current)
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
