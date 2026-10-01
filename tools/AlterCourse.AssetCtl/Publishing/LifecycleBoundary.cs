using AlterCourse.AssetCtl.Catalog;
using AlterCourse.AssetCtl.Configuration;
using StateFile = AlterCourse.AssetCtl.Publishing.PublishingTypes.StateFile;

namespace AlterCourse.AssetCtl.Publishing;

/// <summary>
/// Holds admitted Linux parents and regular-file evidence through manifest commitment.
/// Requires openat/statx and mounted procfs to admit identities and detect renamed parents.
/// Cooperating writers must hold AssetLock; descriptor identity checks do not implement an atomic
/// revision CAS against arbitrary same-UID writers or guarantee power-loss durability.
/// </summary>
internal sealed class LifecycleBoundary : IDisposable
{
    private readonly EffectiveConfiguration _configuration;
    private readonly AssetManifest _expected;
    private readonly BoundFile _manifest;
    private BoundFile? _selected;
    private byte[]? _selectedBytes;

    public LifecycleBoundary(EffectiveConfiguration configuration, AssetManifest expected)
    {
        _configuration = configuration;
        _expected = expected;
        string path = PathPolicy.ResolveManifestPath(configuration, expected.ManifestPath, allowMissing: false);
        _manifest = new BoundFile(path);
        try
        {
            EnsureManifest();
        }
        catch
        {
            _manifest.Dispose();
            throw;
        }
    }

    public PublishingTypes.StateFile.DirectoryHandle Parent => _manifest.Parent;
    public string ManifestLeaf => _manifest.Leaf;
    public string ManifestPath => _manifest.Path;

    public byte[] ReadSelected()
    {
        _selected = new BoundFile(
            PathPolicy.ResolveOutputPath(_configuration, _expected.Request.Output.Path, allowMissing: false)
        );
        _selectedBytes = _selected.Read(_configuration.Limits.MaximumDownloadBytes);
        ManifestStore.VerifyIntegrity(_configuration, _expected, _selectedBytes);
        return _selectedBytes;
    }

    public void EnsureCurrent()
    {
        EnsureManifest();
        if (_selected is not null)
        {
            _selected.EnsureNamed();
            byte[] current = _selected.Read(_configuration.Limits.MaximumDownloadBytes);
            if (!current.AsSpan().SequenceEqual(_selectedBytes))
            {
                throw new AssetCtlException("Selected bytes changed after lifecycle validation.", 7);
            }
        }
    }

    private void EnsureManifest()
    {
        _manifest.EnsureNamed();
        byte[] bytes = _manifest.Read(YamlValues.MaximumBytes);
        AssetManifest current = ManifestStore.LoadSnapshot(
            _configuration,
            _manifest.Path,
            YamlValues.LoadBytes(_manifest.Path, bytes)
        );
        ManifestMutation.EnsureSameVersion(_expected, current);
    }

    public void Dispose()
    {
        _selected?.Dispose();
        _manifest.Dispose();
    }

    private sealed class BoundFile : IDisposable
    {
        private readonly FileStream _stream;
        private readonly StateFile.FileIdentity _identity;

        public BoundFile(string path)
        {
            Path = path;
            Leaf = System.IO.Path.GetFileName(path);
            Parent = StateFile.DirectoryHandle.OpenExisting(System.IO.Path.GetDirectoryName(path)!, "lifecycle parent");
            try
            {
                _stream = Parent.OpenReadFile(Leaf, "lifecycle evidence");
                try
                {
                    _identity = StateFile.Identity(_stream);
                }
                catch
                {
                    _stream.Dispose();
                    throw;
                }
            }
            catch
            {
                Parent.Dispose();
                throw;
            }
        }

        public string Path { get; }
        public string Leaf { get; }
        public StateFile.DirectoryHandle Parent { get; }

        public byte[] Read(long maximumBytes)
        {
            _stream.Position = 0;
            return SelectedAssetReader.Read(_stream, maximumBytes);
        }

        public void EnsureNamed()
        {
            Parent.EnsureStillNamed("lifecycle parent");
            using FileStream named = Parent.OpenReadFile(Leaf, "lifecycle evidence");
            if (StateFile.Identity(named) != _identity)
            {
                throw new AssetCtlException("Lifecycle evidence file was replaced.", 7);
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
            Parent.Dispose();
        }
    }
}
