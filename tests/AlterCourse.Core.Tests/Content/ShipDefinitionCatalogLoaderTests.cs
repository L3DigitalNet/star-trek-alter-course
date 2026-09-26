using System.Text;
using AlterCourse.Core.Content;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Content;

/// <summary>Verifies strict, versioned admission of authored V6 ship designs against the system catalog.</summary>
/// <remarks>
/// V6 designs carry identity, display name, and an initial loadout that references system definitions; every
/// capability value (speed, range, scan and repair timing, power, weapon tuning) is owned by those system
/// definitions and verified by the system-definition loader tests.
/// </remarks>
public sealed class ShipDefinitionCatalogLoaderTests
{
    private const string ValidDefinition = """
        {
          "schemaVersion": 6,
          "id": "pathfinder",
          "designDisplayName": "Pathfinder class",
          "initialLoadout": {
            "nextInstalledSystemId": 6,
            "systems": [
              { "installedSystemId": 1, "definitionId": "pathfinder.power-generation" },
              { "installedSystemId": 2, "definitionId": "pathfinder.sensors" },
              { "installedSystemId": 3, "definitionId": "pathfinder.impulse-propulsion" },
              { "installedSystemId": 4, "definitionId": "pathfinder.shields" },
              { "installedSystemId": 5, "definitionId": "pathfinder.directed-energy-weapons" }
            ]
          }
        }
        """;

    /// <summary>Confirms V5 content — valid under its own historical schema — cannot enter the V6 loader.</summary>
    [Fact]
    public void CurrentLoaderRejectsHistoricalV5Content()
    {
        const string historicalContent = """
            {
              "schemaVersion": 5,
              "id": "pathfinder",
              "designDisplayName": "Pathfinder class",
              "maximumTacticalSpeedKilometersPerSecond": 10,
              "passiveSensorRangeKilometers": 30.0,
              "activeScanDurationMilliseconds": 2000,
              "engineering": { "nominalGenerationPowerUnits": 120, "nominalSensorDemandPowerUnits": 70, "nominalImpulseDemandPowerUnits": 50, "sensorRepairDurationMilliseconds": 8000, "impulseRepairDurationMilliseconds": 6000, "nominalShieldDemandPowerUnits": 40, "nominalDirectedEnergyDemandPowerUnits": 30, "shieldRepairDurationMilliseconds": 8000, "directedEnergyRepairDurationMilliseconds": 6000 }, "directedEnergyWeapon": { "rangeKilometers": 20, "baseNormalizedDamage": 0.25, "cooldownMilliseconds": 2000 }
            }
            """;
        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(historicalContent, "historical.json")
        );
        Assert.Contains(
            exception.Diagnostics,
            diagnostic =>
                string.Equals(diagnostic.Code, "schema.const", StringComparison.Ordinal)
                && string.Equals(diagnostic.InstanceLocation, "#/schemaVersion", StringComparison.Ordinal)
        );
    }

    /// <summary>Confirms text, UTF-8 bytes, and streams map to the same resolved design and loadout.</summary>
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
        Assert.Equal(6, fromText.InitialLoadout.NextInstalledSystemId);
        Assert.Equal([1L, 2L, 3L, 4L, 5L], fromText.InitialLoadout.Systems.Select(system => system.Id.Value));
        Assert.Equal("pathfinder.directed-energy-weapons", fromText.InitialLoadout.Systems[4].DefinitionId.Value);
    }

    /// <summary>Confirms schema-valid integral numeric forms map to the authored integer contract.</summary>
    [Fact]
    public void LoadsSchemaValidIntegralNumericForms()
    {
        string json = ValidDefinition
            .Replace("\"schemaVersion\": 6", "\"schemaVersion\": 6.0", StringComparison.Ordinal)
            .Replace("\"nextInstalledSystemId\": 6", "\"nextInstalledSystemId\": 6e0", StringComparison.Ordinal);

        ShipDefinition definition = CreateLoader().LoadText(json, "integral-forms.json");

        Assert.Equal(6, definition.InitialLoadout.NextInstalledSystemId);
    }

    /// <summary>Confirms the repository's canonical V6 design loads against the production system catalog.</summary>
    [Fact]
    public void LoadsCanonicalPlayerShipDefinition()
    {
        ShipDefinition ship = CreateLoader()
            .LoadText(
                TestShipContent.ReadRepositoryFile("src/AlterCourse.Godot/content/ships/pathfinder.json"),
                "res://content/ships/pathfinder.json"
            );

        Assert.Equal(new ShipDefinitionId("pathfinder"), ship.Id);
        Assert.Equal("Pathfinder class", ship.DesignDisplayName);
        Assert.Equal(5, ship.InitialLoadout.Systems.Count);
    }

    /// <summary>Confirms the domain and canonical schema share the exact persisted identity boundary.</summary>
    [Fact]
    public void EnforcesShipDefinitionIdentityLengthAcrossDomainAndContent()
    {
        string maximumId = new('i', ShipDefinitionId.MaximumLength);

        ShipDefinition loaded = CreateLoader().LoadText(DefinitionWithId(maximumId), "maximum-id.json");

        Assert.Equal(maximumId, loaded.Id.Value);
        Assert.Throws<ArgumentException>(() =>
            new ShipDefinitionId(new string('i', ShipDefinitionId.MaximumLength + 1))
        );
        Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader()
                .LoadText(DefinitionWithId(new string('i', ShipDefinitionId.MaximumLength + 1)), "oversized-id.json")
        );
    }

    /// <summary>Confirms domain construction and semantic admission share the 64-character design-name bound.</summary>
    [Fact]
    public void EnforcesDesignDisplayNameLengthAcrossDomainAndContent()
    {
        string maximumName = new('n', ShipDefinition.MaximumDesignDisplayNameLength);
        string oversizedName = new('n', ShipDefinition.MaximumDesignDisplayNameLength + 1);
        ShipDefinition loaded = CreateLoader()
            .LoadText(
                ValidDefinition.Replace("Pathfinder class", maximumName, StringComparison.Ordinal),
                "maximum-name.json"
            );

        Assert.Equal(maximumName, loaded.DesignDisplayName);
        Assert.Throws<ArgumentException>(() =>
            new ShipDefinition(new ShipDefinitionId("oversized"), oversizedName, loaded.InitialLoadout)
        );
        Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader()
                .LoadText(
                    ValidDefinition.Replace("Pathfinder class", oversizedName, StringComparison.Ordinal),
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
    public void RejectsShipDefinitionIdentityOutsideDurableAlphabet(string identity)
    {
        Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(DefinitionWithId(identity), "invalid-id.json")
        );
    }

    /// <summary>Confirms malformed and truncated JSON fail closed with source-aware diagnostics.</summary>
    [Theory]
    [InlineData("{\"schemaVersion\":6")]
    [InlineData("{\"schemaVersion\":6} trailing")]
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

    /// <summary>
    /// Confirms schema-unknown members — including every V5 capability field, now owned by system definitions —
    /// cannot silently enter the authored design contract.
    /// </summary>
    [Theory]
    [InlineData("\"unconsumed\": true")]
    [InlineData("\"maximumTacticalSpeedKilometersPerSecond\": 10")]
    [InlineData("\"passiveSensorRangeKilometers\": 30.0")]
    [InlineData("\"engineering\": {}")]
    [InlineData("\"directedEnergyWeapon\": {}")]
    public void RejectsUnknownMembers(string member)
    {
        string json = ValidDefinition.Replace(
            "\"schemaVersion\": 6,",
            $"\"schemaVersion\": 6,\n  {member},",
            StringComparison.Ordinal
        );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "unknown.json")
        );

        Assert.Contains("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirms missing and unsupported schema versions are structural failures.</summary>
    [Theory]
    [InlineData("\"schemaVersion\": 6,", "")]
    [InlineData("\"schemaVersion\": 6", "\"schemaVersion\": 4")]
    [InlineData("\"schemaVersion\": 6", "\"schemaVersion\": 5")]
    [InlineData("\"schemaVersion\": 6", "\"schemaVersion\": 7")]
    public void RejectsWrongOrMissingSchemaVersion(string original, string replacement)
    {
        string json = ValidDefinition.Replace(original, replacement, StringComparison.Ordinal);

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "version.json")
        );

        Assert.Contains("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirms the design must author its initial loadout.</summary>
    [Fact]
    public void RejectsMissingInitialLoadout()
    {
        string json = """
            {
              "schemaVersion": 6,
              "id": "pathfinder",
              "designDisplayName": "Pathfinder class"
            }
            """;

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "missing-loadout.json")
        );

        Assert.Contains("schema", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Confirms loadout invariants fail closed: an unresolved definition reference, a repeated installed identity,
    /// and a continuation that does not exceed every authored identity.
    /// </summary>
    [Theory]
    [InlineData("\"pathfinder.shields\"", "\"pathfinder.missing\"")]
    [InlineData("\"installedSystemId\": 5,", "\"installedSystemId\": 4,")]
    [InlineData("\"nextInstalledSystemId\": 6", "\"nextInstalledSystemId\": 5")]
    public void RejectsInvalidLoadout(string original, string replacement)
    {
        string json = ValidDefinition.Replace(original, replacement, StringComparison.Ordinal);

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "loadout.json")
        );

        Assert.StartsWith("#/initialLoadout", exception.Diagnostics[0].InstanceLocation, StringComparison.Ordinal);
    }

    /// <summary>
    /// Confirms an explicit loadout with two installations of one kind is valid storage but is refused at the
    /// typed-world boundary with a distinct cardinality diagnostic, not a malformed-content one.
    /// </summary>
    [Fact]
    public void RejectsUnsupportedCardinalityWithDistinctDiagnostic()
    {
        string json = ValidDefinition
            .Replace("\"nextInstalledSystemId\": 6", "\"nextInstalledSystemId\": 7", StringComparison.Ordinal)
            .Replace(
                "{ \"installedSystemId\": 5, \"definitionId\": \"pathfinder.directed-energy-weapons\" }",
                "{ \"installedSystemId\": 5, \"definitionId\": \"pathfinder.directed-energy-weapons\" },\n"
                    + "              { \"installedSystemId\": 6, \"definitionId\": \"pathfinder.sensors\" }",
                StringComparison.Ordinal
            );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "cardinality.json")
        );

        ShipContentDiagnostic diagnostic = Assert.Single(exception.Diagnostics);
        Assert.Equal("semantic.unsupported-cardinality", diagnostic.Code);
        Assert.Equal("#/initialLoadout/systems/5", diagnostic.InstanceLocation);
    }

    /// <summary>Confirms game-rule invariants remain a semantic validation stage after schema validation.</summary>
    [Fact]
    public void RejectsBlankDesignDisplayNameSemantically()
    {
        string json = ValidDefinition.Replace(
            "\"designDisplayName\": \"Pathfinder class\"",
            "\"designDisplayName\": \"   \"",
            StringComparison.Ordinal
        );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadText(json, "semantic.json")
        );

        Assert.Contains("semantic", exception.Diagnostics[0].Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("#/designDisplayName", exception.Diagnostics[0].InstanceLocation);
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
            "\"nextInstalledSystemId\": 6",
            "\"nextInstalledSystemId\": -1",
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
            diagnostic => diagnostic.InstanceLocation.Contains("nextInstalledSystemId", StringComparison.Ordinal)
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
        TestShipContent.ShipLoader(TestShipContent.ProductionSystems());

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
}
