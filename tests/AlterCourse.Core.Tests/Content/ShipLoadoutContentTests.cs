using System.Text;
using System.Text.Json;
using AlterCourse.Core.Content;
using AlterCourse.Core.Ships;
using Json.Schema;

namespace AlterCourse.Core.Tests.Content;

/// <summary>
/// Verifies the ship-content V6 schema and the initial-loadout reader: explicit installed identities, legal gaps,
/// explicit allocator continuation, and definition references resolved against a fully loaded system catalog.
/// </summary>
public sealed class ShipLoadoutContentTests
{
    private static readonly Uri ShipSchemaBaseUri = new(
        "https://l3digital.net/star-trek-alter-course/schemas/ship-definition-v6.schema.json"
    );

    // The design's production Pathfinder V6 shape; one entry per line so cases can replace unique fragments.
    private const string PathfinderV6 = """
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

    /// <summary>Confirms the production-shaped loadout reads with explicit identities and continuation.</summary>
    [Fact]
    public void ReadsPathfinderLoadoutAgainstProductionCatalog()
    {
        ShipLoadoutDefinition loadout = ReadLoadout(PathfinderV6);

        Assert.Equal(6, loadout.NextInstalledSystemId);
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, loadout.Systems.Select(system => system.Id.Value));
        Assert.Equal(
            [
                "pathfinder.power-generation",
                "pathfinder.sensors",
                "pathfinder.impulse-propulsion",
                "pathfinder.shields",
                "pathfinder.directed-energy-weapons",
            ],
            loadout.Systems.Select(system => system.DefinitionId.Value),
            StringComparer.Ordinal
        );
    }

    /// <summary>
    /// Confirms nonconsecutive identities are legal and the continuation is kept verbatim rather than recomputed
    /// as the largest identity plus one.
    /// </summary>
    [Fact]
    public void PreservesGapsAndExplicitContinuation()
    {
        string json = Loadout(
            5000,
            ("900", "pathfinder.shields"),
            ("2", "pathfinder.power-generation"),
            ("17", "pathfinder.sensors")
        );

        ShipLoadoutDefinition loadout = ReadLoadout(json);

        Assert.Equal(5000, loadout.NextInstalledSystemId);
        Assert.Equal(new long[] { 2, 17, 900 }, loadout.Systems.Select(system => system.Id.Value));
    }

    /// <summary>Confirms entry order never assigns identity: reordered entries yield an equal, canonical loadout.</summary>
    [Fact]
    public void ReorderedEntriesProduceEqualLoadouts()
    {
        ShipLoadoutDefinition forward = ReadLoadout(
            Loadout(10, ("3", "pathfinder.sensors"), ("7", "pathfinder.shields"), ("1", "pathfinder.power-generation"))
        );
        ShipLoadoutDefinition reversed = ReadLoadout(
            Loadout(10, ("1", "pathfinder.power-generation"), ("7", "pathfinder.shields"), ("3", "pathfinder.sensors"))
        );

        Assert.Equal(forward, reversed);
        Assert.Equal(forward.GetHashCode(), reversed.GetHashCode());
        Assert.Equal(new long[] { 1, 3, 7 }, forward.Systems.Select(system => system.Id.Value));
    }

    /// <summary>Confirms an explicitly empty loadout is legal content, distinct from an omitted loadout.</summary>
    [Fact]
    public void DistinguishesExplicitEmptyFromOmittedLoadout()
    {
        ShipLoadoutDefinition empty = ReadLoadout(Loadout(1));
        Assert.Empty(empty.Systems);
        Assert.Equal(1, empty.NextInstalledSystemId);

        // An empty loadout still carries its own continuation; history from removed installations survives.
        Assert.Equal(42, ReadLoadout(Loadout(42)).NextInstalledSystemId);

        string omittedLoadout = """
            { "schemaVersion": 6, "id": "hulk", "designDisplayName": "Hulk" }
            """;
        Assert.Contains(
            AssertRejected(omittedLoadout).Diagnostics,
            diagnostic =>
                string.Equals(diagnostic.Code, "schema.required", StringComparison.Ordinal)
                && string.Equals(diagnostic.InstanceLocation, "#", StringComparison.Ordinal)
        );

        string omittedSystems = """
            { "schemaVersion": 6, "id": "hulk", "designDisplayName": "Hulk", "initialLoadout": { "nextInstalledSystemId": 1 } }
            """;
        Assert.Contains(
            AssertRejected(omittedSystems).Diagnostics,
            diagnostic =>
                string.Equals(diagnostic.Code, "schema.required", StringComparison.Ordinal)
                && string.Equals(diagnostic.InstanceLocation, "#/initialLoadout", StringComparison.Ordinal)
        );
    }

    /// <summary>Confirms a repeated installed identity is rejected at its later occurrence.</summary>
    [Fact]
    public void RejectsDuplicateInstalledIdentity()
    {
        string json = Loadout(9, ("4", "pathfinder.sensors"), ("4", "pathfinder.shields"));

        ShipContentDiagnostic diagnostic = Assert.Single(AssertRejected(json).Diagnostics);

        Assert.Equal("loadout.duplicate-installed-id", diagnostic.Code);
        Assert.Equal("#/initialLoadout/systems/1/installedSystemId", diagnostic.InstanceLocation);
        Assert.Equal("ship.json", diagnostic.SourceIdentity);
    }

    /// <summary>Confirms a continuation at or below a listed identity is rejected, never repaired to max + 1.</summary>
    [Theory]
    [InlineData(17)]
    [InlineData(5)]
    public void RejectsAllocatorBehindListedIdentities(long next)
    {
        string json = Loadout(next, ("2", "pathfinder.power-generation"), ("17", "pathfinder.sensors"));

        ShipContentDiagnostic diagnostic = Assert.Single(AssertRejected(json).Diagnostics);

        Assert.Equal("loadout.allocator-behind", diagnostic.Code);
        Assert.Equal("#/initialLoadout/nextInstalledSystemId", diagnostic.InstanceLocation);
    }

    /// <summary>Confirms a reference missing from the loaded catalog is rejected at its exact path.</summary>
    [Fact]
    public void RejectsUnresolvedDefinitionReference()
    {
        string json = Loadout(9, ("1", "pathfinder.power-generation"), ("2", "pathfinder.warp-core"));

        ShipContentDiagnostic diagnostic = Assert.Single(AssertRejected(json).Diagnostics);

        Assert.Equal("reference.unresolved-definition", diagnostic.Code);
        Assert.Equal("#/initialLoadout/systems/1/definitionId", diagnostic.InstanceLocation);
        Assert.Contains("pathfinder.warp-core", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms all storage failures in one loadout are reported together.</summary>
    [Fact]
    public void ReportsEveryLoadoutFailure()
    {
        string json = Loadout(3, ("3", "nope.a"), ("3", "pathfinder.sensors"));

        Assert.Equal(
            ["loadout.allocator-behind", "loadout.duplicate-installed-id", "reference.unresolved-definition"],
            AssertRejected(json).Diagnostics.Select(diagnostic => diagnostic.Code).Order(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
    }

    /// <summary>
    /// Confirms a loadout may reference an alternative definition of an existing kind when the catalog it is
    /// resolved against contains that definition.
    /// </summary>
    [Fact]
    public void ResolvesAlternativeDefinitionFromSuppliedCatalog()
    {
        string alternate = """
            { "schemaVersion": 1, "definitions": [ { "id": "test.long-range-sensors", "kind": "sensors", "componentLabel": "Long-range sensors", "commonOrder": 200, "conditionParticipation": true, "repair": { "fullRepairDurationMilliseconds": 5000, "actionOrder": 300 }, "power": { "nominalDemandPowerUnits": 60 }, "sensors": { "passiveRangeKilometers": 45, "activeScanDurationMilliseconds": 1500 } } ] }
            """;
        SystemDefinitionCatalog catalog = new SystemDefinitionCatalogLoader(
            ReadRepositoryFile("schemas/system-definition-v1.schema.json")
        ).LoadCatalog([
            SystemDefinitionContent.FromText(
                "pathfinder-systems.json",
                ReadRepositoryFile("systems/pathfinder-systems.json")
            ),
            SystemDefinitionContent.FromText("test.json", alternate),
        ]);
        string json = Loadout(3, ("1", "pathfinder.power-generation"), ("2", "test.long-range-sensors"));

        ShipLoadoutDefinition loadout = ReadLoadout(json, catalog);

        Assert.Equal(new SystemDefinitionId("test.long-range-sensors"), loadout.Systems[1].DefinitionId);
        Assert.Contains(
            AssertRejected(json).Diagnostics,
            diagnostic => string.Equals(diagnostic.Code, "reference.unresolved-definition", StringComparison.Ordinal)
        );
    }

    /// <summary>Confirms the V6 schema enforces closed objects and the authored identity and count bounds.</summary>
    [Theory]
    [InlineData(
        "\"nextInstalledSystemId\": 6",
        "\"nextInstalledSystemId\": 0",
        "schema.minimum",
        "#/initialLoadout/nextInstalledSystemId"
    )]
    [InlineData(
        "\"nextInstalledSystemId\": 6",
        "\"nextInstalledSystemId\": 1000002",
        "schema.maximum",
        "#/initialLoadout/nextInstalledSystemId"
    )]
    [InlineData(
        "\"installedSystemId\": 1,",
        "\"installedSystemId\": 0,",
        "schema.minimum",
        "#/initialLoadout/systems/0/installedSystemId"
    )]
    [InlineData(
        "\"installedSystemId\": 1,",
        "\"installedSystemId\": 1000001,",
        "schema.maximum",
        "#/initialLoadout/systems/0/installedSystemId"
    )]
    [InlineData(
        "\"installedSystemId\": 1,",
        "\"installedSystemId\": 1.5,",
        "schema.type",
        "#/initialLoadout/systems/0/installedSystemId"
    )]
    [InlineData(
        "\"installedSystemId\": 1,",
        "\"installedSystemId\": 1, \"condition\": 1,",
        "schema.additionalProperties",
        "#/initialLoadout/systems/0"
    )]
    [InlineData(
        "\"nextInstalledSystemId\": 6,",
        "\"nextInstalledSystemId\": 6, \"extra\": [],",
        "schema.additionalProperties",
        "#/initialLoadout"
    )]
    [InlineData(
        "\"schemaVersion\": 6,",
        "\"schemaVersion\": 6, \"engineering\": {},",
        "schema.additionalProperties",
        "#"
    )]
    [InlineData("\"schemaVersion\": 6", "\"schemaVersion\": 5", "schema.const", "#/schemaVersion")]
    [InlineData(
        "\"definitionId\": \"pathfinder.sensors\"",
        "\"definitionId\": \"path finder\"",
        "schema.pattern",
        "#/initialLoadout/systems/1/definitionId"
    )]
    [InlineData(
        "\"installedSystemId\": 1,",
        "\"installedSystemId\": 1, \"installedSystemId\": 1,",
        "json.duplicate-member",
        "byte:"
    )]
    public void RejectsSchemaViolations(string original, string replacement, string code, string location)
    {
        Assert.Contains(original, PathfinderV6, StringComparison.Ordinal);
        string json = PathfinderV6.Replace(original, replacement, StringComparison.Ordinal);

        Assert.Contains(
            AssertRejected(json).Diagnostics,
            diagnostic =>
                string.Equals(diagnostic.Code, code, StringComparison.Ordinal)
                && diagnostic.InstanceLocation.StartsWith(location, StringComparison.Ordinal)
        );
    }

    /// <summary>Confirms the schema caps a loadout at the shared per-ship installation limit.</summary>
    [Fact]
    public void BoundsInstallationCount()
    {
        (string, string)[] sixteen = Enumerable
            .Range(1, ShipSystemLimits.MaximumInstalledSystemsPerShip)
            .Select(index => (index.ToString(System.Globalization.CultureInfo.InvariantCulture), "pathfinder.shields"))
            .ToArray();
        Assert.Equal(16, ReadLoadout(Loadout(17, sixteen)).Systems.Count);

        (string, string)[] seventeen = [.. sixteen, ("17", "pathfinder.shields")];
        Assert.Contains(
            AssertRejected(Loadout(18, seventeen)).Diagnostics,
            diagnostic =>
                string.Equals(diagnostic.Code, "schema.maxItems", StringComparison.Ordinal)
                && string.Equals(diagnostic.InstanceLocation, "#/initialLoadout/systems", StringComparison.Ordinal)
        );
    }

    /// <summary>Confirms production V5 ship content does not satisfy the V6 schema; history is never reinterpreted.</summary>
    [Fact]
    public void RejectsHistoricalV5ShipContent()
    {
        string v5 = ReadRepositoryFile("ships/pathfinder.json");
        Assert.Contains("\"schemaVersion\": 5", v5, StringComparison.Ordinal);

        Assert.Contains(
            AssertRejected(v5).Diagnostics,
            diagnostic =>
                string.Equals(diagnostic.Code, "schema.const", StringComparison.Ordinal)
                && string.Equals(diagnostic.InstanceLocation, "#/schemaVersion", StringComparison.Ordinal)
        );
    }

    /// <summary>Confirms the domain loadout type enforces the same storage rules as the reader.</summary>
    [Fact]
    public void LoadoutDefinitionEnforcesStorageRules()
    {
        InitialInstalledSystem Entry(long id) => new(new InstalledSystemId(id), new SystemDefinitionId("d"));

        Assert.Throws<ArgumentException>(() => new ShipLoadoutDefinition(10, [Entry(3), Entry(3)]));
        Assert.Throws<ArgumentException>(() => new ShipLoadoutDefinition(3, [Entry(3)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShipLoadoutDefinition(0, []));
        Assert.Throws<ArgumentNullException>(() => new ShipLoadoutDefinition(2, [null!]));
        Assert.Throws<ArgumentException>(() => new InitialInstalledSystem(default, new SystemDefinitionId("d")));
        Assert.Throws<ArgumentException>(() => new InitialInstalledSystem(new InstalledSystemId(1), default));

        // The count bound is applied before materializing: enumeration stops at the first excess entry.
        Assert.Throws<ArgumentException>(() =>
            new ShipLoadoutDefinition(100, OverflowAfter(Entry, ShipSystemLimits.MaximumInstalledSystemsPerShip + 1))
        );

        // The full identity range is legal at runtime even though authored content stops at 1,000,000.
        var maximal = new ShipLoadoutDefinition(long.MaxValue, [Entry(InstalledSystemId.MaximumValue)]);
        Assert.Equal(long.MaxValue, maximal.NextInstalledSystemId);

        var loadout = new ShipLoadoutDefinition(900, [Entry(17), Entry(2)]);
        Assert.Equal(new long[] { 2, 17 }, loadout.Systems.Select(system => system.Id.Value));
        Assert.Throws<NotSupportedException>(() => ((IList<InitialInstalledSystem>)loadout.Systems).Clear());
        Assert.NotEqual(loadout, new ShipLoadoutDefinition(901, [Entry(2), Entry(17)]));
    }

    private static string Loadout(long next, params (string Id, string DefinitionId)[] systems) =>
        "{ \"schemaVersion\": 6, \"id\": \"pathfinder\", \"designDisplayName\": \"Pathfinder class\", \"initialLoadout\": { \"nextInstalledSystemId\": "
        + next.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + ", \"systems\": ["
        + string.Join(
            ", ",
            systems.Select(system =>
                $"{{ \"installedSystemId\": {system.Id}, \"definitionId\": \"{system.DefinitionId}\" }}"
            )
        )
        + "] } }";

    private static ShipContentValidationException AssertRejected(string json) =>
        Assert.Throws<ShipContentValidationException>(() => ReadLoadout(json));

    /// <summary>
    /// Composes the admission stages the V6 ship loader will run: strict parse, V6 schema, then the loadout reader
    /// against an already complete system catalog.
    /// </summary>
    private static ShipLoadoutDefinition ReadLoadout(string json, SystemDefinitionCatalog? catalog = null)
    {
        catalog ??= LoadProductionCatalog();
        var schema = JsonSchema.FromText(
            ReadRepositoryFile("schemas/ship-definition-v6.schema.json"),
            new BuildOptions { SchemaRegistry = new SchemaRegistry() },
            ShipSchemaBaseUri
        );
        using JsonDocument document = StrictContentJson.Parse(Encoding.UTF8.GetBytes(json), "ship.json", 8);
        IReadOnlyList<ShipContentDiagnostic> schemaDiagnostics = StrictContentJson.EvaluateSchema(
            schema,
            document.RootElement,
            "ship.json"
        );
        if (schemaDiagnostics.Count > 0)
        {
            throw new ShipContentValidationException(schemaDiagnostics);
        }

        var diagnostics = new List<ShipContentDiagnostic>();
        ShipLoadoutDefinition? loadout = SystemDefinitionLoadoutReader.Read(
            document.RootElement.GetProperty("initialLoadout"),
            "#/initialLoadout",
            catalog,
            "ship.json",
            diagnostics
        );
        if (loadout is null)
        {
            Assert.NotEmpty(diagnostics);
            throw new ShipContentValidationException(StrictContentJson.Sort(diagnostics));
        }

        Assert.Empty(diagnostics);
        return loadout;
    }

    private static SystemDefinitionCatalog LoadProductionCatalog() =>
        new SystemDefinitionCatalogLoader(ReadRepositoryFile("schemas/system-definition-v1.schema.json")).LoadCatalog([
            SystemDefinitionContent.FromText(
                "pathfinder-systems.json",
                ReadRepositoryFile("systems/pathfinder-systems.json")
            ),
        ]);

    private static string ReadRepositoryFile(string contentRelativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src/AlterCourse.Godot/content", contentRelativePath));

    private static IEnumerable<T> OverflowAfter<T>(Func<long, T> factory, int yieldedCount)
    {
        for (int index = 1; index <= yieldedCount; index++)
        {
            yield return factory(index);
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
