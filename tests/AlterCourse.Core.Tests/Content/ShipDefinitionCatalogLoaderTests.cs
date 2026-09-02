using System.Text;
using AlterCourse.Core.Content;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Tests.Content;

/// <summary>Verifies strict, versioned admission of authored player-ship definitions.</summary>
public sealed class ShipDefinitionCatalogLoaderTests
{
    private const string ValidDefinition = """
        {
          "schemaVersion": 1,
          "id": "pathfinder",
          "displayName": "Pathfinder",
          "maximumTacticalSpeedKilometersPerSecond": 10,
          "initialSensorIntegrity": 0.4,
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
        Assert.Equal("Pathfinder", fromText.DisplayName);
        Assert.Equal(10, fromText.MaximumTacticalSpeed.Value);
        Assert.Equal(0.4, fromText.InitialSensorIntegrity.Value);
        Assert.Equal(8000, fromText.SensorRepairDuration.Milliseconds);
    }

    /// <summary>Confirms the repository's canonical schema and ship definition remain load-compatible.</summary>
    [Fact]
    public void LoadsCanonicalPlayerShipDefinition()
    {
        string root = FindRepositoryRoot();
        string schema = File.ReadAllText(
            Path.Combine(root, "src/AlterCourse.Godot/content/schemas/ship-definition-v1.schema.json")
        );
        string definition = File.ReadAllText(Path.Combine(root, "src/AlterCourse.Godot/content/ships/pathfinder.json"));

        ShipDefinition ship = new ShipDefinitionCatalogLoader(schema).LoadText(
            definition,
            "res://content/ships/pathfinder.json"
        );

        Assert.Equal(new ShipDefinitionId("pathfinder"), ship.Id);
        Assert.Equal("USS Pathfinder", ship.DisplayName);
    }

    /// <summary>Confirms malformed and truncated JSON fail closed with source-aware diagnostics.</summary>
    [Theory]
    [InlineData("{\"schemaVersion\":1")]
    [InlineData("{\"schemaVersion\":1} trailing")]
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
            "\"displayName\": \"Pathfinder\",",
            "\"displayName\": \"Pathfinder\",\n  \"displayName\": \"Duplicate\",",
            StringComparison.Ordinal
        );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "duplicate.json")
        );

        Assert.Contains("duplicate JSON member 'displayName'", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirms schema-unknown members cannot silently enter the authored contract.</summary>
    [Fact]
    public void RejectsUnknownMembers()
    {
        string json = ValidDefinition.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"unconsumed\": true,",
            StringComparison.Ordinal
        );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "unknown.json")
        );

        Assert.Contains("unconsumed", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms missing and unsupported schema versions are structural failures.</summary>
    [Theory]
    [InlineData("\"schemaVersion\": 1,", "")]
    [InlineData("\"schemaVersion\": 1", "\"schemaVersion\": 2")]
    public void RejectsWrongOrMissingSchemaVersion(string original, string replacement)
    {
        string json = ValidDefinition.Replace(original, replacement, StringComparison.Ordinal);

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "version.json")
        );

        Assert.Contains("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirms structural constraints reject values that cannot reach domain construction.</summary>
    [Fact]
    public void RejectsStructurallyInvalidDefinition()
    {
        string json = ValidDefinition.Replace(
            "\"maximumTacticalSpeedKilometersPerSecond\": 10",
            "\"maximumTacticalSpeedKilometersPerSecond\": -1",
            StringComparison.Ordinal
        );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "structural.json")
        );

        Assert.Contains("maximumTacticalSpeedKilometersPerSecond", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms game-rule invariants remain a semantic validation stage after schema validation.</summary>
    [Theory]
    [InlineData("\"displayName\": \"Pathfinder\"", "\"displayName\": \"   \"")]
    [InlineData(
        "\"maximumTacticalSpeedKilometersPerSecond\": 10",
        "\"maximumTacticalSpeedKilometersPerSecond\": 1e400"
    )]
    [InlineData("\"initialSensorIntegrity\": 0.4", "\"initialSensorIntegrity\": 1")]
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

    private static ShipDefinitionCatalogLoader CreateLoader() =>
        new(
            File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    "src/AlterCourse.Godot/content/schemas/ship-definition-v1.schema.json"
                )
            )
        );

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
