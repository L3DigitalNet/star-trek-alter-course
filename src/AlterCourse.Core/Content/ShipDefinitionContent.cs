using System.Text;

namespace AlterCourse.Core.Content;

/// <summary>Pairs one UTF-8 authored ship document with its stable diagnostic identity.</summary>
public sealed class ShipDefinitionContent
{
    private readonly byte[] _utf8Json;

    private ShipDefinitionContent(string sourceIdentity, byte[] utf8Json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdentity);
        SourceIdentity = sourceIdentity;
        _utf8Json = utf8Json;
    }

    /// <summary>Gets the source identity used in deterministic diagnostics.</summary>
    public string SourceIdentity { get; }

    internal ReadOnlyMemory<byte> Utf8Json => _utf8Json;

    /// <summary>Creates content from JSON text encoded as UTF-8.</summary>
    public static ShipDefinitionContent FromText(string sourceIdentity, string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return new ShipDefinitionContent(sourceIdentity, Encoding.UTF8.GetBytes(json));
    }

    /// <summary>Creates content from UTF-8 JSON bytes, isolated from later caller mutation.</summary>
    public static ShipDefinitionContent FromUtf8(string sourceIdentity, ReadOnlySpan<byte> utf8Json) =>
        new(sourceIdentity, utf8Json.ToArray());

    /// <summary>Reads one UTF-8 JSON document from the stream's current position.</summary>
    public static ShipDefinitionContent FromStream(string sourceIdentity, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return new ShipDefinitionContent(sourceIdentity, buffer.ToArray());
    }
}
