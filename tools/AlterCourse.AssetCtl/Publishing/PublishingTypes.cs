using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlterCourse.AssetCtl.Configuration;
using Microsoft.Win32.SafeHandles;
using YamlDotNet.RepresentationModel;

namespace AlterCourse.AssetCtl.Publishing;

internal static partial class PublishingTypes
{
    internal static partial class StateFile
    {
        private const int OpenReadWrite = 0x0002;
        private const int OpenWriteOnly = 0x0001;
        private const int OpenCreate = 0x0040;
        private const int OpenExclusive = 0x0080;
        private const int OpenNonBlocking = 0x0800;
        private const int OpenCloseOnExec = 0x80000;
        private const int OpenDirectory = 0x10000;
        private const int OpenNoFollow = 0x20000;
        private const int LockExclusive = 2;
        private const int LockNonBlocking = 4;

        internal sealed class DirectoryHandle : IDisposable
        {
            private readonly SafeFileHandle _handle;

            private DirectoryHandle(SafeFileHandle handle, string path)
            {
                _handle = handle;
                Path = path;
            }

            public string Path { get; }

            public FileIdentity ObjectIdentity => StateFile.Identity(_handle, directory: true);

            public static DirectoryHandle OpenExisting(string path, string field)
            {
                RequireSupportedPlatform(field);
                string absolute = System.IO.Path.GetFullPath(path);
                int descriptor = Open("/", OpenDirectory | OpenNoFollow | OpenCloseOnExec, 0);
                if (descriptor < 0)
                {
                    throw new AssetCtlException($"{field}: root directory is unavailable.", 7);
                }

                var current = new DirectoryHandle(new SafeFileHandle(descriptor, ownsHandle: true), "/");
                try
                {
                    // O_NOFOLLOW applies only to the basename. Walking relative to held descriptors
                    // admits every ancestor, rather than trusting a prior full-path symlink check.
                    foreach (string component in absolute.Split('/', StringSplitOptions.RemoveEmptyEntries))
                    {
                        DirectoryHandle next = current.OpenChild(component, field);
                        current.Dispose();
                        current = next;
                    }

                    return current;
                }
                catch
                {
                    current.Dispose();
                    throw;
                }
            }

            public DirectoryHandle CreateChild(string leaf, string field)
            {
                if (MkdirAt(Descriptor, leaf, 0x1C0) != 0)
                {
                    throw new AssetCtlException($"{field}: directory could not be created safely.", 7);
                }

                int descriptor = OpenAt(Descriptor, leaf, OpenDirectory | OpenNoFollow | OpenCloseOnExec, 0);
                if (descriptor < 0)
                {
                    throw new AssetCtlException($"{field}: directory is unavailable or unsafe.", 7);
                }

                return new DirectoryHandle(
                    new SafeFileHandle(descriptor, ownsHandle: true),
                    System.IO.Path.Combine(Path, leaf)
                );
            }

            public DirectoryHandle OpenChild(string leaf, string field)
            {
                int descriptor = OpenAt(Descriptor, leaf, OpenDirectory | OpenNoFollow | OpenCloseOnExec, 0);
                if (descriptor < 0)
                {
                    throw new AssetCtlException($"{field}: directory is unavailable or unsafe.", 7);
                }

                return new DirectoryHandle(
                    new SafeFileHandle(descriptor, ownsHandle: true),
                    System.IO.Path.Combine(Path, leaf)
                );
            }

            public FileStream OpenReadFile(
                string leaf,
                string field,
                Func<SafeFileHandle, FileStream>? streamFactory = null
            )
            {
                int descriptor = OpenAt(Descriptor, leaf, OpenNoFollow | OpenCloseOnExec | OpenNonBlocking, 0);
                if (descriptor < 0)
                {
                    throw new AssetCtlException($"{field}: file is unavailable or unsafe.", 7);
                }

                var handle = new SafeFileHandle(descriptor, ownsHandle: true);
                try
                {
                    // Nonblocking open reaches regular-file admission even for substituted FIFOs.
                    // Disable read-ahead so byte admission also bounds the actual OS reads.
                    _ = Identity(handle);
                    return streamFactory is null
                        ? new FileStream(handle, FileAccess.Read, bufferSize: 1)
                        : streamFactory(handle);
                }
                catch
                {
                    // FileStream takes ownership only after successful construction.
                    handle.Dispose();
                    throw;
                }
            }

            public void DeleteFile(string leaf, string field)
            {
                if (UnlinkAt(Descriptor, leaf, 0) != 0)
                {
                    throw new AssetCtlException($"{field}: descriptor-bound cleanup failed.", 7);
                }
            }

            public FileStream CreateFile(
                string leaf,
                string field,
                Func<SafeFileHandle, FileStream>? streamFactory = null
            ) => CreateFile(leaf, field, out _, streamFactory);

            public FileStream CreateFile(
                string leaf,
                string field,
                out FileIdentity? identity,
                Func<SafeFileHandle, FileStream>? streamFactory = null
            )
            {
                identity = null;
                int descriptor = OpenAt(
                    Descriptor,
                    leaf,
                    OpenWriteOnly | OpenCreate | OpenExclusive | OpenNoFollow | OpenCloseOnExec,
                    0x180
                );
                if (descriptor < 0)
                {
                    throw new AssetCtlException($"{field}: file could not be created safely.", 7);
                }

                var handle = new SafeFileHandle(descriptor, ownsHandle: true);
                try
                {
                    identity = Identity(handle);
                    return streamFactory is null
                        ? new FileStream(handle, FileAccess.Write, bufferSize: 1)
                        : streamFactory(handle);
                }
                catch
                {
                    // Retain the native handle until owned-leaf cleanup finishes. A failed
                    // constructor never acquired stream ownership, and its error remains primary.
                    try
                    {
                        using FileStream named = OpenReadFile(leaf, field);
                        if (identity is not null && Identity(named) == identity)
                        {
                            DeleteFile(leaf, field);
                        }
                    }
                    catch (Exception) { }
                    handle.Dispose();
                    throw;
                }
            }

            public FileStream CreateLockedFile(string leaf, string field)
            {
                int descriptor = OpenAt(
                    Descriptor,
                    leaf,
                    OpenReadWrite | OpenCreate | OpenExclusive | OpenNoFollow | OpenCloseOnExec,
                    0x180
                );
                if (descriptor < 0)
                {
                    throw new AssetCtlException($"{field}: file could not be created safely.", 7);
                }

                var fileHandle = new SafeFileHandle(descriptor, ownsHandle: true);
                if (Flock(fileHandle, LockExclusive | LockNonBlocking) != 0)
                {
                    fileHandle.Dispose();
                    throw new AssetCtlException($"{field}: file could not be locked safely.", 7);
                }

                try
                {
                    return new FileStream(fileHandle, FileAccess.ReadWrite);
                }
                catch
                {
                    fileHandle.Dispose();
                    throw;
                }
            }

            public FileStream OpenLockedFile(string leaf, string field)
            {
                int descriptor = OpenAt(
                    Descriptor,
                    leaf,
                    OpenReadWrite | OpenCreate | OpenNoFollow | OpenNonBlocking | OpenCloseOnExec,
                    0x180
                );
                if (descriptor < 0)
                    throw new AssetCtlException($"{field}: lock file is unavailable or unsafe.", 7);
                var handle = new SafeFileHandle(descriptor, ownsHandle: true);
                try
                {
                    _ = StateFile.Identity(handle);
                    if (Flock(handle, LockExclusive | LockNonBlocking) != 0)
                        throw new IOException("Publication transaction is active.");
                    return new FileStream(handle, FileAccess.ReadWrite, bufferSize: 1);
                }
                catch
                {
                    handle.Dispose();
                    throw;
                }
            }

            public string DescriptorPath(string leaf) => $"/proc/self/fd/{Descriptor}/{leaf}";

            public void MoveTo(string sourceLeaf, string destination, string field)
            {
                string destinationDirectory = System.IO.Path.GetDirectoryName(destination)!;
                using DirectoryHandle target = OpenExisting(destinationDirectory, field);
                if (RenameAt(Descriptor, sourceLeaf, target.Descriptor, System.IO.Path.GetFileName(destination)) != 0)
                {
                    throw new AssetCtlException($"{field}: descriptor-bound move failed.", 7);
                }
            }

            public void MoveTo(string sourceLeaf, DirectoryHandle target, string destinationLeaf, string field)
            {
                if (RenameAt(Descriptor, sourceLeaf, target.Descriptor, destinationLeaf) != 0)
                {
                    throw new AssetCtlException($"{field}: descriptor-bound move failed.", 7);
                }
            }

            public void EnsureStillNamed(string field)
            {
                string? boundPath = new FileInfo($"/proc/self/fd/{Descriptor}").LinkTarget;
                if (
                    boundPath is null
                    || !string.Equals(
                        System.IO.Path.GetFullPath(boundPath),
                        System.IO.Path.GetFullPath(Path),
                        StringComparison.Ordinal
                    )
                )
                {
                    throw new AssetCtlException($"{field}: directory changed during the operation.", 7);
                }
            }

            public void Dispose() => _handle.Dispose();

            private int Descriptor => _handle.DangerousGetHandle().ToInt32();
        }

        public static FileStream OpenLockedLeaf(string directory, string leaf, string field)
        {
            RequireSupportedPlatform(field);

            int directoryDescriptor = Open(directory, OpenDirectory | OpenNoFollow | OpenCloseOnExec, 0);
            if (directoryDescriptor < 0)
            {
                throw new AssetCtlException($"{field}: lock directory is unavailable or unsafe.", 7);
            }

            try
            {
                int descriptor = OpenAt(
                    directoryDescriptor,
                    leaf,
                    OpenReadWrite | OpenCreate | OpenNoFollow | OpenCloseOnExec,
                    0x180
                );
                if (descriptor < 0)
                {
                    throw new AssetCtlException($"{field}: symbolic links are prohibited.", 7);
                }

                var handle = new SafeFileHandle(descriptor, ownsHandle: true);
                if (Flock(handle, LockExclusive | LockNonBlocking) != 0)
                {
                    handle.Dispose();
                    throw new IOException($"{field} is already locked.");
                }

                // The directory descriptor and O_NOFOLLOW bind validation to this open. A path check alone
                // permits a replacement symlink to redirect the state write between validation and opening.
                try
                {
                    return new FileStream(handle, FileAccess.ReadWrite);
                }
                catch
                {
                    handle.Dispose();
                    throw;
                }
            }
            finally
            {
                _ = Close(directoryDescriptor);
            }
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly record struct FileIdentity(ulong Inode, uint DeviceMajor, uint DeviceMinor);

        internal static FileIdentity Identity(FileStream stream) => Identity(stream.SafeFileHandle);

        private static FileIdentity Identity(SafeFileHandle handle, bool directory = false)
        {
            RequireSupportedPlatform("Lifecycle evidence");
            if (
                Statx(handle, "", 0x1000, 0x7FF, out FileStat status) != 0
                || (status.Mask & 0x103) != 0x103
                || (status.Mode & 0xF000) != (directory ? 0x4000 : 0x8000)
            )
            {
                throw new AssetCtlException("Lifecycle evidence must be a regular Linux file.", 7);
            }

            return new FileIdentity(status.Inode, status.DeviceMajor, status.DeviceMinor);
        }

        // statx has a fixed Linux ABI, unlike architecture-dependent struct stat. Only identity
        // and regular-file admission are needed; timestamps are not used as content evidence.
        [StructLayout(LayoutKind.Explicit, Size = 256)]
        private struct FileStat
        {
            [FieldOffset(0)]
            public uint Mask;

            [FieldOffset(28)]
            public ushort Mode;

            [FieldOffset(32)]
            public ulong Inode;

            [FieldOffset(136)]
            public uint DeviceMajor;

            [FieldOffset(140)]
            public uint DeviceMinor;
        }

        [LibraryImport("libc", EntryPoint = "statx", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        private static partial int Statx(
            SafeFileHandle descriptor,
            string path,
            int flags,
            uint mask,
            out FileStat status
        );

        // Admission for every libc call in StateFile. OpenExisting and OpenLockedLeaf are the only
        // descriptor sources (DirectoryHandle's constructor is private), and Identity guards statx
        // for callers holding an arbitrary FileStream, so each native call is reached only after
        // this check. A new native entry point must call it before its first libc call.
        private static void RequireSupportedPlatform(string field) =>
            RequireSupportedPlatform(field, OperatingSystem.IsLinux(), RuntimeInformation.ProcessArchitecture);

        // The open-flag constants at the top of StateFile are the Linux x86-64 (asm-generic) values.
        // On Linux Arm and Arm64, 0x10000 and 0x20000 mean O_DIRECT and O_LARGEFILE, so the same
        // calls would succeed while silently dropping O_DIRECTORY and O_NOFOLLOW: a symlink
        // substitution would then be followed instead of refused. Admitting only the validated
        // ABI turns that silent loss into a refusal. ProcessArchitecture, not OSArchitecture, is
        // the ABI that libc is called through (an x64 process under emulation uses x64 headers).
        // Per-architecture constant tables were rejected: no other ABI is executed or tested.
        internal static void RequireSupportedPlatform(string field, bool isLinux, Architecture processArchitecture)
        {
            if (!isLinux)
            {
                throw new AssetCtlException($"{field}: secure descriptor-bound state access requires Linux.", 7);
            }

            if (processArchitecture != Architecture.X64)
            {
                throw new AssetCtlException(
                    $"{field}: secure descriptor-bound state access requires an x64 process; "
                        + $"{processArchitecture} is not a validated native ABI.",
                    7
                );
            }
        }

        [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        private static partial int Open(string path, int flags, uint mode);

        [LibraryImport("libc", EntryPoint = "openat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        private static partial int OpenAt(int directoryDescriptor, string path, int flags, uint mode);

        [LibraryImport("libc", EntryPoint = "mkdirat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        private static partial int MkdirAt(int directoryDescriptor, string path, uint mode);

        [LibraryImport(
            "libc",
            EntryPoint = "renameat",
            SetLastError = true,
            StringMarshalling = StringMarshalling.Utf8
        )]
        private static partial int RenameAt(
            int oldDirectoryDescriptor,
            string oldPath,
            int newDirectoryDescriptor,
            string newPath
        );

        [LibraryImport(
            "libc",
            EntryPoint = "unlinkat",
            SetLastError = true,
            StringMarshalling = StringMarshalling.Utf8
        )]
        private static partial int UnlinkAt(int directoryDescriptor, string path, int flags);

        [LibraryImport("libc", EntryPoint = "flock", SetLastError = true)]
        private static partial int Flock(SafeFileHandle descriptor, int operation);

        [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
        private static partial int Close(int descriptor);
    }

    public sealed class AssetLock : IDisposable
    {
        private readonly FileStream _stream;

        private AssetLock(FileStream stream) => _stream = stream;

        public static AssetLock Acquire(EffectiveConfiguration configuration, string assetId)
        {
            string stateRoot = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                configuration.Paths.StateRoot,
                "state_root",
                allowMissing: true
            );
            string lockRoot = Path.Combine(stateRoot, "locks");
            RejectReparsePoint(lockRoot, "asset lock directory");
            Directory.CreateDirectory(lockRoot);
            RejectReparsePoint(lockRoot, "asset lock directory");
            FileStream lockStream;
            try
            {
                lockStream = StateFile.OpenLockedLeaf(lockRoot, "catalog.lock", "asset catalog lock");
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

        public void Dispose() => _stream.Dispose();

        internal static void RejectReparsePoint(string path, string field)
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            if (info.LinkTarget is not null)
            {
                throw new AssetCtlException($"{field}: symbolic links are prohibited.", 7);
            }
        }
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
            Action<string>? BeforeWorkFilesWritten = null,
            Action<string, string>? AfterWorkFilesStaged = null,
            Action<PublicationMove>? BeforeMove = null,
            Action<PublicationMove>? AfterMove = null,
            Action<string>? BeforeQuarantineMove = null
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
            PublishPrepared(publication, testHooks);
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
            PublicationBoundary? admission = null;
            try
            {
                admission = new PublicationBoundary(configuration, asset, manifest, staged.TransactionDirectory);
                PublicationJournal journal = CreateJournal(configuration, staged, admission, assetRelativePath);
                // The ignored state journal is only a recovery locator. A separately owned transaction
                // envelope, admitted objects, and mutable-lifecycle semantic ownership authorize mutation.
                admission.WriteAuthority(journal);
                string journalPath = admission.WriteJournal(journal);
                return new PreparedPublication(journalPath, journal, staged, admission);
            }
            catch
            {
                admission?.Dispose();
                try
                {
                    DisposeStaged(staged);
                }
                catch (Exception) { }
                throw;
            }
        }

        private static PublicationJournal CreateJournal(
            EffectiveConfiguration configuration,
            StagedPublication staged,
            PublicationBoundary admission,
            string assetRelativePath
        )
        {
            string asset = Path.Combine(admission.AssetParent.Path, admission.AssetLeaf);
            string manifest = Path.Combine(admission.ManifestParent.Path, admission.ManifestLeaf);
            FileEvidence? oldAsset = admission.ReadAsset();
            FileEvidence? oldManifest = admission.ReadManifest();
            if (oldManifest is null)
            {
                throw new AssetCtlException("Publication requires its owning manifest predecessor.", 7);
            }
            FileEvidence candidateAsset = admission.ReadStage("asset.stage", configuration.Limits.MaximumDownloadBytes);
            FileEvidence candidateManifest = admission.ReadStage("manifest.stage", YamlValues.MaximumBytes);
            if (
                candidateAsset.Identity != staged.AssetIdentity
                || candidateManifest.Identity != staged.ManifestIdentity
            )
                throw new AssetCtlException("Publication stage ownership changed before admission.", 7);
            string assetId = PublicationBoundary.ValidateManifest(
                "manifest.stage",
                staged.TransactionDirectory,
                null,
                assetRelativePath
            );
            PublicationBoundary.ValidateManifest(
                admission.ManifestLeaf,
                admission.ManifestParent,
                assetId,
                assetRelativePath
            );
            return new PublicationJournal(
                staged.TransactionId,
                Path.GetRelativePath(configuration.RepositoryRoot, asset),
                Path.GetRelativePath(configuration.RepositoryRoot, manifest),
                Path.GetRelativePath(configuration.RepositoryRoot, staged.AssetStage),
                Path.GetRelativePath(configuration.RepositoryRoot, staged.ManifestStage),
                Path.GetRelativePath(configuration.RepositoryRoot, asset) + $".assetctl-backup-{staged.TransactionId}",
                Path.GetRelativePath(configuration.RepositoryRoot, manifest)
                    + $".assetctl-backup-{staged.TransactionId}",
                oldAsset is not null,
                true,
                staged.AssetHash,
                staged.AssetLength,
                staged.ManifestHash,
                1,
                assetId,
                admission.AssetParent.ObjectIdentity,
                admission.ManifestParent.ObjectIdentity,
                staged.TransactionDirectory.ObjectIdentity,
                oldAsset,
                oldManifest,
                candidateAsset,
                candidateManifest
            );
        }

        private static void DisposeStaged(StagedPublication staged)
        {
            DeleteOwnedIdentity(staged.TransactionDirectory, "asset.stage", staged.AssetIdentity);
            DeleteOwnedIdentity(staged.TransactionDirectory, "manifest.stage", staged.ManifestIdentity);
            DeleteOwnedIdentity(staged.TransactionDirectory, LeaseFileName, staged.LeaseIdentity);
            try
            {
                staged.Lease.Dispose();
            }
            catch (Exception) { }
            staged.TransactionDirectory.Dispose();
        }

        private static StagedPublication StagePublication(
            EffectiveConfiguration configuration,
            byte[] assetBytes,
            string manifestText,
            PublicationTestHooks? testHooks
        )
        {
            string transaction = Guid.NewGuid().ToString("N");
            StateFile.DirectoryHandle transactionDirectory = CreateTransactionDirectory(configuration, transaction);
            string transactionRoot = transactionDirectory.Path;
            string leasePath = Path.Combine(transactionRoot, LeaseFileName);
            FileStream? lease = null;
            StateFile.FileIdentity? leaseIdentity = null;
            StateFile.FileIdentity? assetIdentity = null;
            StateFile.FileIdentity? manifestIdentity = null;
            string assetStage = Path.Combine(transactionRoot, "asset.stage");
            string manifestStage = Path.Combine(transactionRoot, "manifest.stage");
            try
            {
                testHooks?.BeforeWorkFilesWritten?.Invoke(transactionRoot);
                transactionDirectory.EnsureStillNamed("publication transaction root");
                (lease, leaseIdentity) = CreatePublicationLease(transactionDirectory);
                assetIdentity = WriteDurable(transactionDirectory, "asset.stage", assetBytes, "staged asset");
                manifestIdentity = WriteDurable(
                    transactionDirectory,
                    "manifest.stage",
                    new UTF8Encoding(false).GetBytes(manifestText),
                    "staged manifest"
                );
                testHooks?.AfterWorkFilesStaged?.Invoke(assetStage, manifestStage);
                transactionDirectory.EnsureStillNamed("publication transaction root");
                (string assetHash, long assetLength, string manifestHash) = VerifyStaged(
                    transactionDirectory,
                    assetBytes,
                    manifestText,
                    configuration.Limits.MaximumDownloadBytes
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
                    lease,
                    transactionDirectory,
                    assetIdentity.Value,
                    manifestIdentity.Value,
                    leaseIdentity.Value
                );
            }
            catch
            {
                DeleteOwnedIdentity(transactionDirectory, "asset.stage", assetIdentity);
                DeleteOwnedIdentity(transactionDirectory, "manifest.stage", manifestIdentity);
                DeleteOwnedIdentity(transactionDirectory, LeaseFileName, leaseIdentity);
                try
                {
                    lease?.Dispose();
                }
                catch (Exception) { }
                transactionDirectory.Dispose();
                throw;
            }
        }

        private static (FileStream Stream, StateFile.FileIdentity Identity) CreatePublicationLease(
            StateFile.DirectoryHandle directory
        )
        {
            FileStream stream = directory.CreateLockedFile(LeaseFileName, "publication lease");
            StateFile.FileIdentity? identity = null;
            try
            {
                identity = StateFile.Identity(stream);
                JsonSerializer.Serialize(
                    stream,
                    new { process_id = Environment.ProcessId, acquired_at = DateTimeOffset.UtcNow },
                    JournalJsonOptions
                );
                stream.Flush(flushToDisk: true);
                return (stream, identity.Value);
            }
            catch
            {
                DeleteOwnedIdentity(directory, LeaseFileName, identity);
                try
                {
                    stream.Dispose();
                }
                catch (Exception) { }
                throw;
            }
        }

        private static StateFile.DirectoryHandle CreateTransactionDirectory(
            EffectiveConfiguration configuration,
            string transaction
        )
        {
            string workRoot = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                configuration.Paths.WorkRoot,
                "work_root",
                allowMissing: true
            );
            string publishRoot = EnsureFixedDirectory(workRoot, "publish", "publication staging root");
            using var publishDirectory = StateFile.DirectoryHandle.OpenExisting(
                publishRoot,
                "publication staging root"
            );
            return publishDirectory.CreateChild(transaction, "publication transaction root");
        }

        private static void PublishPrepared(PreparedPublication publication, PublicationTestHooks? testHooks)
        {
            PublicationJournal journal = publication.Journal;
            PublicationBoundary boundary = publication.Boundary;
            try
            {
                boundary.ValidateAuthority(journal);
                InstallPrepared(publication, testHooks);
                boundary.Recover(journal);
            }
            catch (SimulatedPublicationInterruptionException)
            {
                // This test-only exception represents process loss, so recovery evidence must survive.
                throw;
            }
            catch
            {
                if (!publication.Committed)
                {
                    // Rollback uses the admitted parents even if their old names now resolve elsewhere.
                    // A secondary recovery/cleanup failure must never replace the original refusal.
                    try
                    {
                        boundary.Recover(journal);
                    }
                    catch (Exception) { }
                    throw;
                }
                // Successful manifest replacement commits publication. Reporting and cleanup failures
                // after that point leave recoverable evidence rather than claiming refusal or rollback.
            }
        }

        private static void InstallPrepared(PreparedPublication publication, PublicationTestHooks? testHooks)
        {
            PublicationJournal journal = publication.Journal;
            PublicationBoundary boundary = publication.Boundary;
            if (journal.AssetExisted)
            {
                boundary.Move(
                    boundary.AssetParent,
                    boundary.AssetLeaf,
                    boundary.AssetParent,
                    boundary.AssetBackupLeaf(journal),
                    journal.AssetPredecessor!,
                    PublicationMove.BackupAsset,
                    testHooks
                );
            }
            boundary.Move(
                boundary.ManifestParent,
                boundary.ManifestLeaf,
                boundary.ManifestParent,
                boundary.ManifestBackupLeaf(journal),
                journal.ManifestPredecessor!,
                PublicationMove.BackupManifest,
                testHooks
            );
            boundary.Move(
                boundary.TransactionDirectory,
                "asset.stage",
                boundary.AssetParent,
                boundary.AssetLeaf,
                journal.AssetCandidate!,
                PublicationMove.InstallAsset,
                testHooks
            );
            boundary.Move(
                boundary.TransactionDirectory,
                "manifest.stage",
                boundary.ManifestParent,
                boundary.ManifestLeaf,
                journal.ManifestCandidate!,
                PublicationMove.InstallManifest,
                testHooks,
                () => publication.Committed = true
            );
        }

        internal static PublicationRecoveryResult RecoverPending(
            EffectiveConfiguration configuration,
            PublicationTestHooks? testHooks = null
        )
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
                catch (Exception exception)
                    when (exception is JsonException or InvalidDataException or AssetCtlException)
                {
                    _ = QuarantineJournal(journalRoot, path, testHooks);
                    throw new AssetCtlException("Publication journal is invalid and was quarantined.", 7);
                }

                RecoveryLease? recoveryLease = null;
                try
                {
                    recoveryLease = TryAcquireRecoveryLease(configuration, journal.TransactionId);
                    if (recoveryLease is null)
                    {
                        active++;
                        continue;
                    }
                    RecoverJournal(configuration, path, journal, recoveryLease.Directory);
                    recovered++;
                }
                catch (AssetCtlException)
                {
                    _ = QuarantineJournal(journalRoot, path, testHooks);
                    throw new AssetCtlException("Publication journal is unsafe and was quarantined.", 7);
                }
                finally
                {
                    recoveryLease?.Dispose();
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

            byte[] bytes = global::AlterCourse.AssetCtl.Catalog.SelectedAssetReader.Read(path, MaximumJournalBytes);
            if (bytes.Length == 0)
                throw new InvalidDataException("empty journal");
            return JsonSerializer.Deserialize<PublicationJournal>(bytes, JournalJsonOptions)
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
                || journal.AuthorityVersion != 1
                || string.IsNullOrWhiteSpace(journal.AssetId)
                || journal.ManifestPredecessor is null
                || journal.AssetCandidate is null
                || journal.ManifestCandidate is null
                || journal.AssetExisted != (journal.AssetPredecessor is not null)
                || !journal.ManifestExisted
            )
            {
                throw new InvalidDataException("journal fields do not satisfy the publication contract");
            }
        }

        private static RecoveryLease? TryAcquireRecoveryLease(
            EffectiveConfiguration configuration,
            string transactionId
        )
        {
            string transactionRoot = ResolveWorkPath(
                configuration,
                Path.Combine(configuration.Paths.WorkRoot, "publish", transactionId)
            );
            var directory = StateFile.DirectoryHandle.OpenExisting(transactionRoot, "publication recovery transaction");
            try
            {
                FileStream stream = directory.OpenLockedFile(LeaseFileName, "publication recovery lease");
                return new RecoveryLease(directory, stream);
            }
            catch (IOException)
            {
                // Kernel lease contention distinguishes an active publisher without a clock-age policy.
                directory.Dispose();
                return null;
            }
            catch
            {
                directory.Dispose();
                throw;
            }
        }

        private sealed class RecoveryLease : IDisposable
        {
            private readonly FileStream _stream;
            private readonly StateFile.FileIdentity _identity;

            public RecoveryLease(StateFile.DirectoryHandle directory, FileStream stream)
            {
                Directory = directory;
                _stream = stream;
                try
                {
                    _identity = StateFile.Identity(stream);
                }
                catch
                {
                    try
                    {
                        stream.Dispose();
                    }
                    catch (Exception) { }
                    throw;
                }
            }

            public StateFile.DirectoryHandle Directory { get; }

            public void Dispose()
            {
                try
                {
                    using FileStream named = Directory.OpenReadFile(
                        LeaseFileName,
                        "publication recovery lease cleanup"
                    );
                    if (StateFile.Identity(named) == _identity)
                        Directory.DeleteFile(LeaseFileName, "publication recovery lease cleanup");
                }
                catch (Exception) { }
                try
                {
                    _stream.Dispose();
                }
                catch (Exception) { }
                Directory.Dispose();
            }
        }

        private static string QuarantineJournal(string journalRoot, string path, PublicationTestHooks? testHooks)
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

            string quarantineRoot = EnsureFixedDirectory(fullRoot, "quarantine", "publication quarantine root");
            string destination = Path.Combine(
                quarantineRoot,
                Path.GetFileNameWithoutExtension(fullPath) + "." + Guid.NewGuid().ToString("N") + ".invalid"
            );
            using var journalDirectory = StateFile.DirectoryHandle.OpenExisting(fullRoot, "publication journal root");
            using StateFile.DirectoryHandle quarantineDirectory = journalDirectory.OpenChild(
                "quarantine",
                "publication quarantine root"
            );
            testHooks?.BeforeQuarantineMove?.Invoke(quarantineRoot);
            journalDirectory.MoveTo(
                Path.GetFileName(fullPath),
                quarantineDirectory,
                Path.GetFileName(destination),
                "publication journal quarantine"
            );
            return destination;
        }

        private static (string AssetHash, long AssetLength, string ManifestHash) VerifyStaged(
            StateFile.DirectoryHandle transactionDirectory,
            byte[] expectedAssetBytes,
            string expectedManifestText,
            long maximumAssetBytes
        )
        {
            string manifestStage = transactionDirectory.DescriptorPath("manifest.stage");
            byte[] stagedAsset = global::AlterCourse.AssetCtl.Catalog.SelectedAssetReader.Read(
                transactionDirectory,
                "asset.stage",
                Math.Min(expectedAssetBytes.LongLength, maximumAssetBytes)
            );
            byte[] expectedManifestBytes = new UTF8Encoding(false).GetBytes(expectedManifestText);
            byte[] stagedManifest = global::AlterCourse.AssetCtl.Catalog.SelectedAssetReader.Read(
                transactionDirectory,
                "manifest.stage",
                Math.Min(expectedManifestBytes.LongLength, YamlValues.MaximumBytes)
            );
            string assetHash = Hash(stagedAsset);
            string expectedAssetHash = Hash(expectedAssetBytes);
            string manifestHash = Hash(stagedManifest);
            string expectedManifestHash = Hash(expectedManifestBytes);
            if (
                !string.Equals(assetHash, expectedAssetHash, StringComparison.Ordinal)
                || !string.Equals(manifestHash, expectedManifestHash, StringComparison.Ordinal)
            )
            {
                throw new AssetCtlException("Staged publication bytes changed before verification.", 7);
            }

            YamlMappingNode root = StrictYaml.LoadBytes(manifestStage, stagedManifest);
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

        private static string JournalRoot(EffectiveConfiguration configuration)
        {
            string stateRoot = PathPolicy.ResolveUnder(
                configuration.RepositoryRoot,
                configuration.Paths.StateRoot,
                "state_root",
                allowMissing: true
            );
            string journalRoot = Path.Combine(stateRoot, "publish-transactions");
            if (
                (File.Exists(journalRoot) || Directory.Exists(journalRoot))
                && (File.GetAttributes(journalRoot) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint
            )
            {
                throw new AssetCtlException("Publication journal root cannot be a symbolic link.", 7);
            }
            return journalRoot;
        }

        private static string EnsureFixedDirectory(string root, string child, string field)
        {
            string path = Path.Combine(root, child);
            AssetLock.RejectReparsePoint(path, field);
            Directory.CreateDirectory(path);
            AssetLock.RejectReparsePoint(path, field);
            return path;
        }

        private static void RecoverJournal(
            EffectiveConfiguration configuration,
            string journalPath,
            PublicationJournal journal,
            StateFile.DirectoryHandle transaction
        )
        {
            string asset = PathPolicy.ResolveOutputPath(configuration, journal.AssetPath, allowMissing: true);
            string manifest = PathPolicy.ResolveManifestPath(configuration, journal.ManifestPath, allowMissing: true);
            using var boundary = new PublicationBoundary(configuration, asset, manifest, transaction);
            boundary.SetJournal(journalPath, journal);
            boundary.ValidateAuthority(journal);
            boundary.Recover(journal);
        }

        private static string ResolveWorkPath(EffectiveConfiguration configuration, string path) =>
            PathPolicy.ResolveUnderConfiguredRoot(
                configuration.RepositoryRoot,
                configuration.Paths.WorkRoot,
                path,
                "publication work path",
                allowMissing: true
            );

        private static StateFile.FileIdentity WriteDurable(
            StateFile.DirectoryHandle directory,
            string leaf,
            byte[] bytes,
            string field
        )
        {
            FileStream? stream = null;
            StateFile.FileIdentity? identity = null;
            try
            {
                stream = directory.CreateFile(leaf, field, out identity);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
                stream.Dispose();
                stream = null;
                return identity!.Value;
            }
            catch
            {
                try
                {
                    stream?.Dispose();
                }
                catch (Exception) { }
                DeleteOwnedIdentity(directory, leaf, identity);
                throw;
            }
        }

        private static void DeleteOwnedIdentity(
            StateFile.DirectoryHandle directory,
            string leaf,
            StateFile.FileIdentity? identity
        )
        {
            if (identity is null)
                return;
            try
            {
                using FileStream named = directory.OpenReadFile(leaf, "publication stage cleanup");
                if (StateFile.Identity(named) == identity)
                    directory.DeleteFile(leaf, "publication stage cleanup");
            }
            catch (Exception) { }
        }

        private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

        private static bool IsSha256(string? value) =>
            value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        // Version-zero legacy journals contain only assertions and cannot authorize destructive recovery.
        // They are quarantined; operators must preserve and inspect their predecessor artifacts manually.
        // This version applies only to ignored local recovery state, not selected manifests or receipts.
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
            string ManifestHash,
            int AuthorityVersion = 0,
            string? AssetId = null,
            StateFile.FileIdentity AssetParentIdentity = default,
            StateFile.FileIdentity ManifestParentIdentity = default,
            StateFile.FileIdentity TransactionIdentity = default,
            FileEvidence? AssetPredecessor = null,
            FileEvidence? ManifestPredecessor = null,
            FileEvidence? AssetCandidate = null,
            FileEvidence? ManifestCandidate = null
        );

        private sealed record FileEvidence(StateFile.FileIdentity Identity, string Hash, long Length);

        /// <summary>
        /// Pins Linux publication parents and admits owned file snapshots before any rename or cleanup.
        /// The envelope corroborates a journal; neither authenticates arbitrary same-UID rewrites of all
        /// evidence. Cooperating callers retain AssetLock, and renameat is not a two-file revision CAS.
        /// </summary>
        private sealed class PublicationBoundary : IDisposable
        {
            private readonly EffectiveConfiguration _configuration;
            private readonly StateFile.DirectoryHandle _journalParent;
            private string? _journalLeaf;
            private FileEvidence? _journalEvidence;
            private FileEvidence? _authorityEvidence;
            private readonly FileEvidence? _leaseEvidence;

            public PublicationBoundary(
                EffectiveConfiguration configuration,
                string asset,
                string manifest,
                StateFile.DirectoryHandle transactionDirectory
            )
            {
                _configuration = configuration;
                TransactionDirectory = transactionDirectory;
                AssetLeaf = Path.GetFileName(asset);
                ManifestLeaf = Path.GetFileName(manifest);
                AssetParent = StateFile.DirectoryHandle.OpenExisting(
                    Path.GetDirectoryName(asset)!,
                    "publication asset parent"
                );
                try
                {
                    ManifestParent = StateFile.DirectoryHandle.OpenExisting(
                        Path.GetDirectoryName(manifest)!,
                        "publication manifest parent"
                    );
                    try
                    {
                        string journalRoot = JournalRoot(configuration);
                        Directory.CreateDirectory(journalRoot);
                        _leaseEvidence = ReadEvidence(
                            TransactionDirectory,
                            LeaseFileName,
                            MaximumJournalBytes
                        )?.Evidence;
                        _journalParent = StateFile.DirectoryHandle.OpenExisting(
                            journalRoot,
                            "publication journal parent"
                        );
                    }
                    catch
                    {
                        ManifestParent.Dispose();
                        throw;
                    }
                }
                catch
                {
                    AssetParent.Dispose();
                    throw;
                }
            }

            public StateFile.DirectoryHandle AssetParent { get; }
            public StateFile.DirectoryHandle ManifestParent { get; }
            public StateFile.DirectoryHandle TransactionDirectory { get; }
            public string AssetLeaf { get; }
            public string ManifestLeaf { get; }

            public string AssetBackupLeaf(PublicationJournal journal) =>
                AssetLeaf + ".assetctl-backup-" + journal.TransactionId;

            public string ManifestBackupLeaf(PublicationJournal journal) =>
                ManifestLeaf + ".assetctl-backup-" + journal.TransactionId;

            public FileEvidence? ReadAsset() =>
                ReadEvidence(AssetParent, AssetLeaf, _configuration.Limits.MaximumDownloadBytes)?.Evidence;

            public FileEvidence? ReadManifest() =>
                ReadEvidence(ManifestParent, ManifestLeaf, YamlValues.MaximumBytes)?.Evidence;

            public FileEvidence ReadStage(string leaf, long limit) =>
                ReadEvidence(TransactionDirectory, leaf, limit)?.Evidence ?? throw Unsafe();

            public void WriteAuthority(PublicationJournal journal)
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(journal, JournalJsonOptions);
                StateFile.FileIdentity identity = WriteDurable(
                    TransactionDirectory,
                    "authority.json",
                    bytes,
                    "publication authority"
                );
                _authorityEvidence = new FileEvidence(identity, Hash(bytes), bytes.LongLength);
            }

            public string WriteJournal(PublicationJournal journal)
            {
                string leaf = journal.TransactionId + ".json";
                string stage = leaf + ".tmp";
                WriteDurable(
                    _journalParent,
                    stage,
                    JsonSerializer.SerializeToUtf8Bytes(journal, JournalJsonOptions),
                    "publication journal"
                );
                if (ReadEvidence(_journalParent, leaf, MaximumJournalBytes) is not null)
                    throw Unsafe();
                _journalParent.MoveTo(stage, _journalParent, leaf, "publication journal install");
                return Path.Combine(_journalParent.Path, leaf);
            }

            public void SetJournal(string path, PublicationJournal journal)
            {
                _journalLeaf = Path.GetFileName(path);
                FileSnapshot snapshot =
                    ReadEvidence(_journalParent, _journalLeaf, MaximumJournalBytes) ?? throw Unsafe();
                if (
                    !snapshot
                        .Bytes.AsSpan()
                        .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(journal, JournalJsonOptions))
                )
                    throw Unsafe();
                _journalEvidence = snapshot.Evidence;
            }

            public static string ValidateManifest(
                string leaf,
                StateFile.DirectoryHandle parent,
                string? assetId,
                string assetPath
            )
            {
                FileSnapshot snapshot = ReadEvidence(parent, leaf, YamlValues.MaximumBytes) ?? throw Unsafe();
                return ValidateManifest(snapshot, assetId, assetPath);
            }

            private static string ValidateManifest(FileSnapshot snapshot, string? assetId, string assetPath)
            {
                YamlMappingNode root = StrictYaml.LoadBytes("publication manifest", snapshot.Bytes);
                string id = root.Scalar("id", "manifest");
                string lifecycle = root.Scalar("lifecycle", "manifest");
                if (
                    lifecycle is not ("placeholder" or "candidate")
                    || assetId is not null && !string.Equals(id, assetId, StringComparison.Ordinal)
                    || !string.Equals(
                        root.Mapping("output", "manifest").Scalar("path", "manifest.output"),
                        assetPath,
                        StringComparison.Ordinal
                    )
                )
                    throw new AssetCtlException(
                        "Publication recovery requires mutable lifecycle and semantic pair ownership.",
                        7
                    );
                return id;
            }

            public void ValidateAuthority(PublicationJournal journal)
            {
                if (
                    journal.AuthorityVersion != 1
                    || journal.TransactionIdentity != TransactionDirectory.ObjectIdentity
                    || journal.AssetParentIdentity != AssetParent.ObjectIdentity
                    || journal.ManifestParentIdentity != ManifestParent.ObjectIdentity
                    || !JournalPathsMatch(journal)
                    || journal.ManifestPredecessor is null
                    || journal.AssetCandidate is null
                    || journal.ManifestCandidate is null
                    || journal.AssetExisted != (journal.AssetPredecessor is not null)
                    || !journal.ManifestExisted
                    || !string.Equals(journal.AssetCandidate.Hash, journal.AssetHash, StringComparison.Ordinal)
                    || journal.AssetCandidate.Length != journal.AssetLength
                    || !string.Equals(journal.ManifestCandidate.Hash, journal.ManifestHash, StringComparison.Ordinal)
                )
                    throw Unsafe();
                FileSnapshot envelope =
                    ReadEvidence(TransactionDirectory, "authority.json", MaximumJournalBytes) ?? throw Unsafe();
                if (
                    !envelope
                        .Bytes.AsSpan()
                        .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(journal, JournalJsonOptions))
                    || _authorityEvidence is not null && envelope.Evidence != _authorityEvidence
                )
                    throw Unsafe();
                _authorityEvidence ??= envelope.Evidence;
                ValidateFiles(journal);
            }

            private bool JournalPathsMatch(PublicationJournal journal)
            {
                string relativeTransaction = Path.GetRelativePath(
                    _configuration.RepositoryRoot,
                    TransactionDirectory.Path
                );
                return string.Equals(
                        journal.AssetPath,
                        Path.GetRelativePath(_configuration.RepositoryRoot, Path.Combine(AssetParent.Path, AssetLeaf)),
                        StringComparison.Ordinal
                    )
                    && string.Equals(
                        journal.ManifestPath,
                        Path.GetRelativePath(
                            _configuration.RepositoryRoot,
                            Path.Combine(ManifestParent.Path, ManifestLeaf)
                        ),
                        StringComparison.Ordinal
                    )
                    && string.Equals(
                        journal.AssetStagePath,
                        Path.Combine(relativeTransaction, "asset.stage"),
                        StringComparison.Ordinal
                    )
                    && string.Equals(
                        journal.ManifestStagePath,
                        Path.Combine(relativeTransaction, "manifest.stage"),
                        StringComparison.Ordinal
                    )
                    && string.Equals(
                        journal.AssetBackupPath,
                        journal.AssetPath + ".assetctl-backup-" + journal.TransactionId,
                        StringComparison.Ordinal
                    )
                    && string.Equals(
                        journal.ManifestBackupPath,
                        journal.ManifestPath + ".assetctl-backup-" + journal.TransactionId,
                        StringComparison.Ordinal
                    );
            }

            private bool ValidateFiles(PublicationJournal journal)
            {
                FileSnapshot? asset = ReadEvidence(AssetParent, AssetLeaf, _configuration.Limits.MaximumDownloadBytes);
                FileSnapshot? manifest = ReadEvidence(ManifestParent, ManifestLeaf, YamlValues.MaximumBytes);
                FileSnapshot? assetBackup = ReadEvidence(
                    AssetParent,
                    AssetBackupLeaf(journal),
                    _configuration.Limits.MaximumDownloadBytes
                );
                FileSnapshot? manifestBackup = ReadEvidence(
                    ManifestParent,
                    ManifestBackupLeaf(journal),
                    YamlValues.MaximumBytes
                );
                FileSnapshot? assetStage = ReadEvidence(
                    TransactionDirectory,
                    "asset.stage",
                    _configuration.Limits.MaximumDownloadBytes
                );
                FileSnapshot? manifestStage = ReadEvidence(
                    TransactionDirectory,
                    "manifest.stage",
                    YamlValues.MaximumBytes
                );
                Admit(asset, journal.AssetPredecessor, journal.AssetCandidate);
                Admit(manifest, journal.ManifestPredecessor, journal.ManifestCandidate);
                Admit(assetBackup, journal.AssetPredecessor);
                Admit(manifestBackup, journal.ManifestPredecessor);
                Admit(assetStage, journal.AssetCandidate);
                Admit(manifestStage, journal.ManifestCandidate);
                bool complete =
                    asset?.Evidence == journal.AssetCandidate && manifest?.Evidence == journal.ManifestCandidate;
                if (
                    !complete
                    && journal.AssetPredecessor is not null
                    && asset?.Evidence != journal.AssetPredecessor
                    && assetBackup?.Evidence != journal.AssetPredecessor
                )
                    throw Unsafe();
                if (
                    !complete
                    && manifest?.Evidence != journal.ManifestPredecessor
                    && manifestBackup?.Evidence != journal.ManifestPredecessor
                )
                    throw Unsafe();
                // Validate every available manifest snapshot independently. A correct hash in a forged
                // journal cannot authorize an approved/deprecated or differently owned live/backup pair.
                foreach (FileSnapshot? snapshot in new[] { manifest, manifestBackup, manifestStage })
                    if (snapshot is not null)
                        ValidateManifest(snapshot, journal.AssetId, journal.AssetPath);
                return complete;
            }

            public void Move(
                StateFile.DirectoryHandle source,
                string sourceLeaf,
                StateFile.DirectoryHandle target,
                string targetLeaf,
                FileEvidence evidence,
                PublicationMove move,
                PublicationTestHooks? hooks,
                Action? committed = null
            )
            {
                hooks?.BeforeMove?.Invoke(move);
                AssetParent.EnsureStillNamed("publication asset parent");
                ManifestParent.EnsureStillNamed("publication manifest parent");
                TransactionDirectory.EnsureStillNamed("publication transaction parent");
                _journalParent.EnsureStillNamed("publication journal parent");
                Require(source, sourceLeaf, evidence);
                if (ReadEvidence(target, targetLeaf, Limit(target, targetLeaf)) is not null)
                    throw Unsafe();
                source.MoveTo(sourceLeaf, target, targetLeaf, "publication move");
                committed?.Invoke();
                hooks?.AfterMove?.Invoke(move);
            }

            public void Recover(PublicationJournal journal)
            {
                ValidateAuthority(journal);
                bool complete = ValidateFiles(journal);
                if (!complete)
                {
                    Restore(
                        AssetParent,
                        AssetLeaf,
                        AssetBackupLeaf(journal),
                        journal.AssetPredecessor,
                        journal.AssetCandidate!
                    );
                    Restore(
                        ManifestParent,
                        ManifestLeaf,
                        ManifestBackupLeaf(journal),
                        journal.ManifestPredecessor,
                        journal.ManifestCandidate!
                    );
                }
                DeleteOwned(TransactionDirectory, "asset.stage", journal.AssetCandidate);
                DeleteOwned(TransactionDirectory, "manifest.stage", journal.ManifestCandidate);
                DeleteOwned(AssetParent, AssetBackupLeaf(journal), journal.AssetPredecessor);
                DeleteOwned(ManifestParent, ManifestBackupLeaf(journal), journal.ManifestPredecessor);
                // Remove the locator last. If cleanup stops midway, remaining owned evidence permits
                // another bounded recovery; an incomplete envelope will be quarantined without mutation.
                DeleteOwned(TransactionDirectory, "authority.json", _authorityEvidence);
                if (_journalLeaf is not null)
                    DeleteOwned(_journalParent, _journalLeaf, _journalEvidence);
            }

            private void Restore(
                StateFile.DirectoryHandle parent,
                string liveLeaf,
                string backupLeaf,
                FileEvidence? predecessor,
                FileEvidence candidate
            )
            {
                FileSnapshot? live = ReadEvidence(parent, liveLeaf, Limit(parent, liveLeaf));
                FileSnapshot? backup = ReadEvidence(parent, backupLeaf, Limit(parent, backupLeaf));
                if (predecessor is null)
                {
                    if (live is not null)
                        DeleteOwned(parent, liveLeaf, candidate);
                    return;
                }
                if (backup is not null)
                {
                    Require(parent, backupLeaf, predecessor);
                    if (live is not null)
                        DeleteOwned(parent, liveLeaf, candidate);
                    parent.MoveTo(backupLeaf, parent, liveLeaf, "publication predecessor restore");
                }
                else if (live?.Evidence != predecessor)
                    throw Unsafe();
            }

            public void DeleteLease() => DeleteOwned(TransactionDirectory, LeaseFileName, _leaseEvidence);

            private long Limit(StateFile.DirectoryHandle parent, string leaf) =>
                ReferenceEquals(parent, AssetParent)
                || ReferenceEquals(parent, TransactionDirectory)
                    && string.Equals(leaf, "asset.stage", StringComparison.Ordinal)
                    ? _configuration.Limits.MaximumDownloadBytes
                : ReferenceEquals(parent, _journalParent) || leaf is "authority.json" or LeaseFileName
                    ? MaximumJournalBytes
                : YamlValues.MaximumBytes;

            private void Require(StateFile.DirectoryHandle parent, string leaf, FileEvidence expected)
            {
                if (ReadEvidence(parent, leaf, Limit(parent, leaf))?.Evidence != expected)
                    throw Unsafe();
            }

            private void DeleteOwned(StateFile.DirectoryHandle parent, string leaf, FileEvidence? expected)
            {
                FileSnapshot? snapshot = ReadEvidence(parent, leaf, Limit(parent, leaf));
                if (snapshot is null)
                    return;
                if (expected is null || snapshot.Evidence != expected)
                    throw Unsafe();
                parent.DeleteFile(leaf, "publication owned cleanup");
            }

            private static void Admit(FileSnapshot? actual, FileEvidence? first, FileEvidence? second = null)
            {
                if (actual is not null && actual.Evidence != first && actual.Evidence != second)
                    throw Unsafe();
            }

            private static FileSnapshot? ReadEvidence(StateFile.DirectoryHandle parent, string leaf, long limit)
            {
                try
                {
                    _ = File.GetAttributes(parent.DescriptorPath(leaf));
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
                using FileStream stream = parent.OpenReadFile(leaf, "publication evidence");
                StateFile.FileIdentity identity = StateFile.Identity(stream);
                byte[] bytes = global::AlterCourse.AssetCtl.Catalog.SelectedAssetReader.Read(stream, limit);
                return new FileSnapshot(new FileEvidence(identity, Hash(bytes), bytes.LongLength), bytes);
            }

            private static AssetCtlException Unsafe() =>
                new("Publication transaction ownership evidence is unavailable or changed.", 7);

            public void Dispose()
            {
                _journalParent.Dispose();
                ManifestParent.Dispose();
                AssetParent.Dispose();
            }

            private sealed record FileSnapshot(FileEvidence Evidence, byte[] Bytes);
        }

        private sealed class PreparedPublication : IDisposable
        {
            private readonly StagedPublication _staged;

            public PreparedPublication(
                string journalPath,
                PublicationJournal journal,
                StagedPublication staged,
                PublicationBoundary boundary
            )
            {
                Journal = journal;
                _staged = staged;
                Boundary = boundary;
                boundary.SetJournal(journalPath, journal);
            }

            public PublicationJournal Journal { get; }
            public PublicationBoundary Boundary { get; }
            public bool Committed { get; set; }

            public void Dispose()
            {
                // Disposal is auxiliary after commitment, and must preserve any primary publication error.
                try
                {
                    Boundary.DeleteLease();
                }
                catch (Exception) { }
                try
                {
                    _staged.Lease.Dispose();
                }
                catch (Exception) { }
                Boundary.Dispose();
                _staged.TransactionDirectory.Dispose();
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
            FileStream Lease,
            StateFile.DirectoryHandle TransactionDirectory,
            StateFile.FileIdentity AssetIdentity,
            StateFile.FileIdentity ManifestIdentity,
            StateFile.FileIdentity LeaseIdentity
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
