using AlterCourse.AssetCtl.Configuration;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies hostile YAML and repository paths fail closed at configuration boundaries.</summary>
public sealed class ConfigurationSafetyTests
{
    /// <summary>Rejects YAML features that can execute constructors or expand recursive graphs.</summary>
    [Theory]
    [InlineData("schema_version: '1'\na: &anchor value\nb: *anchor\n")]
    [InlineData("schema_version: '1'\na: !custom value\n")]
    public void StrictYamlRejectsExecutableOrRecursiveFeatures(string yaml)
    {
        string path = TemporaryFile(yaml);
        try
        {
            AssetCtlException exception = Assert.Throws<AssetCtlException>(() => StrictYaml.LoadMapping(path));
            Assert.Contains("prohibited", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Rejects duplicate YAML keys instead of silently choosing a value.</summary>
    [Fact]
    public void StrictYamlRejectsDuplicateKeys()
    {
        string path = TemporaryFile("schema_version: '1'\nschema_version: '1'\n");
        try
        {
            Assert.Throws<AssetCtlException>(() => StrictYaml.LoadMapping(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Rejects absolute paths and parent traversal outside the repository.</summary>
    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute")]
    public void RepositoryPathRejectsTraversalAndAbsolutePaths(string value)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<AssetCtlException>(() => PathPolicy.ResolveUnder(root, value, "test", true));
        }
        finally
        {
            Directory.Delete(root);
        }
    }

    /// <summary>Rejects an existing symlink component that redirects a configured-root path outside the repository.</summary>
    [Fact]
    public void ConfiguredRootRejectsSymlinkEscape()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "catalog"));
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Path.Combine(root, "catalog", "escaped"), outside);
        try
        {
            Assert.Throws<AssetCtlException>(() =>
                PathPolicy.ResolveUnderConfiguredRoot(
                    root,
                    "catalog",
                    "catalog/escaped/manifest.asset.yaml",
                    "manifest path",
                    allowMissing: true
                )
            );
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>Rejects a repository-confined path that is outside the configured catalog root.</summary>
    [Fact]
    public void ConfiguredRootRejectsSiblingPath()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "catalog"));
        try
        {
            Assert.Throws<AssetCtlException>(() =>
                PathPolicy.ResolveUnderConfiguredRoot(
                    root,
                    "catalog",
                    "other/manifest.asset.yaml",
                    "manifest path",
                    allowMissing: true
                )
            );
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Reports unknown configuration keys with their precise logical path.</summary>
    [Fact]
    public void UnknownKeysArePathSpecific()
    {
        string path = TemporaryFile("known: 'yes'\nextra: 'no'\n");
        try
        {
            global::YamlDotNet.RepresentationModel.YamlMappingNode root = StrictYaml.LoadMapping(path);
            AssetCtlException exception = Assert.Throws<AssetCtlException>(() => root.RequireOnly("root", "known"));
            Assert.Contains("root.extra", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TemporaryFile(string contents)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, contents);
        return path;
    }
}
