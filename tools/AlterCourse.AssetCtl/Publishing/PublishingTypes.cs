using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlterCourse.AssetCtl.Configuration;
using YamlDotNet.RepresentationModel;

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
            string path = Path.Combine(lockRoot, "catalog.lock");
            FileStream lockStream;
            try
            {
                lockStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                throw new AssetCtlException($"Asset catalog is locked while '{assetId}' waits to mutate.", 7);
            }

            lockStream.SetLength(0);
            JsonSerializer.Serialize(
                lockStream,
                new { process_id = Environment.ProcessId, acquired_at = DateTimeOffset.UtcNow }
            );
            lockStream.Flush(flushToDisk: true);
            try
            {
                ManifestStore.LoadAll(configuration);
            }
            catch
            {
                lockStream.Dispose();
                throw;
            }
            return new AssetLock(lockStream);
        }

        public void Dispose() => stream.Dispose();
    }

    public static class AtomicPublisher
    {
        private const long MaximumJournalBytes = 64 * 1024;
        private const string LeaseFileName = "active.lease";
        private static readonly JsonSerializerOptions JournalJsonOptions = new(JsonOptions.Stable) { MaxDepth = 16 };

        public sealed record PublicationResult(
            bool Published,
            int RecoveredPendingTransactions,
            int ActiveTransactionsSkipped,
            string Rollback
        );

        internal sealed record PublicationRecoveryResult(int RecoveredTransactions, int ActiveTransactionsSkipped);

        internal enum PublicationMove
        {
            BackupAsset,
            BackupManifest,
            InstallAsset,
            InstallManifest,
        }

        internal sealed record PublicationTestHooks(
            Action<string, string>? AfterWorkFilesStaged = null,
            Action<PublicationMove>? BeforeMove = null,
            Action<PublicationMove>? AfterMove = null
        );

        internal sealed class SimulatedPublicationInterruptionException : Exception;

        public static PublicationResult Publish(
            EffectiveConfiguration configuration,
            string assetRelativePath,
            byte[] assetBytes,
            string manifestRelativePath,
            string manifestText
        ) => Publish(configuration, assetRelativePath, assetBytes, manifestRelativePath, manifestText, null);

        internal static PublicationResult Publish(
            EffectiveConfiguration configuration,
            string assetRelativePath,
            byte[] assetBytes,
            string manifestRelativePath,
            string manifestText,
            PublicationTestHooks? testHooks
        )
        {
            ManifestStore.ValidatePublicationOwnership(configuration, manifestRelativePath, assetRelativePath);
            PublicationRecoveryResult recovery = RecoverPending(configuration);
            using PreparedPublication publication = PreparePublication(
                configuration,
                assetRelativePath,
                assetBytes,
                manifestRelativePath,
                manifestText,
                testHooks
            );
            PublishPrepared(configuration, publication, testHooks);
            return new PublicationResult(
                true,
                recovery.RecoveredTransactions,
                recovery.ActiveTransactionsSkipped,
                "not-required"
            );
        }

        private static PreparedPublication PreparePublication(
            EffectiveConfiguration configuration,
            string assetRelativePath,
            byte[] assetBytes,
            string manifestRelativePath,
            string manifestText,
            PublicationTestHooks? testHooks
        )
        {
            string asset = PathPolicy.ResolveOutputPath(configuration, assetRelativePath, allowMissing: true);
            string manifest = PathPolicy.ResolveManifestPath(configuration, manifestRelativePath, allowMissing: true);
            Directory.CreateDirectory(Path.GetDirectoryName(asset)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);

            StagedPublication staged = StagePublication(configuration, assetBytes, manifestText, testHooks);
            try
            {
                string assetBackup = asset + $".assetctl-backup-{staged.TransactionId}";
                string manifestBackup = manifest + $".assetctl-backup-{staged.TransactionId}";

                (bool assetExisted, bool manifestExisted) = ValidateExistingState(
                    configuration,
                    assetRelativePath,
                    manifestRelativePath,
                    asset,
                    manifest,
                    staged.ManifestStage
                );

                var journal = new PublicationJournal(
                    staged.TransactionId,
                    Path.GetRelativePath(configuration.RepositoryRoot, asset),
                    Path.GetRelativePath(configuration.RepositoryRoot, manifest),
                    Path.GetRelativePath(configuration.RepositoryRoot, staged.AssetStage),
                    Path.GetRelativePath(configuration.RepositoryRoot, staged.ManifestStage),
                    Path.GetRelativePath(configuration.RepositoryRoot, assetBackup),
                    Path.GetRelativePath(configuration.RepositoryRoot, manifestBackup),
                    assetExisted,
                    manifestExisted,
                    staged.AssetHash,
                    staged.AssetLength,
                    staged.ManifestHash
                );
                string journalPath = WriteJournal(configuration, journal);
                PublicationJournal resolvedJournal = journal with
                {
                    AssetStagePath = staged.AssetStage,
                    ManifestStagePath = staged.ManifestStage,
                    AssetBackupPath = assetBackup,
                    ManifestBackupPath = manifestBackup,
                };
                return new PreparedPublication(
                    asset,
                    manifest,
                    journalPath,
                    resolvedJournal,
                    journal,
                    staged.TransactionRoot,
                    staged.LeasePath,
                    staged.Lease
                );
            }
            catch
            {
                staged.Lease.Dispose();
                DeleteTree(staged.TransactionRoot);
                throw;
            }
        }

        private static (bool AssetExisted, bool ManifestExisted) ValidateExistingState(
            EffectiveConfiguration configuration,
            string assetRelativePath,
            string manifestRelativePath,
            string asset,
            string manifest,
            string stagedManifestPath
        )
        {
            bool assetExisted = File.Exists(asset);
            bool manifestExisted = File.Exists(manifest);
            if (assetExisted && !manifestExisted)
            {
                throw new AssetCtlException("Existing asset and manifest do not form a complete publish pair.", 7);
            }

            if (!assetExisted && manifestExisted)
            {
                ValidateManifestOnlyOrRepairState(
                    configuration,
                    assetRelativePath,
                    manifestRelativePath,
                    stagedManifestPath
                );
            }

            return (assetExisted, manifestExisted);
        }

        private static void ValidateManifestOnlyOrRepairState(
            EffectiveConfiguration configuration,
            string assetRelativePath,
            string manifestRelativePath,
            string stagedManifestPath
        )
        {
            AssetManifest existing = ManifestStore.Load(configuration, manifestRelativePath);
            YamlMappingNode staged = StrictYaml.LoadMapping(stagedManifestPath);
            string stagedId = staged.Scalar("id", "manifest");
            string stagedOutput = staged.Mapping("output", "manifest").Scalar("path", "manifest.output");
            if (
                !string.Equals(existing.Request.Id, stagedId, StringComparison.Ordinal)
                || !string.Equals(existing.Request.Output.Path, assetRelativePath, StringComparison.Ordinal)
                || !string.Equals(stagedOutput, assetRelativePath, StringComparison.Ordinal)
            )
            {
                throw new AssetCtlException(
                    "Existing manifest does not authorize publication or repair of this output.",
                    7
                );
            }
        }

        private static StagedPublication StagePublication(
            EffectiveConfiguration configuration,
            byte[] assetBytes,
            string manifestText,
            PublicationTestHooks? testHooks
        )
        {
            string transaction = Guid.NewGuid().ToString("N");
            string workRoot = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                configuration.Paths.WorkRoot,
                "work_root",
                allowMissing: true
            );
            string transactionRoot = Path.Combine(workRoot, "publish", transaction);
            Directory.CreateDirectory(transactionRoot);
            string leasePath = Path.Combine(transactionRoot, LeaseFileName);
            FileStream? lease = null;
            string assetStage = Path.Combine(transactionRoot, "asset.stage");
            string manifestStage = Path.Combine(transactionRoot, "manifest.stage");
            try
            {
                lease = new FileStream(leasePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                JsonSerializer.Serialize(
                    lease,
                    new { process_id = Environment.ProcessId, acquired_at = DateTimeOffset.UtcNow },
                    JournalJsonOptions
                );
                lease.Flush(flushToDisk: true);
                WriteDurable(assetStage, assetBytes);
                WriteDurable(manifestStage, new UTF8Encoding(false).GetBytes(manifestText));
                testHooks?.AfterWorkFilesStaged?.Invoke(assetStage, manifestStage);
                (string assetHash, long assetLength, string manifestHash) = VerifyStaged(
                    assetStage,
                    manifestStage,
                    assetBytes,
                    manifestText
                );
                return new StagedPublication(
                    transaction,
                    transactionRoot,
                    assetStage,
                    manifestStage,
                    assetHash,
                    assetLength,
                    manifestHash,
                    leasePath,
                    lease
                );
            }
            catch
            {
                lease?.Dispose();
                DeleteTree(transactionRoot);
                throw;
            }
        }

        private static void PublishPrepared(
            EffectiveConfiguration configuration,
            PreparedPublication publication,
            PublicationTestHooks? testHooks
        )
        {
            PublicationJournal journal = publication.Journal;
            try
            {
                // The journal becomes durable before the first rename. Recovery can therefore distinguish a complete
                // new pair from every partial sequence and deterministically finish or restore the previous pair.
                if (journal.AssetExisted)
                {
                    Move(publication.Asset, journal.AssetBackupPath, PublicationMove.BackupAsset, testHooks);
                }

                if (journal.ManifestExisted)
                {
                    Move(publication.Manifest, journal.ManifestBackupPath, PublicationMove.BackupManifest, testHooks);
                }

                Move(journal.AssetStagePath, publication.Asset, PublicationMove.InstallAsset, testHooks);
                Move(journal.ManifestStagePath, publication.Manifest, PublicationMove.InstallManifest, testHooks);
                if (!NewPairMatches(journal, publication.Asset, publication.Manifest))
                {
                    throw new AssetCtlException("Published pair does not match its staged integrity evidence.", 7);
                }

                Complete(publication.JournalPath, journal, configuration);
            }
            catch (SimulatedPublicationInterruptionException)
            {
                // Tests use this exception to model process loss: production never catches an actual terminated process,
                // so retaining the journal and files is necessary to exercise next-publish recovery faithfully.
                throw;
            }
            catch
            {
                RecoverJournal(configuration, publication.JournalPath, publication.RecoveryJournal);
                throw;
            }
        }

        internal static PublicationRecoveryResult RecoverPending(EffectiveConfiguration configuration)
        {
            string journalRoot = JournalRoot(configuration);
            if (!Directory.Exists(journalRoot))
            {
                return new PublicationRecoveryResult(0, 0);
            }

            int recovered = 0;
            int active = 0;
            foreach (string path in Directory.EnumerateFiles(journalRoot, "*.json").Order(StringComparer.Ordinal))
            {
                PublicationJournal journal;
                try
                {
                    journal = ReadJournal(path);
                    ValidateJournal(path, journal);
                }
                catch (Exception exception) when (exception is JsonException or InvalidDataException)
                {
                    string quarantined = QuarantineJournal(journalRoot, path);
                    throw new AssetCtlException(
                        $"Publication journal '{Path.GetFileName(path)}' is invalid and was quarantined as "
                            + $"'{Path.GetFileName(quarantined)}': {exception.Message}",
                        7
                    );
                }

                FileStream? recoveryLease = TryAcquireRecoveryLease(configuration, journal.TransactionId);
                if (recoveryLease is null)
                {
                    active++;
                    continue;
                }

                try
                {
                    RecoverJournal(configuration, path, journal);
                    recovered++;
                }
                catch (AssetCtlException exception)
                {
                    string quarantined = QuarantineJournal(journalRoot, path);
                    throw new AssetCtlException(
                        $"Publication journal '{Path.GetFileName(path)}' was unsafe and was quarantined as "
                            + $"'{Path.GetFileName(quarantined)}': {exception.Message}",
                        7
                    );
                }
                finally
                {
                    string leasePath = recoveryLease.Name;
                    recoveryLease.Dispose();
                    DeleteIfExists(leasePath);
                    DeleteEmptyTransactionDirectory(leasePath, configuration);
                }
            }

            return new PublicationRecoveryResult(recovered, active);
        }

        private static PublicationJournal ReadJournal(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                throw new InvalidDataException("journal must be a regular file inside the configured state root");
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > MaximumJournalBytes)
            {
                throw new InvalidDataException($"journal length must be from 1 to {MaximumJournalBytes} bytes");
            }

            return JsonSerializer.Deserialize<PublicationJournal>(stream, JournalJsonOptions)
                ?? throw new JsonException("empty journal");
        }

        private static void ValidateJournal(string path, PublicationJournal journal)
        {
            if (
                !Guid.TryParseExact(journal.TransactionId, "N", out _)
                || !string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    journal.TransactionId,
                    StringComparison.Ordinal
                )
                || string.IsNullOrWhiteSpace(journal.AssetPath)
                || string.IsNullOrWhiteSpace(journal.ManifestPath)
                || string.IsNullOrWhiteSpace(journal.AssetStagePath)
                || string.IsNullOrWhiteSpace(journal.ManifestStagePath)
                || string.IsNullOrWhiteSpace(journal.AssetBackupPath)
                || string.IsNullOrWhiteSpace(journal.ManifestBackupPath)
                || journal.AssetLength < 0
                || !IsSha256(journal.AssetHash)
                || !IsSha256(journal.ManifestHash)
            )
            {
                throw new InvalidDataException("journal fields do not satisfy the publication contract");
            }
        }

        private static FileStream? TryAcquireRecoveryLease(EffectiveConfiguration configuration, string transactionId)
        {
            string workRoot = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                configuration.Paths.WorkRoot,
                "work_root",
                allowMissing: true
            );
            string transactionRoot = Path.Combine(workRoot, "publish", transactionId);
            Directory.CreateDirectory(transactionRoot);
            string leasePath = Path.Combine(transactionRoot, LeaseFileName);
            try
            {
                return new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                // An operating-system lock, rather than journal age, distinguishes a live publisher from a crashed one.
                // Wall-clock stale policies can destroy a slow but active transaction after clock skew or provider delay.
                return null;
            }
        }

        private static string QuarantineJournal(string journalRoot, string path)
        {
            string fullRoot = Path.GetFullPath(journalRoot);
            string fullPath = Path.GetFullPath(path);
            if (
                !string.Equals(Path.GetDirectoryName(fullPath), fullRoot, StringComparison.Ordinal)
                || !File.Exists(fullPath)
            )
            {
                throw new AssetCtlException("Publication journal quarantine target escaped its configured root.", 7);
            }

            string quarantineRoot = Path.Combine(fullRoot, "quarantine");
            Directory.CreateDirectory(quarantineRoot);
            string destination = Path.Combine(
                quarantineRoot,
                Path.GetFileNameWithoutExtension(fullPath) + "." + Guid.NewGuid().ToString("N") + ".invalid"
            );
            File.Move(fullPath, destination);
            return destination;
        }

        private static (string AssetHash, long AssetLength, string ManifestHash) VerifyStaged(
            string assetStage,
            string manifestStage,
            byte[] expectedAssetBytes,
            string expectedManifestText
        )
        {
            byte[] stagedAsset = File.ReadAllBytes(assetStage);
            byte[] stagedManifest = File.ReadAllBytes(manifestStage);
            string assetHash = Hash(stagedAsset);
            string expectedAssetHash = Hash(expectedAssetBytes);
            string manifestHash = Hash(stagedManifest);
            string expectedManifestHash = Hash(new UTF8Encoding(false).GetBytes(expectedManifestText));
            if (
                !string.Equals(assetHash, expectedAssetHash, StringComparison.Ordinal)
                || !string.Equals(manifestHash, expectedManifestHash, StringComparison.Ordinal)
            )
            {
                throw new AssetCtlException("Staged publication bytes changed before verification.", 7);
            }

            YamlMappingNode root = StrictYaml.LoadMapping(manifestStage);
            YamlMappingNode integrity = root.Mapping("integrity", "manifest");
            string claimedHash = integrity.Scalar("sha256", "manifest.integrity");
            long claimedLength = integrity.Long("byte_length", "manifest.integrity");
            if (
                !string.Equals(assetHash, claimedHash, StringComparison.Ordinal)
                || stagedAsset.LongLength != claimedLength
            )
            {
                throw new AssetCtlException("Staged manifest integrity does not match staged asset bytes.", 7);
            }

            return (assetHash, stagedAsset.LongLength, manifestHash);
        }

        private static void Move(
            string source,
            string destination,
            PublicationMove move,
            PublicationTestHooks? testHooks
        )
        {
            testHooks?.BeforeMove?.Invoke(move);
            File.Move(source, destination);
            testHooks?.AfterMove?.Invoke(move);
        }

        private static string WriteJournal(EffectiveConfiguration configuration, PublicationJournal journal)
        {
            string root = JournalRoot(configuration);
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, journal.TransactionId + ".json");
            string stage = path + ".tmp";
            WriteDurable(stage, JsonSerializer.SerializeToUtf8Bytes(journal, JournalJsonOptions));
            File.Move(stage, path);
            return path;
        }

        private static string JournalRoot(EffectiveConfiguration configuration)
        {
            string stateRoot = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                configuration.Paths.StateRoot,
                "state_root",
                allowMissing: true
            );
            return Path.Combine(stateRoot, "publish-transactions");
        }

        private static void RecoverJournal(
            EffectiveConfiguration configuration,
            string journalPath,
            PublicationJournal journal
        )
        {
            string asset = PathPolicy.ResolveOutputPath(configuration, journal.AssetPath, allowMissing: true);
            string manifest = PathPolicy.ResolveManifestPath(configuration, journal.ManifestPath, allowMissing: true);
            string assetStage = ResolveWorkPath(configuration, journal.AssetStagePath);
            string manifestStage = ResolveWorkPath(configuration, journal.ManifestStagePath);
            string assetBackup = PathPolicy.ResolveOutputPath(
                configuration,
                journal.AssetBackupPath,
                allowMissing: true
            );
            string manifestBackup = PathPolicy.ResolveManifestPath(
                configuration,
                journal.ManifestBackupPath,
                allowMissing: true
            );
            PublicationJournal resolved = journal with
            {
                AssetStagePath = assetStage,
                ManifestStagePath = manifestStage,
                AssetBackupPath = assetBackup,
                ManifestBackupPath = manifestBackup,
            };

            if (NewPairMatches(journal, asset, manifest))
            {
                Complete(journalPath, resolved, configuration);
                return;
            }

            // A backup's presence proves that its old live file already moved. Without a backup, the old
            // file is still live and must not be deleted merely because the paired move was interrupted.
            RestoreOldFile(asset, assetBackup, journal.AssetExisted);
            RestoreOldFile(manifest, manifestBackup, journal.ManifestExisted);
            DeleteIfExists(assetStage);
            DeleteIfExists(manifestStage);
            DeleteIfExists(journalPath);
            DeleteEmptyTransactionDirectory(assetStage, configuration);
        }

        private static string ResolveWorkPath(EffectiveConfiguration configuration, string path) =>
            PathPolicy.ResolveUnderConfiguredRoot(
                configuration.RepositoryRoot,
                configuration.Paths.WorkRoot,
                path,
                "publication work path",
                allowMissing: true
            );

        private static bool NewPairMatches(PublicationJournal journal, string asset, string manifest)
        {
            if (!File.Exists(asset) || !File.Exists(manifest))
            {
                return false;
            }

            byte[] assetBytes = File.ReadAllBytes(asset);
            byte[] manifestBytes = File.ReadAllBytes(manifest);
            if (
                assetBytes.LongLength != journal.AssetLength
                || !string.Equals(Hash(assetBytes), journal.AssetHash, StringComparison.Ordinal)
                || !string.Equals(Hash(manifestBytes), journal.ManifestHash, StringComparison.Ordinal)
            )
            {
                return false;
            }

            try
            {
                YamlMappingNode root = StrictYaml.LoadMapping(manifest);
                YamlMappingNode integrity = root.Mapping("integrity", "manifest");
                return string.Equals(
                        integrity.Scalar("sha256", "manifest.integrity"),
                        journal.AssetHash,
                        StringComparison.Ordinal
                    )
                    && integrity.Long("byte_length", "manifest.integrity") == journal.AssetLength;
            }
            catch (AssetCtlException)
            {
                return false;
            }
        }

        private static void RestoreOldFile(string live, string backup, bool existed)
        {
            if (!existed)
            {
                DeleteIfExists(live);
                DeleteIfExists(backup);
                return;
            }

            if (File.Exists(backup))
            {
                DeleteIfExists(live);
                File.Move(backup, live);
            }
        }

        private static void Complete(
            string journalPath,
            PublicationJournal journal,
            EffectiveConfiguration configuration
        )
        {
            DeleteIfExists(journal.AssetStagePath);
            DeleteIfExists(journal.ManifestStagePath);
            DeleteIfExists(journal.AssetBackupPath);
            DeleteIfExists(journal.ManifestBackupPath);
            DeleteIfExists(journalPath);
            DeleteEmptyTransactionDirectory(journal.AssetStagePath, configuration);
        }

        private static void DeleteEmptyTransactionDirectory(string assetStage, EffectiveConfiguration configuration)
        {
            string workRoot = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                configuration.Paths.WorkRoot,
                "work_root",
                allowMissing: true
            );
            string? transactionRoot = Path.GetDirectoryName(assetStage);
            if (
                transactionRoot is not null
                && transactionRoot.StartsWith(workRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && Directory.Exists(transactionRoot)
                && !Directory.EnumerateFileSystemEntries(transactionRoot).Any()
            )
            {
                Directory.Delete(transactionRoot);
            }
        }

        private static void WriteDurable(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

        private static bool IsSha256(string? value) =>
            value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static void DeleteTree(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private sealed record PublicationJournal(
            string TransactionId,
            string AssetPath,
            string ManifestPath,
            string AssetStagePath,
            string ManifestStagePath,
            string AssetBackupPath,
            string ManifestBackupPath,
            bool AssetExisted,
            bool ManifestExisted,
            string AssetHash,
            long AssetLength,
            string ManifestHash
        );

        private sealed class PreparedPublication : IDisposable
        {
            public PreparedPublication(
                string asset,
                string manifest,
                string journalPath,
                PublicationJournal journal,
                PublicationJournal recoveryJournal,
                string transactionRoot,
                string leasePath,
                FileStream lease
            )
            {
                Asset = asset;
                Manifest = manifest;
                JournalPath = journalPath;
                Journal = journal;
                RecoveryJournal = recoveryJournal;
                this.transactionRoot = transactionRoot;
                this.leasePath = leasePath;
                this.lease = lease;
            }

            private readonly string transactionRoot;
            private readonly string leasePath;
            private readonly FileStream lease;

            public string Asset { get; }

            public string Manifest { get; }

            public string JournalPath { get; }

            public PublicationJournal Journal { get; }

            public PublicationJournal RecoveryJournal { get; }

            public void Dispose()
            {
                lease.Dispose();
                DeleteIfExists(leasePath);
                if (Directory.Exists(transactionRoot) && !Directory.EnumerateFileSystemEntries(transactionRoot).Any())
                {
                    Directory.Delete(transactionRoot);
                }
            }
        }

        private sealed record StagedPublication(
            string TransactionId,
            string TransactionRoot,
            string AssetStage,
            string ManifestStage,
            string AssetHash,
            long AssetLength,
            string ManifestHash,
            string LeasePath,
            FileStream Lease
        );
    }

    public static class ReceiptWriter
    {
        public static string Write(EffectiveConfiguration configuration, string runId, object receipt)
        {
            string root = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                configuration.Paths.ReceiptRoot,
                "receipt_root",
                allowMissing: true
            );
            Directory.CreateDirectory(root);
            if (
                string.IsNullOrWhiteSpace(runId)
                || runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || runId.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || runId.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            )
            {
                throw new AssetCtlException("run_id is not safe for a receipt filename.", 2);
            }

            string path = Path.Combine(root, runId + ".json");
            string stage = path + ".tmp";
            File.WriteAllText(
                stage,
                JsonSerializer.Serialize(receipt, JsonOptions.Indented),
                new System.Text.UTF8Encoding(false)
            );
            File.Move(stage, path, overwrite: false);
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
