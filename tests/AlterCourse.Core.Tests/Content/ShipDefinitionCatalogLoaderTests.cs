using System.Text;
using AlterCourse.Core.Content;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Tests.Content;

/// <summary>Verifies strict, versioned admission of authored ship definitions.</summary>
public sealed class ShipDefinitionCatalogLoaderTests
{
    private const string ValidDefinition = """
        {
          "schemaVersion": 3,
          "id": "pathfinder",
          "designDisplayName": "Pathfinder class",
          "maximumTacticalSpeedKilometersPerSecond": 10,
          "passiveSensorRangeKilometers": 30.0,
          "activeScanDurationMilliseconds": 2000,
          "sensorRepairDurationMilliseconds": 8000
        }
        """;

    /// <summary>Confirms text, UTF-8 bytes, and streams map to the existing domain definition.</summary>
    [Fact]
    public void LoadsValidDefinitionFromSupportedInputs()
    {
        ShipDefinitionCatalogLoader loader = CreateLoader();

        ShipDefinition fromText = loader.LoadText(ValidDefinition, "text.json");
        ShipDefinition fromBytes = loader.LoadUtf8(Encoding.UTF8.GetBytes(ValidDefinition), "bytes.json");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ValidDefinition));
        ShipDefinition fromStream = loader.Load(stream, "stream.json");

        Assert.Equal(fromText, fromBytes);
        Assert.Equal(fromText, fromStream);
        Assert.Equal(new ShipDefinitionId("pathfinder"), fromText.Id);
        Assert.Equal("Pathfinder class", fromText.DesignDisplayName);
        Assert.Equal(10, fromText.MaximumTacticalSpeed.Value);
        Assert.Equal(30, fromText.PassiveSensorRange.Value);
        Assert.Equal(2000, fromText.ActiveScanDuration.Milliseconds);
        Assert.Equal(8000, fromText.SensorRepairDuration.Milliseconds);
    }

    /// <summary>Confirms schema-valid integral numeric forms map to the authored integer contract.</summary>
    [Fact]
    public void LoadsSchemaValidIntegralNumericForms()
    {
        string json = ValidDefinition
            .Replace("\"schemaVersion\": 3", "\"schemaVersion\": 3.0", StringComparison.Ordinal)
            .Replace(
                "\"activeScanDurationMilliseconds\": 2000",
                "\"activeScanDurationMilliseconds\": 2e3",
                StringComparison.Ordinal
            );

        ShipDefinition definition = CreateLoader().LoadText(json, "integral-forms.json");

        Assert.Equal(2000, definition.ActiveScanDuration.Milliseconds);
    }

    /// <summary>Confirms an integral JSON number outside the runtime range fails with a typed diagnostic.</summary>
    [Theory]
    [InlineData("1e100")]
    [InlineData("10000000000000000000")]
    public void RejectsUnmappableIntegralNumberWithSourceAwareDiagnostic(string invalidDuration)
    {
        string json = ValidDefinition.Replace(
            "\"activeScanDurationMilliseconds\": 2000",
            $"\"activeScanDurationMilliseconds\": {invalidDuration}",
            StringComparison.Ordinal
        );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "integral-range.json")
        );

        Assert.Contains("semantic", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("integral-range.json", exception.Diagnostics[0].SourceIdentity);
        Assert.Equal("#/activeScanDurationMilliseconds", exception.Diagnostics[0].InstanceLocation);
    }

    /// <summary>Confirms the repository's canonical schema and ship definition remain load-compatible.</summary>
    [Fact]
    public void LoadsCanonicalPlayerShipDefinition()
    {
        string root = FindRepositoryRoot();
        string schema = File.ReadAllText(
            Path.Combine(root, "src/AlterCourse.Godot/content/schemas/ship-definition-v3.schema.json")
        );
        string definition = File.ReadAllText(Path.Combine(root, "src/AlterCourse.Godot/content/ships/pathfinder.json"));

        ShipDefinition ship = new ShipDefinitionCatalogLoader(schema).LoadText(
            definition,
            "res://content/ships/pathfinder.json"
        );

        Assert.Equal(new ShipDefinitionId("pathfinder"), ship.Id);
        Assert.Equal("Pathfinder class", ship.DesignDisplayName);
    }

    /// <summary>Confirms the domain and canonical schema share the exact persisted identity boundary.</summary>
    [Fact]
    public void EnforcesShipDefinitionIdentityLengthAcrossDomainAndContent()
    {
        string maximumId = new('i', ShipDefinitionId.MaximumLength);
        string maximum = DefinitionWithId(maximumId);

        ShipDefinition loaded = CreateLoader().LoadText(maximum, "maximum-id.json");

        Assert.Equal(maximumId, loaded.Id.Value);
        Assert.Throws<ArgumentException>(() =>
            new ShipDefinitionId(new string('i', ShipDefinitionId.MaximumLength + 1))
        );
        Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader()
                .LoadText(DefinitionWithId(new string('i', ShipDefinitionId.MaximumLength + 1)), "oversized-id.json")
        );
    }

    /// <summary>Confirms the canonical schema rejects identities outside the durable ASCII alphabet.</summary>
    [Theory]
    [InlineData("invalid id")]
    [InlineData("invalid/id")]
    [InlineData("invalid:id")]
    [InlineData("invalidéid")]
    [InlineData("invalid\\u0001id")]
    public void RejectsShipDefinitionIdentityOutsideDurableAlphabet(string identity)
    {
        Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(DefinitionWithId(identity), "invalid-id.json")
        );
    }

    /// <summary>Confirms malformed and truncated JSON fail closed with source-aware diagnostics.</summary>
    [Theory]
    [InlineData("{\"schemaVersion\":2")]
    [InlineData("{\"schemaVersion\":2} trailing")]
    public void RejectsMalformedJson(string json)
    {
        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "malformed.json")
        );

        Assert.Contains("malformed.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("JSON", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms duplicate members are rejected before structural schema evaluation.</summary>
    [Fact]
    public void RejectsDuplicateObjectMembers()
    {
        string json = ValidDefinition.Replace(
            "\"designDisplayName\": \"Pathfinder class\",",
            "\"designDisplayName\": \"Pathfinder class\",\n  \"designDisplayName\": \"Duplicate\",",
            StringComparison.Ordinal
        );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "duplicate.json")
        );

        Assert.Contains("duplicate JSON member 'designDisplayName'", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirms schema-unknown members cannot silently enter the authored contract.</summary>
    [Fact]
    public void RejectsUnknownMembers()
    {
        string json = ValidDefinition.Replace(
            "\"schemaVersion\": 3,",
            "\"schemaVersion\": 3,\n  \"unconsumed\": true,",
            StringComparison.Ordinal
        );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "unknown.json")
        );

        Assert.Contains("unconsumed", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms missing and unsupported schema versions are structural failures.</summary>
    [Theory]
    [InlineData("\"schemaVersion\": 3,", "")]
    [InlineData("\"schemaVersion\": 3", "\"schemaVersion\": 2")]
    [InlineData("\"schemaVersion\": 3", "\"schemaVersion\": 4")]
    public void RejectsWrongOrMissingSchemaVersion(string original, string replacement)
    {
        string json = ValidDefinition.Replace(original, replacement, StringComparison.Ordinal);

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "version.json")
        );

        Assert.Contains("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirms the removed instance starting condition cannot enter reusable design content.</summary>
    [Fact]
    public void RejectsRemovedInitialSensorIntegrity()
    {
        string json = ValidDefinition.Replace(
            "\"sensorRepairDurationMilliseconds\": 8000",
            "\"initialSensorIntegrity\": 0.4,\n  \"sensorRepairDurationMilliseconds\": 8000",
            StringComparison.Ordinal
        );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "removed-field.json")
        );

        Assert.Contains("initialSensorIntegrity", exception.Message, StringComparison.Ordinal);
        Assert.Contains("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirms both version-three sensor capabilities are required.</summary>
    [Theory]
    [InlineData("  \"passiveSensorRangeKilometers\": 30.0,\n")]
    [InlineData("  \"activeScanDurationMilliseconds\": 2000,\n")]
    public void RejectsMissingSensorCapability(string removedMember)
    {
        string json = ValidDefinition.Replace(removedMember, string.Empty, StringComparison.Ordinal);

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "missing-capability.json")
        );

        Assert.Contains("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirms structural constraints reject values that cannot reach domain construction.</summary>
    [Theory]
    [InlineData("\"maximumTacticalSpeedKilometersPerSecond\": 10", "\"maximumTacticalSpeedKilometersPerSecond\": -1")]
    [InlineData("\"passiveSensorRangeKilometers\": 30.0", "\"passiveSensorRangeKilometers\": -1")]
    [InlineData("\"activeScanDurationMilliseconds\": 2000", "\"activeScanDurationMilliseconds\": 0")]
    public void RejectsStructurallyInvalidDefinition(string original, string replacement)
    {
        string json = ValidDefinition.Replace(original, replacement, StringComparison.Ordinal);

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "structural.json")
        );

        Assert.Contains("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirms game-rule invariants remain a semantic validation stage after schema validation.</summary>
    [Theory]
    [InlineData("\"designDisplayName\": \"Pathfinder class\"", "\"designDisplayName\": \"   \"")]
    [InlineData(
        "\"maximumTacticalSpeedKilometersPerSecond\": 10",
        "\"maximumTacticalSpeedKilometersPerSecond\": 1e400"
    )]
    [InlineData("\"passiveSensorRangeKilometers\": 30.0", "\"passiveSensorRangeKilometers\": 1e400")]
    [InlineData("\"activeScanDurationMilliseconds\": 2000", "\"activeScanDurationMilliseconds\": 2050")]
    [InlineData("\"sensorRepairDurationMilliseconds\": 8000", "\"sensorRepairDurationMilliseconds\": 8050")]
    public void RejectsSemanticallyInvalidDefinition(string original, string replacement)
    {
        string json = ValidDefinition.Replace(original, replacement, StringComparison.Ordinal);

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "semantic.json")
        );

        Assert.Contains("semantic", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("#", exception.Diagnostics[0].InstanceLocation, StringComparer.Ordinal);
    }

    /// <summary>Confirms catalog registration rejects stable identities repeated across inputs.</summary>
    [Fact]
    public void RejectsDuplicateCatalogIdentifiers()
    {
        ShipDefinitionCatalogLoader loader = CreateLoader();

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            loader.LoadCatalog([
                ShipDefinitionContent.FromText("first.json", ValidDefinition),
                ShipDefinitionContent.FromText("second.json", ValidDefinition),
            ])
        );

        Assert.Contains("pathfinder", exception.Message, StringComparison.Ordinal);
        Assert.Contains("first.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("second.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms catalog admission is finite and accepts the documented maximum.</summary>
    [Fact]
    public void BoundsCatalogDefinitionMaterialization()
    {
        ShipDefinitionContent[] maximum =
        [
            .. Enumerable
                .Range(0, ShipDefinitionCatalogLoader.MaximumDefinitions)
                .Select(index =>
                    ShipDefinitionContent.FromText($"ship-{index}.json", DefinitionWithId($"ship-{index}"))
                ),
        ];

        ShipDefinitionCatalog catalog = CreateLoader().LoadCatalog(maximum);

        Assert.Equal(ShipDefinitionCatalogLoader.MaximumDefinitions, catalog.Definitions.Count);
        Assert.Throws<ArgumentException>(() =>
            CreateLoader()
                .LoadCatalog(
                    OverflowAfter(
                        ShipDefinitionContent.FromText("overflow.json", ValidDefinition),
                        ShipDefinitionCatalogLoader.MaximumDefinitions + 1
                    )
                )
        );
    }

    /// <summary>Confirms diagnostics are stable, source-aware, and carry instance/schema locations.</summary>
    [Fact]
    public void ProducesDeterministicUsefulDiagnostics()
    {
        string json = ValidDefinition.Replace(
            "\"maximumTacticalSpeedKilometersPerSecond\": 10",
            "\"maximumTacticalSpeedKilometersPerSecond\": -1",
            StringComparison.Ordinal
        );
        ShipDefinitionCatalogLoader loader = CreateLoader();

        ShipContentValidationException first = Assert.Throws<ShipContentValidationException>(() =>
            loader.LoadText(json, "diagnostic.json")
        );
        ShipContentValidationException second = Assert.Throws<ShipContentValidationException>(() =>
            loader.LoadText(json, "diagnostic.json")
        );

        Assert.Equal(first.Message, second.Message);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.All(first.Diagnostics, diagnostic => Assert.Equal("diagnostic.json", diagnostic.SourceIdentity));
        Assert.Contains(
            first.Diagnostics,
            diagnostic => diagnostic.InstanceLocation.Contains("maximumTacticalSpeed", StringComparison.Ordinal)
        );
        Assert.Contains(first.Diagnostics, diagnostic => !string.IsNullOrWhiteSpace(diagnostic.SchemaLocation));
    }

    /// <summary>Confirms every authored-input path rejects oversized documents before parsing.</summary>
    [Fact]
    public void RejectsOversizedDocumentsAcrossInputForms()
    {
        string oversized = new(' ', (256 * 1024) + 1);
        byte[] bytes = Encoding.UTF8.GetBytes(oversized);

        ShipContentValidationException text = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(oversized, "large-text.json")
        );
        ShipContentValidationException utf8 = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadUtf8(bytes, "large-bytes.json")
        );
        using var stream = new MemoryStream(bytes);
        ShipContentValidationException streamed = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().Load(stream, "large-stream.json")
        );

        Assert.Equal("content.size-limit", text.Diagnostics.Single().Code);
        Assert.Equal("content.size-limit", utf8.Diagnostics.Single().Code);
        Assert.Equal("content.size-limit", streamed.Diagnostics.Single().Code);
    }

    /// <summary>Confirms the documented byte ceiling is inclusive for every authored-input path.</summary>
    [Fact]
    public void AcceptsDocumentsAtTheExactByteLimit()
    {
        const int maximumDocumentBytes = 256 * 1024;
        string exact =
            ValidDefinition + new string(' ', maximumDocumentBytes - Encoding.UTF8.GetByteCount(ValidDefinition));
        byte[] bytes = Encoding.UTF8.GetBytes(exact);
        ShipDefinitionCatalogLoader loader = CreateLoader();

        ShipDefinition fromText = loader.LoadText(exact, "limit-text.json");
        ShipDefinition fromBytes = loader.LoadUtf8(bytes, "limit-bytes.json");
        using var stream = new MemoryStream(bytes);
        ShipDefinition fromStream = loader.Load(stream, "limit-stream.json");

        Assert.Equal(maximumDocumentBytes, bytes.Length);
        Assert.Equal(fromText, fromBytes);
        Assert.Equal(fromText, fromStream);
    }

    private static ShipDefinitionCatalogLoader CreateLoader() =>
        new(
            File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    "src/AlterCourse.Godot/content/schemas/ship-definition-v3.schema.json"
                )
            )
        );

    private static string DefinitionWithId(string id) =>
        ValidDefinition.Replace("\"id\": \"pathfinder\"", $"\"id\": \"{id}\"", StringComparison.Ordinal);

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
