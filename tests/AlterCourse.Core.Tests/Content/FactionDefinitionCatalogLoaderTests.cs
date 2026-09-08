using System.Text;
using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;

namespace AlterCourse.Core.Tests.Content;

/// <summary>Verifies strict, versioned admission of authored faction definitions.</summary>
public sealed class FactionDefinitionCatalogLoaderTests
{
    private const string ValidDefinition = """
        {
          "schemaVersion": 1,
          "id": "faction-a",
          "displayName": "Faction A"
        }
        """;

    /// <summary>Confirms text, UTF-8 bytes, and streams map to the immutable domain definition.</summary>
    [Fact]
    public void LoadsValidDefinitionFromSupportedInputs()
    {
        FactionDefinitionCatalogLoader loader = CreateLoader();

        FactionDefinition fromText = loader.LoadText(ValidDefinition, "text.json");
        FactionDefinition fromBytes = loader.LoadUtf8(Encoding.UTF8.GetBytes(ValidDefinition), "bytes.json");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ValidDefinition));
        FactionDefinition fromStream = loader.Load(stream, "stream.json");

        Assert.Equal(fromText, fromBytes);
        Assert.Equal(fromText, fromStream);
        Assert.Equal(new FactionDefinitionId("faction-a"), fromText.Id);
        Assert.Equal("Faction A", fromText.DisplayName);
    }

    /// <summary>Confirms the canonical schema and both production definitions remain load-compatible.</summary>
    [Fact]
    public void LoadsCanonicalProductionDefinitionsInStableOrder()
    {
        string root = FindRepositoryRoot();
        FactionDefinitionCatalogLoader loader = CreateLoader();
        FactionDefinitionCatalog catalog = loader.LoadCatalog(
            Directory
                .EnumerateFiles(Path.Combine(root, "src/AlterCourse.Godot/content/factions"), "*.json")
                .Reverse()
                .Select(path => FactionDefinitionContent.FromText(path, File.ReadAllText(path)))
        );

        Assert.Equal(
            ["faction-a", "faction-b"],
            catalog.Definitions.Select(definition => definition.Id.Value),
            StringComparer.Ordinal
        );
        Assert.Equal("Faction A", catalog.GetRequired(new FactionDefinitionId("faction-a")).DisplayName);
        Assert.Equal("Faction B", catalog.GetRequired(new FactionDefinitionId("faction-b")).DisplayName);
    }

    /// <summary>Confirms zero-definition catalogs are valid and missing references fail explicitly.</summary>
    [Fact]
    public void EmptyCatalogRejectsMissingRequiredDefinition()
    {
        Assert.Empty(FactionDefinitionCatalog.Empty.Definitions);

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() =>
            FactionDefinitionCatalog.Empty.GetRequired(new FactionDefinitionId("missing"))
        );

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms domain construction and content admission share their exact text boundaries.</summary>
    [Fact]
    public void EnforcesIdentityAndDisplayNameBoundsAcrossDomainAndContent()
    {
        string maximumId = new('i', FactionDefinitionId.MaximumLength);
        string maximumName = new('n', FactionDefinition.MaximumDisplayNameLength);
        FactionDefinition maximum = CreateLoader().LoadText(Definition(maximumId, maximumName), "maximum.json");

        Assert.Equal(maximumId, maximum.Id.Value);
        Assert.Equal(maximumName, maximum.DisplayName);
        Assert.Throws<ArgumentException>(() =>
            new FactionDefinitionId(new string('i', FactionDefinitionId.MaximumLength + 1))
        );
        Assert.Throws<ArgumentException>(() =>
            new FactionDefinition(
                new FactionDefinitionId("oversized"),
                new string('n', FactionDefinition.MaximumDisplayNameLength + 1)
            )
        );
        Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader()
                .LoadText(
                    Definition(new string('i', FactionDefinitionId.MaximumLength + 1), "Faction"),
                    "oversized-id.json"
                )
        );
        Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader()
                .LoadText(
                    Definition("faction", new string('n', FactionDefinition.MaximumDisplayNameLength + 1)),
                    "oversized-name.json"
                )
        );
    }

    /// <summary>Confirms the canonical schema rejects identities outside the durable ASCII alphabet.</summary>
    [Theory]
    [InlineData("invalid id")]
    [InlineData("invalid/id")]
    [InlineData("invalid:id")]
    [InlineData("invalidéid")]
    [InlineData("invalid\\u0001id")]
    public void RejectsIdentityOutsideDurableAlphabet(string identity)
    {
        Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader().LoadText(Definition(identity, "Faction"), "invalid-id.json")
        );
    }

    /// <summary>Confirms malformed and invalid UTF-8 JSON fail closed with source-aware diagnostics.</summary>
    [Fact]
    public void RejectsMalformedAndInvalidUtf8Json()
    {
        FactionContentValidationException malformed = Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader().LoadText("{\"schemaVersion\":1", "malformed.json")
        );
        byte[] invalidUtf8 = [0x7b, 0x22, 0x69, 0x64, 0x22, 0x3a, 0x22, 0xff, 0x22, 0x7d];
        FactionContentValidationException invalid = Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader().LoadUtf8(invalidUtf8, "invalid-utf8.json")
        );

        Assert.Equal("json.invalid", malformed.Diagnostics.Single().Code);
        Assert.Equal("malformed.json", malformed.Diagnostics.Single().SourceIdentity);
        Assert.Equal("json.invalid", invalid.Diagnostics.Single().Code);
        Assert.Equal("invalid-utf8.json", invalid.Diagnostics.Single().SourceIdentity);
    }

    /// <summary>Confirms duplicate members are rejected before structural schema evaluation.</summary>
    [Fact]
    public void RejectsDuplicateObjectMembers()
    {
        string duplicate = ValidDefinition.Replace(
            "\"displayName\": \"Faction A\"",
            "\"displayName\": \"Faction A\",\n  \"displayName\": \"Duplicate\"",
            StringComparison.Ordinal
        );

        FactionContentValidationException exception = Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader().LoadText(duplicate, "duplicate.json")
        );

        Assert.Equal("json.duplicate-member", exception.Diagnostics.Single().Code);
        Assert.Contains("displayName", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms unknown members and unsupported schema versions cannot enter the authored contract.</summary>
    [Theory]
    [InlineData("\"schemaVersion\": 1,", "\"schemaVersion\": 1,\n  \"objective\": \"expand\",")]
    [InlineData("\"schemaVersion\": 1,", "")]
    [InlineData("\"schemaVersion\": 1", "\"schemaVersion\": 2")]
    public void RejectsUnknownMembersAndWrongOrMissingSchemaVersion(string original, string replacement)
    {
        string json = ValidDefinition.Replace(original, replacement, StringComparison.Ordinal);

        FactionContentValidationException exception = Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader().LoadText(json, "schema.json")
        );

        Assert.Contains("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirms whitespace-only semantic values fail with stable typed locations.</summary>
    [Theory]
    [InlineData("\"id\": \"faction-a\"", "\"id\": \"   \"", "#/id", "schema")]
    [InlineData("\"displayName\": \"Faction A\"", "\"displayName\": \"   \"", "#/displayName", "semantic")]
    public void RejectsWhitespaceOnlyValues(
        string original,
        string replacement,
        string expectedLocation,
        string expectedStage
    )
    {
        string json = ValidDefinition.Replace(original, replacement, StringComparison.Ordinal);

        FactionContentValidationException exception = Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader().LoadText(json, "semantic.json")
        );

        Assert.Contains(
            exception.Diagnostics,
            diagnostic => string.Equals(diagnostic.InstanceLocation, expectedLocation, StringComparison.Ordinal)
        );
        Assert.All(
            exception.Diagnostics,
            diagnostic => Assert.Contains(expectedStage, diagnostic.Code, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>Confirms catalog registration rejects stable identities repeated across source documents.</summary>
    [Fact]
    public void RejectsDuplicateCatalogIdentifiers()
    {
        FactionContentValidationException exception = Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader()
                .LoadCatalog([
                    FactionDefinitionContent.FromText("first.json", ValidDefinition),
                    FactionDefinitionContent.FromText("second.json", ValidDefinition),
                ])
        );

        Assert.Equal("catalog.duplicate-id", exception.Diagnostics.Single().Code);
        Assert.Contains("first.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("second.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms catalog materialization is bounded without reading past the rejection threshold.</summary>
    [Fact]
    public void BoundsCatalogDefinitionMaterialization()
    {
        FactionDefinitionContent[] maximum =
        [
            .. Enumerable
                .Range(0, FactionDefinitionCatalogLoader.MaximumDefinitions)
                .Select(index =>
                    FactionDefinitionContent.FromText(
                        $"faction-{index}.json",
                        Definition($"faction-{index}", $"Faction {index}")
                    )
                ),
        ];

        FactionDefinitionCatalog catalog = CreateLoader().LoadCatalog(maximum);

        Assert.Equal(FactionDefinitionCatalogLoader.MaximumDefinitions, catalog.Definitions.Count);
        Assert.Throws<ArgumentException>(() =>
            CreateLoader()
                .LoadCatalog(
                    OverflowAfter(
                        FactionDefinitionContent.FromText("overflow.json", ValidDefinition),
                        FactionDefinitionCatalogLoader.MaximumDefinitions + 1
                    )
                )
        );
    }

    /// <summary>Confirms diagnostics are deterministic, source-aware, and carry instance and schema locations.</summary>
    [Fact]
    public void ProducesDeterministicUsefulDiagnostics()
    {
        string json = ValidDefinition.Replace(
            ",\n  \"displayName\": \"Faction A\"",
            string.Empty,
            StringComparison.Ordinal
        );
        FactionDefinitionCatalogLoader loader = CreateLoader();

        FactionContentValidationException first = Assert.Throws<FactionContentValidationException>(() =>
            loader.LoadText(json, "diagnostic.json")
        );
        FactionContentValidationException second = Assert.Throws<FactionContentValidationException>(() =>
            loader.LoadText(json, "diagnostic.json")
        );

        Assert.Equal(first.Message, second.Message);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.All(first.Diagnostics, diagnostic => Assert.Equal("diagnostic.json", diagnostic.SourceIdentity));
        Assert.Contains(first.Diagnostics, diagnostic => !string.IsNullOrWhiteSpace(diagnostic.SchemaLocation));
    }

    /// <summary>Confirms every authored-input path rejects oversized documents before parsing.</summary>
    [Fact]
    public void RejectsOversizedDocumentsAcrossInputForms()
    {
        string oversized = new(' ', (256 * 1024) + 1);
        byte[] bytes = Encoding.UTF8.GetBytes(oversized);

        FactionContentValidationException text = Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader().LoadText(oversized, "large-text.json")
        );
        FactionContentValidationException utf8 = Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader().LoadUtf8(bytes, "large-bytes.json")
        );
        using var stream = new MemoryStream(bytes);
        FactionContentValidationException streamed = Assert.Throws<FactionContentValidationException>(() =>
            CreateLoader().Load(stream, "large-stream.json")
        );

        Assert.Equal("content.size-limit", text.Diagnostics.Single().Code);
        Assert.Equal("content.size-limit", utf8.Diagnostics.Single().Code);
        Assert.Equal("content.size-limit", streamed.Diagnostics.Single().Code);
    }

    private static FactionDefinitionCatalogLoader CreateLoader() =>
        new(
            File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    "src/AlterCourse.Godot/content/schemas/faction-definition-v1.schema.json"
                )
            )
        );

    private static string Definition(string id, string displayName) =>
        ValidDefinition
            .Replace("\"id\": \"faction-a\"", $"\"id\": \"{id}\"", StringComparison.Ordinal)
            .Replace("\"displayName\": \"Faction A\"", $"\"displayName\": \"{displayName}\"", StringComparison.Ordinal);

    private static IEnumerable<T> OverflowAfter<T>(T value, int yieldedCount)
    {
        for (int index = 0; index < yieldedCount; index++)
        {
            yield return value;
        }

        throw new InvalidOperationException("The bounded consumer enumerated past its rejection threshold.");
    }

    private static string FindRepositoryRoot()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, "AlterCourse.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
