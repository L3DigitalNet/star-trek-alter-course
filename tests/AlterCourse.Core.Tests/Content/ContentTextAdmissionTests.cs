using System.Text;
using AlterCourse.Core.Content;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Content;

/// <summary>Verifies UTF-16 text admission without changing authored Unicode or the byte envelope.</summary>
public sealed class ContentTextAdmissionTests
{
    private const string Source = "user://authored-content.json";

    /// <summary>Actual UTF-16 code units must fail before replacement encoding can alter the document.</summary>
    [Theory]
    [InlineData(0, 0xD800, false)]
    [InlineData(0, 0xDC00, false)]
    [InlineData(0, 0xD800, true)]
    [InlineData(0, 0xDC00, true)]
    [InlineData(1, 0xD800, false)]
    [InlineData(1, 0xDC00, false)]
    [InlineData(1, 0xD800, true)]
    [InlineData(1, 0xDC00, true)]
    [InlineData(2, 0xD800, false)]
    [InlineData(2, 0xDC00, false)]
    [InlineData(2, 0xD800, true)]
    [InlineData(2, 0xDC00, true)]
    public void EveryFamilyRejectsActualIsolatedSurrogates(int family, int codeUnit, bool memberName)
    {
        // These are actual .NET string code units, unlike the literal JSON escapes in StrictContentUnicodeTests.
        string surrogate = new((char)codeUnit, 1);
        string json = memberName ? "{\"" + surrogate + "\":0}" : "{\"id\":\"" + surrogate + "\"}";
        AssertFailure(family, () => Load(family, json, 0), "json.invalid");
    }

    /// <summary>Non-BMP pairs, multibyte text, and authored replacement characters retain their exact meaning.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ValidUnicodeHasEquivalentTextByteAndStreamSemantics(int family)
    {
        string json = ValidDocument(family);
        object text = Load(family, json, 0);
        Assert.Equivalent(text, Load(family, json, 1), strict: true);
        Assert.Equivalent(text, Load(family, json, 2), strict: true);
    }

    /// <summary>Each family applies its limit to UTF-8 bytes, including multibyte content at the boundary.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MultibyteDocumentsHonorExactByteLimit(int family)
    {
        string json = ValidDocument(family);
        string exact = json + new string(' ', SystemDefinitionContent.MaximumDocumentBytes - Encoding.UTF8.GetByteCount(json));
        Assert.True(exact.Length < SystemDefinitionContent.MaximumDocumentBytes);
        Assert.Equivalent(Load(family, json, 0), Load(family, exact, 0), strict: true);
        Assert.Equivalent(Load(family, exact, 0), Load(family, exact, 1), strict: true);
        Assert.Equivalent(Load(family, exact, 0), Load(family, exact, 2), strict: true);
        for (int form = 0; form < 3; form++)
        {
            int inputForm = form;
            AssertFailure(family, () => Load(family, exact + " ", inputForm), family == 1 ? "content.too-large" : "content.size-limit");
        }
    }

    private static string ValidDocument(int family)
    {
        const string label = "Café 星 \uD83D\uDE80 \uFFFD";
        return family switch
        {
            0 => TestShipContent.ReadRepositoryFile("src/AlterCourse.Godot/content/ships/pathfinder.json")
                .Replace("Pathfinder class", label, StringComparison.Ordinal),
            1 => TestShipContent.ReadRepositoryFile("src/AlterCourse.Godot/content/systems/pathfinder-systems.json")
                .Replace("Power generation", label, StringComparison.Ordinal),
            2 => "{\"schemaVersion\":1,\"id\":\"faction-a\",\"displayName\":\"" + label + "\"}",
            _ => throw new ArgumentOutOfRangeException(nameof(family)),
        };
    }

    private static object Load(int family, string json, int form)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);
        return family switch
        {
            0 => TestShipContent.ShipLoader(TestShipContent.ProductionSystems()).Load(form switch
            {
                0 => ShipDefinitionContent.FromText(Source, json),
                1 => ShipDefinitionContent.FromUtf8(Source, bytes),
                _ => ShipDefinitionContent.FromStream(Source, stream),
            }),
            1 => new SystemDefinitionCatalogLoader(TestShipContent.ReadRepositoryFile("src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json"))
                .LoadCatalog([form switch
                {
                    0 => SystemDefinitionContent.FromText(Source, json),
                    1 => SystemDefinitionContent.FromUtf8(Source, bytes),
                    _ => SystemDefinitionContent.FromStream(Source, stream),
                }]).Definitions,
            2 => new FactionDefinitionCatalogLoader(TestShipContent.ReadRepositoryFile("src/AlterCourse.Godot/content/schemas/faction-definition-v1.schema.json"))
                .Load(form switch
                {
                    0 => FactionDefinitionContent.FromText(Source, json),
                    1 => FactionDefinitionContent.FromUtf8(Source, bytes),
                    _ => FactionDefinitionContent.FromStream(Source, stream),
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(family)),
        };
    }

    private static void AssertFailure(int family, Action action, string code)
    {
        if (family == 2)
        {
            FactionContentDiagnostic diagnostic = Assert.Single(Assert.Throws<FactionContentValidationException>(action).Diagnostics);
            Assert.Equal(code, diagnostic.Code);
            Assert.Equal(Source, diagnostic.SourceIdentity);
            return;
        }

        ShipContentDiagnostic shipDiagnostic = Assert.Single(Assert.Throws<ShipContentValidationException>(action).Diagnostics);
        Assert.Equal(code, shipDiagnostic.Code);
        Assert.Equal(Source, shipDiagnostic.SourceIdentity);
    }
}
