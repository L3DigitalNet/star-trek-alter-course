using System.Globalization;
using System.Text;
using AlterCourse.Core.Content;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Content;

/// <summary>Verifies strict, bounded admission of system-definition V1 content and its compatibility descriptors.</summary>
public sealed class SystemDefinitionCatalogLoaderTests
{
    private const string ProductionSource = "res://content/systems/pathfinder-systems.json";

    // One definition per kind in a compact single-line-per-definition layout so each negative case can replace an
    // exact, unique fragment. Tuning equals production so positive assertions stay meaningful.
    private const string ValidDocument = """
        {
          "schemaVersion": 1,
          "definitions": [
            { "id": "t.gen", "kind": "power-generation", "componentLabel": "Power generation", "commonOrder": 100, "conditionParticipation": true, "powerGeneration": { "nominalOutputPowerUnits": 120 } },
            { "id": "t.sen", "kind": "sensors", "componentLabel": "Sensors", "commonOrder": 200, "conditionParticipation": true, "repair": { "fullRepairDurationMilliseconds": 8000, "actionOrder": 300 }, "power": { "nominalDemandPowerUnits": 70 }, "sensors": { "passiveRangeKilometers": 30, "activeScanDurationMilliseconds": 2000 } },
            { "id": "t.imp", "kind": "impulse-propulsion", "componentLabel": "Impulse propulsion", "commonOrder": 300, "conditionParticipation": true, "repair": { "fullRepairDurationMilliseconds": 6000, "actionOrder": 400 }, "power": { "nominalDemandPowerUnits": 50 }, "impulsePropulsion": { "maximumTacticalSpeedKilometersPerSecond": 10 } },
            { "id": "t.shd", "kind": "shields", "componentLabel": "Shields", "commonOrder": 400, "conditionParticipation": true, "repair": { "fullRepairDurationMilliseconds": 8000, "actionOrder": 100 }, "power": { "nominalDemandPowerUnits": 40 } },
            { "id": "t.dew", "kind": "directed-energy-weapons", "componentLabel": "Directed-energy weapons", "commonOrder": 500, "conditionParticipation": true, "repair": { "fullRepairDurationMilliseconds": 6000, "actionOrder": 200 }, "power": { "nominalDemandPowerUnits": 30 }, "directedEnergyWeapons": { "rangeKilometers": 20, "baseNormalizedDamage": 0.25, "cooldownMilliseconds": 2000 } }
          ]
        }
        """;

    // The alternate sensor model used by the extension demonstration; it is test content only.
    private const string LongRangeSensorDocument = """
        {
          "schemaVersion": 1,
          "definitions": [
            { "id": "test.long-range-sensors", "kind": "sensors", "componentLabel": "Long-range sensors", "commonOrder": 200, "conditionParticipation": true, "repair": { "fullRepairDurationMilliseconds": 5000, "actionOrder": 300 }, "power": { "nominalDemandPowerUnits": 60 }, "sensors": { "passiveRangeKilometers": 45, "activeScanDurationMilliseconds": 1500 } }
          ]
        }
        """;

    /// <summary>Confirms production content carries today's Pathfinder tuning, order, and labels exactly.</summary>
    [Fact]
    public void LoadsProductionPathfinderDefinitionsWithExactTuning()
    {
        SystemDefinitionCatalog catalog = LoadProduction();

        Assert.Equal(
            [
                "pathfinder.power-generation",
                "pathfinder.sensors",
                "pathfinder.impulse-propulsion",
                "pathfinder.shields",
                "pathfinder.directed-energy-weapons",
            ],
            catalog.Definitions.Select(definition => definition.Id.Value),
            StringComparer.Ordinal
        );
        Assert.Equal(
            ["Power generation", "Sensors", "Impulse propulsion", "Shields", "Directed-energy weapons"],
            catalog.Definitions.Select(definition => definition.ComponentLabel),
            StringComparer.Ordinal
        );
        Assert.Equal([100, 200, 300, 400, 500], catalog.Definitions.Select(definition => definition.CommonOrder));
        Assert.All(catalog.Definitions, definition => Assert.True(definition.ConditionParticipation));

        PowerGenerationSystemDefinition generation = Assert.IsType<PowerGenerationSystemDefinition>(
            catalog.Definitions[0]
        );
        Assert.Equal(ShipSystemKind.PowerGeneration, generation.Kind);
        Assert.Equal(new PowerUnits(120), generation.NominalOutput);
        Assert.Null(generation.Power);
        Assert.Null(generation.Repair);

        SensorSystemDefinition sensors = Assert.IsType<SensorSystemDefinition>(catalog.Definitions[1]);
        Assert.Equal(ShipSystemKind.Sensors, sensors.Kind);
        Assert.Equal(new SystemPowerDemand(new PowerUnits(70)), sensors.Power);
        Assert.Equal(new SystemRepairCapability(new SimulationDuration(8000), 300), sensors.Repair);
        Assert.Equal(new DistanceKilometers(30), sensors.PassiveRange);
        Assert.Equal(new SimulationDuration(2000), sensors.ActiveScanDuration);

        ImpulsePropulsionSystemDefinition impulse = Assert.IsType<ImpulsePropulsionSystemDefinition>(
            catalog.Definitions[2]
        );
        Assert.Equal(ShipSystemKind.ImpulsePropulsion, impulse.Kind);
        Assert.Equal(new SystemPowerDemand(new PowerUnits(50)), impulse.Power);
        Assert.Equal(new SystemRepairCapability(new SimulationDuration(6000), 400), impulse.Repair);
        Assert.Equal(new SpeedKilometersPerSecond(10), impulse.MaximumTacticalSpeed);

        ShieldSystemDefinition shields = Assert.IsType<ShieldSystemDefinition>(catalog.Definitions[3]);
        Assert.Equal(ShipSystemKind.Shields, shields.Kind);
        Assert.Equal(new SystemPowerDemand(new PowerUnits(40)), shields.Power);
        Assert.Equal(new SystemRepairCapability(new SimulationDuration(8000), 100), shields.Repair);

        DirectedEnergyWeaponSystemDefinition weapon = Assert.IsType<DirectedEnergyWeaponSystemDefinition>(
            catalog.Definitions[4]
        );
        Assert.Equal(ShipSystemKind.DirectedEnergyWeapons, weapon.Kind);
        Assert.Equal(new SystemPowerDemand(new PowerUnits(30)), weapon.Power);
        Assert.Equal(new SystemRepairCapability(new SimulationDuration(6000), 200), weapon.Repair);
        Assert.Equal(
            new DirectedEnergyWeaponDefinition(new DistanceKilometers(20), 0.25, new SimulationDuration(2000)),
            weapon.Weapon
        );
    }

    /// <summary>Confirms identity lookups find catalog members and refuse unknown identities.</summary>
    [Fact]
    public void LooksUpDefinitionsByIdentity()
    {
        SystemDefinitionCatalog catalog = LoadProduction();

        Assert.Same(catalog.Definitions[1], catalog.GetRequired(new SystemDefinitionId("pathfinder.sensors")));
        Assert.True(catalog.TryGet(new SystemDefinitionId("pathfinder.shields"), out SystemDefinition? found));
        Assert.Same(catalog.Definitions[3], found);
        Assert.False(catalog.TryGet(new SystemDefinitionId("pathfinder.warp"), out _));
        Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired(new SystemDefinitionId("pathfinder.warp")));
    }

    /// <summary>
    /// Confirms repair-action order reproduces the established Engineering button order (shields, weapons,
    /// sensors, impulse) independently of common order.
    /// </summary>
    [Fact]
    public void ProductionRepairActionOrderReproducesEstablishedButtonOrder()
    {
        SystemDefinitionCatalog catalog = LoadProduction();

        Assert.Equal(
            [
                ShipSystemKind.Shields,
                ShipSystemKind.DirectedEnergyWeapons,
                ShipSystemKind.Sensors,
                ShipSystemKind.ImpulsePropulsion,
            ],
            catalog
                .Definitions.Where(definition => definition.Repair is not null)
                .OrderBy(definition => definition.Repair!.ActionOrder)
                .Select(definition => definition.Kind)
        );
    }

    /// <summary>Confirms production compatibility descriptors equal the design's frozen V9-to-V10 expected values.</summary>
    [Fact]
    public void ProductionDescriptorsArePinned()
    {
        SystemDefinitionCatalog catalog = LoadProduction();

        Assert.Equal(
            [
                "sd1;kind=power-generation;condition=true;order=100;power=none;repair=none;outputPu=120",
                "sd1;kind=sensors;condition=true;order=200;power=70;repair=8000;passiveRangeKm=30;scanMs=2000",
                "sd1;kind=impulse-propulsion;condition=true;order=300;power=50;repair=6000;maxSpeedKmS=10",
                "sd1;kind=shields;condition=true;order=400;power=40;repair=8000",
                "sd1;kind=directed-energy-weapons;condition=true;order=500;power=30;repair=6000;rangeKm=20;baseDamage=0.25;cooldownMs=2000",
            ],
            catalog.Definitions.Select(SystemDefinitionSemantics.Describe),
            StringComparer.Ordinal
        );
    }

    /// <summary>Confirms descriptors ignore the process culture, which would otherwise render 0.25 as 0,25.</summary>
    [Fact]
    public void DescriptorsAreCultureInvariant()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            // A cloned culture with a comma decimal separator works even under invariant-globalization hosts.
            var commaCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            commaCulture.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = commaCulture;
            SystemDefinition weapon = LoadProduction()
                .GetRequired(new SystemDefinitionId("pathfinder.directed-energy-weapons"));
            Assert.EndsWith(
                "rangeKm=20;baseDamage=0.25;cooldownMs=2000",
                SystemDefinitionSemantics.Describe(weapon),
                StringComparison.Ordinal
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// Confirms presentation-only data (label, repair-action order) and the identity key are excluded from the
    /// descriptor, while every simulation-relevant change alters it.
    /// </summary>
    [Fact]
    public void DescriptorCoversSimulationFieldsOnly()
    {
        SensorSystemDefinition baseline = Sensor("a", "Sensors", 200, (8000, 300), 70, 30, 2000);
        string expected = SystemDefinitionSemantics.Describe(baseline);

        // Different key, label, and repair-action order: presentation and lookup only.
        Assert.Equal(
            expected,
            SystemDefinitionSemantics.Describe(Sensor("renamed.b", "Relabeled array", 200, (8000, 999), 70, 30, 2000))
        );

        SensorSystemDefinition[] changed =
        [
            Sensor("a", "Sensors", 201, (8000, 300), 70, 30, 2000),
            Sensor("a", "Sensors", 200, null, 70, 30, 2000),
            Sensor("a", "Sensors", 200, (8100, 300), 70, 30, 2000),
            Sensor("a", "Sensors", 200, (8000, 300), 71, 30, 2000),
            Sensor("a", "Sensors", 200, (8000, 300), 70, 30.5, 2000),
            Sensor("a", "Sensors", 200, (8000, 300), 70, 30, 2100),
        ];
        Assert.All(
            changed,
            definition =>
                Assert.NotEqual(expected, SystemDefinitionSemantics.Describe(definition), StringComparer.Ordinal)
        );
    }

    private static SensorSystemDefinition Sensor(
        string id,
        string label,
        int order,
        (long Milliseconds, int ActionOrder)? repair,
        int demand,
        double range,
        long scanMilliseconds
    ) =>
        new(
            new SystemDefinitionId(id),
            label,
            order,
            true,
            repair is { } value
                ? new SystemRepairCapability(new SimulationDuration(value.Milliseconds), value.ActionOrder)
                : null,
            new SystemPowerDemand(new PowerUnits(demand)),
            new DistanceKilometers(range),
            new SimulationDuration(scanMilliseconds)
        );

    /// <summary>Confirms large doubles use the round-trip exponent form admitted by the descriptor charset.</summary>
    [Fact]
    public void DescriptorRendersExponentFormsWithinCharset()
    {
        var impulse = new ImpulsePropulsionSystemDefinition(
            new SystemDefinitionId("fast"),
            "Fast",
            0,
            true,
            null,
            new SystemPowerDemand(new PowerUnits(1)),
            new SpeedKilometersPerSecond(1e20)
        );

        Assert.Equal(
            "sd1;kind=impulse-propulsion;condition=true;order=0;power=1;repair=none;maxSpeedKmS=1E+20",
            SystemDefinitionSemantics.Describe(impulse)
        );
    }

    /// <summary>Confirms the aim vocabulary is derived from content and matches the historical five-kind order.</summary>
    [Fact]
    public void DerivesDamageTargetKindsFromContent()
    {
        Assert.Equal(
            [
                ShipSystemKind.PowerGeneration,
                ShipSystemKind.Sensors,
                ShipSystemKind.ImpulsePropulsion,
                ShipSystemKind.Shields,
                ShipSystemKind.DirectedEnergyWeapons,
            ],
            LoadProduction().DamageTargetKinds
        );

        // Only kinds that some definition provides are admitted; the full closed kind vocabulary is not.
        string shieldsOnly = """
            { "schemaVersion": 1, "definitions": [ { "id": "s", "kind": "shields", "componentLabel": "Shields", "commonOrder": 7, "conditionParticipation": true, "power": { "nominalDemandPowerUnits": 1 } } ] }
            """;
        Assert.Equal([ShipSystemKind.Shields], Load(("shields.json", shieldsOnly)).DamageTargetKinds);
    }

    /// <summary>
    /// Confirms an alternative definition of an existing kind loads through the same path, coexists with the
    /// production definition of that kind, and does not duplicate the aim kind.
    /// </summary>
    [Fact]
    public void LoadsAlternativeDefinitionOfExistingKind()
    {
        SystemDefinitionCatalog catalog = Load(
            (ProductionSource, ReadProductionText()),
            ("test/long-range-sensors.json", LongRangeSensorDocument)
        );

        Assert.Equal(6, catalog.Definitions.Count);
        Assert.Equal(
            ["pathfinder.sensors", "test.long-range-sensors"],
            catalog
                .Definitions.Where(definition => definition.CommonOrder == 200)
                .Select(definition => definition.Id.Value),
            StringComparer.Ordinal
        );
        SensorSystemDefinition alternate = Assert.IsType<SensorSystemDefinition>(
            catalog.GetRequired(new SystemDefinitionId("test.long-range-sensors"))
        );
        Assert.Equal(new DistanceKilometers(45), alternate.PassiveRange);
        Assert.Equal(new SimulationDuration(1500), alternate.ActiveScanDuration);
        Assert.Equal(new SimulationDuration(5000), alternate.Repair!.FullRepairDuration);
        Assert.Equal(new PowerUnits(60), alternate.Power!.NominalDemand);
        Assert.Equal(
            "sd1;kind=sensors;condition=true;order=200;power=60;repair=5000;passiveRangeKm=45;scanMs=1500",
            SystemDefinitionSemantics.Describe(alternate)
        );
        Assert.Equal(5, catalog.DamageTargetKinds.Count);
    }

    /// <summary>Confirms a consumer without repair is valid content: repairability is exactly presence of repair data.</summary>
    [Fact]
    public void LoadsConsumerWithoutRepair()
    {
        string json = ValidDocument.Replace(
            "\"repair\": { \"fullRepairDurationMilliseconds\": 8000, \"actionOrder\": 100 }, ",
            string.Empty,
            StringComparison.Ordinal
        );

        SystemDefinition shields = Load(("no-repair.json", json)).GetRequired(new SystemDefinitionId("t.shd"));

        Assert.Null(shields.Repair);
        Assert.NotNull(shields.Power);
        Assert.EndsWith("power=40;repair=none", SystemDefinitionSemantics.Describe(shields), StringComparison.Ordinal);
    }

    /// <summary>
    /// Confirms catalog content, order, descriptors, and aim kinds are independent of definition order within a
    /// document and of document order across a split catalog.
    /// </summary>
    [Fact]
    public void CatalogIsIndependentOfInputOrder()
    {
        string[] lines = ValidDocument.Split('\n');
        string[] definitionLines = lines
            .Where(line => line.TrimStart().StartsWith("{ \"id\"", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(5, definitionLines.Length);
        string reversed = BuildDocument(definitionLines.Reverse());
        string first = BuildDocument(definitionLines.Take(2));
        string second = BuildDocument(definitionLines.Skip(2));

        SystemDefinitionCatalog canonical = Load(("one.json", ValidDocument));
        SystemDefinitionCatalog[] variants =
        [
            Load(("one.json", reversed)),
            Load(("a.json", first), ("b.json", second)),
            Load(("b.json", second), ("a.json", first)),
        ];

        foreach (SystemDefinitionCatalog variant in variants)
        {
            Assert.Equal(canonical.Definitions, variant.Definitions);
            Assert.Equal(
                canonical.Definitions.Select(SystemDefinitionSemantics.Describe),
                variant.Definitions.Select(SystemDefinitionSemantics.Describe),
                StringComparer.Ordinal
            );
            Assert.Equal(canonical.DamageTargetKinds, variant.DamageTargetKinds);
        }
    }

    /// <summary>Confirms every schema-owned shape violation is rejected with a schema diagnostic at the offending path.</summary>
    [Theory]
    [InlineData("\"schemaVersion\": 1", "\"schemaVersion\": 2", "schema.const", "#/schemaVersion")]
    [InlineData("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"extra\": true,", "schema.additionalProperties", "#")]
    [InlineData(
        "\"id\": \"t.sen\",",
        "\"id\": \"t.sen\", \"mystery\": 1,",
        "schema.additionalProperties",
        "#/definitions/1"
    )]
    [InlineData(
        "\"nominalDemandPowerUnits\": 70 }",
        "\"nominalDemandPowerUnits\": 70, \"surge\": 1 }",
        "schema.additionalProperties",
        "#/definitions/1/power"
    )]
    [InlineData("\"kind\": \"sensors\"", "\"kind\": \"warp-core\"", "schema.enum", "#/definitions/1/kind")]
    [InlineData("\"kind\": \"sensors\"", "\"kind\": \"Sensors\"", "schema.enum", "#/definitions/1/kind")]
    [InlineData("\"kind\": \"sensors\"", "\"kind\": 2", "schema.enum", "#/definitions/1/kind")]
    [InlineData(
        "\"sensors\": { \"passiveRangeKilometers\"",
        "\"impulsePropulsion\": { \"maximumTacticalSpeedKilometersPerSecond\": 1 }, \"sensors\": { \"passiveRangeKilometers\"",
        "schema.",
        "#/definitions/1"
    )]
    [InlineData(
        "\"power\": { \"nominalDemandPowerUnits\": 40 }",
        "\"power\": { \"nominalDemandPowerUnits\": 40 }, \"sensors\": { \"passiveRangeKilometers\": 1, \"activeScanDurationMilliseconds\": 100 }",
        "schema.",
        "#/definitions/3"
    )]
    [InlineData(
        ", \"powerGeneration\": { \"nominalOutputPowerUnits\": 120 }",
        ", \"powerGeneration\": { \"nominalOutputPowerUnits\": 120 }, \"directedEnergyWeapons\": { \"rangeKilometers\": 1, \"baseNormalizedDamage\": 0.1, \"cooldownMilliseconds\": 100 }",
        "schema.",
        "#/definitions/0"
    )]
    [InlineData(
        ", \"impulsePropulsion\": { \"maximumTacticalSpeedKilometersPerSecond\": 10 }",
        "",
        "schema.required",
        "#/definitions/2"
    )]
    [InlineData(", \"actionOrder\": 300 }", " }", "schema.required", "#/definitions/1/repair")]
    [InlineData("\"actionOrder\": 300", "\"actionOrder\": -1", "schema.minimum", "#/definitions/1/repair/actionOrder")]
    [InlineData(
        "\"nominalDemandPowerUnits\": 70",
        "\"nominalDemandPowerUnits\": 0",
        "schema.minimum",
        "#/definitions/1/power/nominalDemandPowerUnits"
    )]
    [InlineData(
        "\"nominalDemandPowerUnits\": 70",
        "\"nominalDemandPowerUnits\": 1000001",
        "schema.maximum",
        "#/definitions/1/power/nominalDemandPowerUnits"
    )]
    [InlineData(
        "\"nominalDemandPowerUnits\": 70",
        "\"nominalDemandPowerUnits\": 70.5",
        "schema.type",
        "#/definitions/1/power/nominalDemandPowerUnits"
    )]
    [InlineData(
        "\"fullRepairDurationMilliseconds\": 8000, \"actionOrder\": 300",
        "\"fullRepairDurationMilliseconds\": 8050, \"actionOrder\": 300",
        "schema.multipleOf",
        "#/definitions/1/repair/fullRepairDurationMilliseconds"
    )]
    [InlineData(
        "\"activeScanDurationMilliseconds\": 2000",
        "\"activeScanDurationMilliseconds\": 0",
        "schema.minimum",
        "#/definitions/1/sensors/activeScanDurationMilliseconds"
    )]
    [InlineData(
        "\"passiveRangeKilometers\": 30",
        "\"passiveRangeKilometers\": -1",
        "schema.minimum",
        "#/definitions/1/sensors/passiveRangeKilometers"
    )]
    [InlineData(
        "\"baseNormalizedDamage\": 0.25",
        "\"baseNormalizedDamage\": 1.01",
        "schema.maximum",
        "#/definitions/4/directedEnergyWeapons/baseNormalizedDamage"
    )]
    [InlineData(
        "\"rangeKilometers\": 20",
        "\"rangeKilometers\": 0",
        "schema.exclusiveMinimum",
        "#/definitions/4/directedEnergyWeapons/rangeKilometers"
    )]
    [InlineData("\"commonOrder\": 200", "\"commonOrder\": 1000001", "schema.maximum", "#/definitions/1/commonOrder")]
    [InlineData("\"id\": \"t.sen\"", "\"id\": \"t sen\"", "schema.pattern", "#/definitions/1/id")]
    [InlineData(
        "\"componentLabel\": \"Sensors\"",
        "\"componentLabel\": \"\"",
        "schema.minLength",
        "#/definitions/1/componentLabel"
    )]
    [InlineData(
        "\"conditionParticipation\": true, \"repair\": { \"fullRepairDurationMilliseconds\": 8000, \"actionOrder\": 300 }",
        "\"conditionParticipation\": \"yes\", \"repair\": { \"fullRepairDurationMilliseconds\": 8000, \"actionOrder\": 300 }",
        "schema.type",
        "#/definitions/1/conditionParticipation"
    )]
    public void RejectsSchemaViolations(string original, string replacement, string codePrefix, string location)
    {
        ShipContentValidationException exception = AssertRejected(Mutate(original, replacement));

        Assert.Contains(
            exception.Diagnostics,
            diagnostic =>
                diagnostic.Code.StartsWith(codePrefix, StringComparison.Ordinal)
                && diagnostic.InstanceLocation.StartsWith(location, StringComparison.Ordinal)
                && string.Equals(diagnostic.SourceIdentity, "invalid.json", StringComparison.Ordinal)
        );
    }

    /// <summary>Confirms semantic capability and condition rules report their specific codes at the offending path.</summary>
    [Theory]
    [InlineData(
        ", \"powerGeneration\"",
        ", \"power\": { \"nominalDemandPowerUnits\": 5 }, \"powerGeneration\"",
        "semantic.invalid-capability",
        "#/definitions/0/power"
    )]
    [InlineData(
        ", \"powerGeneration\"",
        ", \"repair\": { \"fullRepairDurationMilliseconds\": 1000, \"actionOrder\": 0 }, \"powerGeneration\"",
        "semantic.invalid-capability",
        "#/definitions/0/repair"
    )]
    [InlineData(
        "\"power\": { \"nominalDemandPowerUnits\": 40 }",
        "\"componentLabel\": \"x\"",
        "json.duplicate-member",
        "byte:"
    )]
    [InlineData(
        ", \"power\": { \"nominalDemandPowerUnits\": 40 }",
        "",
        "semantic.invalid-capability",
        "#/definitions/3"
    )]
    [InlineData(
        ", \"power\": { \"nominalDemandPowerUnits\": 70 }",
        "",
        "semantic.invalid-capability",
        "#/definitions/1"
    )]
    [InlineData(
        "\"id\": \"t.imp\", \"kind\": \"impulse-propulsion\", \"componentLabel\": \"Impulse propulsion\", \"commonOrder\": 300, \"conditionParticipation\": true",
        "\"id\": \"t.imp\", \"kind\": \"impulse-propulsion\", \"componentLabel\": \"Impulse propulsion\", \"commonOrder\": 300, \"conditionParticipation\": false",
        "semantic.kind-requires-condition",
        "#/definitions/2/conditionParticipation"
    )]
    [InlineData(
        "\"componentLabel\": \"Sensors\"",
        "\"componentLabel\": \"   \"",
        "semantic.invalid-value",
        "#/definitions/1/componentLabel"
    )]
    [InlineData(
        "\"fullRepairDurationMilliseconds\": 8000, \"actionOrder\": 300",
        "\"fullRepairDurationMilliseconds\": 100000000000000000000, \"actionOrder\": 300",
        "semantic.invalid-value",
        "#/definitions/1/repair/fullRepairDurationMilliseconds"
    )]
    [InlineData(
        "\"passiveRangeKilometers\": 30",
        "\"passiveRangeKilometers\": 1e400",
        "semantic.invalid-value",
        "#/definitions/1/sensors/passiveRangeKilometers"
    )]
    [InlineData("\"commonOrder\": 200", "\"commonOrder\": 2e2", null, null)]
    public void RejectsSemanticViolations(string original, string replacement, string? code, string? location)
    {
        string json = Mutate(original, replacement);
        if (code is null)
        {
            // Control case: an integral exponent form is schema-valid and maps to the authored integer.
            Assert.Equal(200, Load(("integral.json", json)).GetRequired(new SystemDefinitionId("t.sen")).CommonOrder);
            return;
        }

        ShipContentValidationException exception = AssertRejected(json);
        Assert.Contains(
            exception.Diagnostics,
            diagnostic =>
                string.Equals(diagnostic.Code, code, StringComparison.Ordinal)
                && diagnostic.InstanceLocation.StartsWith(location!, StringComparison.Ordinal)
        );
    }

    /// <summary>Confirms malformed JSON, comments, trailing commas, and duplicate members fail before schema work.</summary>
    [Theory]
    [InlineData("{ \"schemaVersion\": 1, \"definitions\": [", "json.invalid")]
    [InlineData("{ \"schemaVersion\": 1, \"definitions\": [], }", "json.invalid")]
    [InlineData("{ /* note */ \"schemaVersion\": 1, \"definitions\": [] }", "json.invalid")]
    [InlineData("{ \"schemaVersion\": 1, \"schemaVersion\": 1, \"definitions\": [] }", "json.duplicate-member")]
    [InlineData("", "json.invalid")]
    public void RejectsMalformedJson(string json, string code)
    {
        ShipContentValidationException exception = AssertRejected(json);

        ShipContentDiagnostic diagnostic = Assert.Single(exception.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal("invalid.json", diagnostic.SourceIdentity);
    }

    /// <summary>Confirms nesting beyond eight containers is refused with the depth code, and eight is admitted.</summary>
    [Fact]
    public void BoundsNestingDepth()
    {
        // Root object (1) + member array (2) + seven nested arrays = nine containers.
        string tooDeep = ValidDocument.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1, \"deep\": [[[[[[[[1]]]]]]]],",
            StringComparison.Ordinal
        );
        ShipContentDiagnostic diagnostic = Assert.Single(AssertRejected(tooDeep).Diagnostics);
        Assert.Equal("json.too-deep", diagnostic.Code);

        byte[] eightDeep = Encoding.UTF8.GetBytes("{\"a\":[[[[[[[1]]]]]]]}");
        using (StrictContentJson.Parse(eightDeep, "eight.json", SystemDefinitionCatalogLoader.MaximumDepth)) { }

        byte[] nineDeep = Encoding.UTF8.GetBytes("{\"a\":[[[[[[[[1]]]]]]]]}");
        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            StrictContentJson.Parse(nineDeep, "nine.json", SystemDefinitionCatalogLoader.MaximumDepth).Dispose()
        );
        Assert.Equal("json.too-deep", Assert.Single(exception.Diagnostics).Code);
    }

    /// <summary>Confirms every content input form enforces the byte bound before parsing.</summary>
    [Fact]
    public void BoundsDocumentBytes()
    {
        string oversize = new(' ', SystemDefinitionContent.MaximumDocumentBytes + 1);
        byte[] oversizeBytes = Encoding.UTF8.GetBytes(oversize);

        Assert.Equal(
            "content.too-large",
            Assert
                .Single(
                    Assert
                        .Throws<ShipContentValidationException>(() =>
                            SystemDefinitionContent.FromText("big.json", oversize)
                        )
                        .Diagnostics
                )
                .Code
        );
        Assert.Equal(
            "content.too-large",
            Assert
                .Single(
                    Assert
                        .Throws<ShipContentValidationException>(() =>
                            SystemDefinitionContent.FromUtf8("big.json", oversizeBytes)
                        )
                        .Diagnostics
                )
                .Code
        );
        using var stream = new MemoryStream(oversizeBytes);
        Assert.Equal(
            "content.too-large",
            Assert
                .Single(
                    Assert
                        .Throws<ShipContentValidationException>(() =>
                            SystemDefinitionContent.FromStream("big.json", stream)
                        )
                        .Diagnostics
                )
                .Code
        );

        // Exactly at the bound is admitted as content (and then fails later stages on its own merits).
        SystemDefinitionContent.FromText("edge.json", new string(' ', SystemDefinitionContent.MaximumDocumentBytes));
    }

    /// <summary>Confirms the document-count bound is enforced without enumerating past the first excess document.</summary>
    [Fact]
    public void BoundsDocumentCountBeforeMaterializing()
    {
        IEnumerable<SystemDefinitionContent> documents = OverflowAfter(
            index =>
                SystemDefinitionContent.FromText(
                    "doc-" + index.ToString("D2", CultureInfo.InvariantCulture) + ".json",
                    ValidDocument
                ),
            SystemDefinitionCatalogLoader.MaximumDocuments + 1
        );

        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            CreateLoader().LoadCatalog(documents)
        );

        ShipContentDiagnostic diagnostic = Assert.Single(exception.Diagnostics);
        Assert.Equal("catalog.too-many-documents", diagnostic.Code);
        Assert.Equal("doc-16.json", diagnostic.SourceIdentity);
    }

    /// <summary>Confirms per-document (schema) and whole-catalog definition bounds.</summary>
    [Fact]
    public void BoundsDefinitionCounts()
    {
        Assert.Contains(
            AssertRejected(
                ShieldDocument("p", SystemDefinitionCatalogLoader.MaximumDefinitionsPerDocument + 1)
            ).Diagnostics,
            diagnostic =>
                string.Equals(diagnostic.Code, "schema.maxItems", StringComparison.Ordinal)
                && string.Equals(diagnostic.InstanceLocation, "#/definitions", StringComparison.Ordinal)
        );

        int half = (SystemDefinitionCatalogLoader.MaximumDefinitions / 2) + 1;
        ShipContentValidationException exception = Assert.Throws<ShipContentValidationException>(() =>
            Load(("a.json", ShieldDocument("a", half)), ("b.json", ShieldDocument("b", half)))
        );
        ShipContentDiagnostic diagnostic = Assert.Single(exception.Diagnostics);
        Assert.Equal("catalog.too-many-definitions", diagnostic.Code);
        Assert.Equal("b.json", diagnostic.SourceIdentity);

        Assert.Equal(
            SystemDefinitionCatalogLoader.MaximumDefinitions,
            Load(("a.json", ShieldDocument("a", 128)), ("b.json", ShieldDocument("b", 128))).Definitions.Count
        );
    }

    /// <summary>Confirms a repeated identity inside one document is rejected at the later occurrence.</summary>
    [Fact]
    public void RejectsDuplicateIdentityWithinDocument()
    {
        ShipContentValidationException exception = AssertRejected(Mutate("\"id\": \"t.imp\"", "\"id\": \"t.sen\""));

        ShipContentDiagnostic diagnostic = Assert.Single(exception.Diagnostics);
        Assert.Equal("catalog.duplicate-id", diagnostic.Code);
        Assert.Equal("#/definitions/2/id", diagnostic.InstanceLocation);
        Assert.Contains("'t.sen'", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Confirms a repeated identity across documents names both sources, and the reported diagnostic is the same
    /// whichever order the documents arrive in.
    /// </summary>
    [Fact]
    public void RejectsDuplicateIdentityAcrossDocumentsDeterministically()
    {
        string duplicate = LongRangeSensorDocument.Replace(
            "test.long-range-sensors",
            "t.sen",
            StringComparison.Ordinal
        );

        ShipContentValidationException forward = Assert.Throws<ShipContentValidationException>(() =>
            Load(("a.json", ValidDocument), ("z.json", duplicate))
        );
        ShipContentValidationException reverse = Assert.Throws<ShipContentValidationException>(() =>
            Load(("z.json", duplicate), ("a.json", ValidDocument))
        );

        ShipContentDiagnostic diagnostic = Assert.Single(forward.Diagnostics);
        Assert.Equal("catalog.duplicate-id", diagnostic.Code);
        Assert.Equal("z.json", diagnostic.SourceIdentity);
        Assert.Contains("'a.json'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(forward.Diagnostics, reverse.Diagnostics);
    }

    /// <summary>Confirms failures from several documents are all reported, sorted identically for any input order.</summary>
    [Fact]
    public void ReportsAllDocumentFailuresInDeterministicOrder()
    {
        string badKind = Mutate("\"kind\": \"sensors\"", "\"kind\": \"warp-core\"");
        string badCondition = Mutate(
                "\"componentLabel\": \"Shields\", \"commonOrder\": 400, \"conditionParticipation\": true",
                "\"componentLabel\": \"Shields\", \"commonOrder\": 400, \"conditionParticipation\": false"
            )
            .Replace("t.", "u.", StringComparison.Ordinal);

        ShipContentValidationException forward = Assert.Throws<ShipContentValidationException>(() =>
            Load(("m.json", badKind), ("c.json", badCondition), ("x.json", "{"))
        );
        ShipContentValidationException reverse = Assert.Throws<ShipContentValidationException>(() =>
            Load(("x.json", "{"), ("c.json", badCondition), ("m.json", badKind))
        );

        Assert.Equal(forward.Diagnostics, reverse.Diagnostics);
        Assert.Equal(
            ["c.json", "m.json", "x.json"],
            forward.Diagnostics.Select(diagnostic => diagnostic.SourceIdentity).Distinct(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        Assert.Contains(
            forward.Diagnostics,
            diagnostic => string.Equals(diagnostic.Code, "semantic.kind-requires-condition", StringComparison.Ordinal)
        );
        Assert.Contains(
            forward.Diagnostics,
            diagnostic => string.Equals(diagnostic.Code, "json.invalid", StringComparison.Ordinal)
        );
    }

    /// <summary>Confirms typed constructors refuse the combinations and values the loader reports as diagnostics.</summary>
    [Fact]
    public void TypedDefinitionsRefuseInvalidData()
    {
        var id = new SystemDefinitionId("x");
        var demand = new SystemPowerDemand(new PowerUnits(1));

        Assert.Throws<ArgumentException>(() => new ShieldSystemDefinition(id, "Shields", 0, false, null, demand));
        Assert.Throws<ArgumentNullException>(() => new ShieldSystemDefinition(id, "Shields", 0, true, null, null!));
        Assert.Throws<ArgumentException>(() => new ShieldSystemDefinition(default, "Shields", 0, true, null, demand));
        Assert.Throws<ArgumentException>(() => new ShieldSystemDefinition(id, " ", 0, true, null, demand));
        Assert.Throws<ArgumentException>(() =>
            new ShieldSystemDefinition(id, new string('l', 65), 0, true, null, demand)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShieldSystemDefinition(id, "Shields", -1, true, null, demand)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShieldSystemDefinition(id, "Shields", 1_000_001, true, null, demand)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemPowerDemand(new PowerUnits(0)));
        Assert.Throws<ArgumentException>(() => new SystemRepairCapability(new SimulationDuration(0), 0));
        Assert.Throws<ArgumentException>(() => new SystemRepairCapability(new SimulationDuration(150), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemRepairCapability(new SimulationDuration(100), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SystemRepairCapability(new SimulationDuration(100), 1_000_001)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PowerGenerationSystemDefinition(id, "Gen", 0, true, new PowerUnits(0))
        );
        Assert.Throws<ArgumentException>(() =>
            new SensorSystemDefinition(
                id,
                "S",
                0,
                true,
                null,
                demand,
                new DistanceKilometers(1),
                new SimulationDuration(150)
            )
        );
        Assert.Throws<ArgumentNullException>(() =>
            new DirectedEnergyWeaponSystemDefinition(id, "W", 0, true, null, demand, null!)
        );
    }

    /// <summary>Confirms catalog collections cannot be mutated through their interfaces.</summary>
    [Fact]
    public void CatalogCollectionsAreImmutable()
    {
        SystemDefinitionCatalog catalog = LoadProduction();

        Assert.Throws<NotSupportedException>(() => ((IList<SystemDefinition>)catalog.Definitions).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ShipSystemKind>)catalog.DamageTargetKinds).Add(ShipSystemKind.Shields)
        );
    }

    private static string Mutate(string original, string replacement)
    {
        Assert.Contains(original, ValidDocument, StringComparison.Ordinal);
        int first = ValidDocument.IndexOf(original, StringComparison.Ordinal);
        Assert.Equal(first, ValidDocument.LastIndexOf(original, StringComparison.Ordinal));
        return ValidDocument.Replace(original, replacement, StringComparison.Ordinal);
    }

    private static string BuildDocument(IEnumerable<string> definitionLines) =>
        "{ \"schemaVersion\": 1, \"definitions\": [\n"
        + string.Join(",\n", definitionLines.Select(line => line.TrimEnd().TrimEnd(',')))
        + "\n] }";

    private static string ShieldDocument(string prefix, int count) =>
        "{ \"schemaVersion\": 1, \"definitions\": ["
        + string.Join(
            ",",
            Enumerable
                .Range(0, count)
                .Select(index =>
                    $"{{ \"id\": \"{prefix}.{index}\", \"kind\": \"shields\", \"componentLabel\": \"S\", \"commonOrder\": {index}, \"conditionParticipation\": true, \"power\": {{ \"nominalDemandPowerUnits\": 1 }} }}"
                )
        )
        + "] }";

    private static ShipContentValidationException AssertRejected(string json) =>
        Assert.Throws<ShipContentValidationException>(() => Load(("invalid.json", json)));

    private static SystemDefinitionCatalog Load(params (string Source, string Json)[] documents) =>
        CreateLoader()
            .LoadCatalog(
                documents.Select(document => SystemDefinitionContent.FromText(document.Source, document.Json))
            );

    private static SystemDefinitionCatalog LoadProduction() => Load((ProductionSource, ReadProductionText()));

    private static string ReadProductionText() =>
        File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "src/AlterCourse.Godot/content/systems/pathfinder-systems.json")
        );

    private static SystemDefinitionCatalogLoader CreateLoader() =>
        new(
            File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    "src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json"
                )
            )
        );

    private static IEnumerable<T> OverflowAfter<T>(Func<int, T> factory, int yieldedCount)
    {
        for (int index = 0; index < yieldedCount; index++)
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
