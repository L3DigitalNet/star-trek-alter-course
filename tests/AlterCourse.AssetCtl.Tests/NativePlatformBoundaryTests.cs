using System.Runtime.InteropServices;
using AlterCourse.AssetCtl.Publishing;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>
/// Exercises native ABI admission through the production guard with a modeled OS and process
/// architecture, so unsupported platforms are proven refused without invoking libc on them.
/// </summary>
public sealed class NativePlatformBoundaryTests
{
    /// <summary>
    /// Every platform other than Linux x64 refuses with the stable state-access exit code. Linux
    /// Arm64 and Arm are the cases that matter: their fcntl.h assigns 0x10000 and 0x20000 to
    /// O_DIRECT and O_LARGEFILE, so the x64 open-flag constants would silently drop the directory
    /// and no-follow protections there instead of failing.
    /// </summary>
    [Theory]
    [InlineData(false, Architecture.X64)]
    [InlineData(false, Architecture.Arm64)]
    [InlineData(true, Architecture.X86)]
    [InlineData(true, Architecture.Arm)]
    [InlineData(true, Architecture.Arm64)]
    [InlineData(true, (Architecture)int.MaxValue)]
    public void UnsupportedPlatformFailsClosed(bool isLinux, Architecture processArchitecture)
    {
        AssetCtlException failure = Assert.Throws<AssetCtlException>(() =>
            PublishingTypes.StateFile.RequireSupportedPlatform("state lock", isLinux, processArchitecture)
        );

        Assert.Equal(7, failure.ExitCode);
        Assert.StartsWith("state lock:", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Linux x64, the only validated native ABI, is admitted by the same guard.</summary>
    [Fact]
    public void LinuxX64IsAdmitted()
    {
        PublishingTypes.StateFile.RequireSupportedPlatform("state lock", true, Architecture.X64);
    }
}
