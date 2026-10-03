using System.Text;
using AlterCourse.AssetCtl.Configuration;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies the YAML file loader admits one bounded byte snapshot before decoding.</summary>
public sealed class YamlBoundaryRegressionTests
{
    /// <summary>A stale initial length must not permit the decoder to consume arbitrary growth.</summary>
    [Fact]
    public void GrowingYamlIsRejectedWithinInitialSnapshotBound()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("value: '" + new string('x', 128) + "'\n");
        using var stream = new GrowingStream(bytes, 8);

        AssetCtlException failure = Assert.Throws<AssetCtlException>(() =>
            YamlValues.LoadMapping("YAML input", stream, 16)
        );

        Assert.Equal(2, failure.ExitCode);
        Assert.True(stream.Consumed <= 9);
    }

    /// <summary>The bounded path preserves the existing BOM-selected UTF-16 decoding contract.</summary>
    [Fact]
    public void YamlSnapshotPreservesBomEncoding()
    {
        byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("value: valid\n")];
        using var stream = new MemoryStream(bytes);

        global::YamlDotNet.RepresentationModel.YamlMappingNode mapping = YamlValues.LoadMapping(
            "YAML input",
            stream,
            bytes.Length
        );

        Assert.Equal("valid", mapping.Scalar("value", "root"));
    }

    private sealed class GrowingStream(byte[] bytes, long reportedLength) : MemoryStream(bytes)
    {
        public override long Length => reportedLength;
        public long Consumed => Position;
    }
}
