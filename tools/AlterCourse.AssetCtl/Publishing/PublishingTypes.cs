using System.Text.Json;

namespace AlterCourse.AssetCtl.Publishing;

internal static class PublishingTypes
{
    public sealed class AssetLock : IDisposable
    {
        private readonly FileStream stream;

        private AssetLock(FileStream stream) => this.stream = stream;

        public static AssetLock Acquire(EffectiveConfiguration configuration, string assetId)
        {
            string stateRoot = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                configuration.Paths.StateRoot,
                "state_root",
                allowMissing: true
            );
            string lockRoot = Path.Combine(stateRoot, "locks");
            Directory.CreateDirectory(lockRoot);
            string safeId = string.Concat(
                assetId.Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_')
            );
            string path = Path.Combine(lockRoot, safeId + ".lock");
            FileStream lockStream;
            try
            {
                lockStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                throw new AssetCtlException($"Asset '{assetId}' is locked by another process.", 7);
            }

            lockStream.SetLength(0);
            JsonSerializer.Serialize(
                lockStream,
                new { process_id = Environment.ProcessId, acquired_at = DateTimeOffset.UtcNow }
            );
            lockStream.Flush(flushToDisk: true);
            return new AssetLock(lockStream);
        }

        public void Dispose() => stream.Dispose();
    }

    public static class AtomicPublisher
    {
        public static void Publish(
            EffectiveConfiguration configuration,
            string assetRelativePath,
            byte[] assetBytes,
            string manifestRelativePath,
            string manifestText
        )
        {
            string asset = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                assetRelativePath,
                "asset output",
                allowMissing: true
            );
            string manifest = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                manifestRelativePath,
                "manifest output",
                allowMissing: true
            );
            Directory.CreateDirectory(Path.GetDirectoryName(asset)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);

            string transaction = Guid.NewGuid().ToString("N");
            string assetStage = asset + $".assetctl-stage-{transaction}";
            string manifestStage = manifest + $".assetctl-stage-{transaction}";
            string assetBackup = asset + $".assetctl-backup-{transaction}";
            string manifestBackup = manifest + $".assetctl-backup-{transaction}";
            File.WriteAllBytes(assetStage, assetBytes);
            File.WriteAllText(manifestStage, manifestText, new System.Text.UTF8Encoding(false));

            bool assetExisted = File.Exists(asset);
            bool manifestExisted = File.Exists(manifest);
            PublishStaged(
                asset,
                manifest,
                assetStage,
                manifestStage,
                assetBackup,
                manifestBackup,
                assetExisted,
                manifestExisted
            );
        }

        private static void PublishStaged(
            string asset,
            string manifest,
            string assetStage,
            string manifestStage,
            string assetBackup,
            string manifestBackup,
            bool assetExisted,
            bool manifestExisted
        )
        {
            try
            {
                // Both replacements are fully staged before either live file moves; rollback can therefore restore the prior matching pair.
                if (assetExisted)
                {
                    File.Move(asset, assetBackup);
                }

                if (manifestExisted)
                {
                    File.Move(manifest, manifestBackup);
                }

                File.Move(assetStage, asset);
                File.Move(manifestStage, manifest);
                DeleteIfExists(assetBackup);
                DeleteIfExists(manifestBackup);
            }
            catch
            {
                // A manifest must never survive claiming bytes that were not published; remove the partial pair before restoring backups.
                DeleteIfExists(asset);
                DeleteIfExists(manifest);
                if (assetExisted && File.Exists(assetBackup))
                {
                    File.Move(assetBackup, asset);
                }

                if (manifestExisted && File.Exists(manifestBackup))
                {
                    File.Move(manifestBackup, manifest);
                }

                throw;
            }
            finally
            {
                DeleteIfExists(assetStage);
                DeleteIfExists(manifestStage);
                DeleteIfExists(assetBackup);
                DeleteIfExists(manifestBackup);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public static class ReceiptWriter
    {
        public static string Write(EffectiveConfiguration configuration, object receipt)
        {
            string root = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                configuration.Paths.ReceiptRoot,
                "receipt_root",
                allowMissing: true
            );
            Directory.CreateDirectory(root);
            string id = Guid.NewGuid().ToString();
            string path = Path.Combine(root, id + ".json");
            string stage = path + ".tmp";
            File.WriteAllText(
                stage,
                JsonSerializer.Serialize(receipt, JsonOptions.Indented),
                new System.Text.UTF8Encoding(false)
            );
            File.Move(stage, path);
            return Path.GetRelativePath(configuration.RepositoryRoot, path);
        }
    }

    public static class JsonOptions
    {
        public static readonly JsonSerializerOptions Stable = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
        };

        public static readonly JsonSerializerOptions Indented = new(Stable) { WriteIndented = true };
    }
}
