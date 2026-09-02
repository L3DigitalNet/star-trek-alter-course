using System.Security.Cryptography;
using System.Text;

namespace AlterCourse.AssetCtl.Publishing;

internal static class ManifestMutation
{
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

    public static void WriteCas(EffectiveConfiguration configuration, AssetManifest expected, AssetManifest replacement)
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
            File.Move(stage, path, overwrite: true);
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
