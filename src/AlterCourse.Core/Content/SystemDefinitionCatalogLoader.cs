using System.Globalization;
using System.Text.Json;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using Json.Schema;

namespace AlterCourse.Core.Content;

/// <summary>
/// Strictly validates system-definition V1 documents and builds one <see cref="SystemDefinitionCatalog"/>.
/// </summary>
/// <remarks>
/// <para>
/// Admission runs in fixed stages per document: byte bound (at <see cref="SystemDefinitionContent"/> creation),
/// strict parse (duplicate members, depth, no comments or trailing commas), JSON Schema, then typed semantic
/// validation. The schema owns shape (closed objects, the kind enum, which specialized block each kind requires or
/// forbids); semantic validation owns capability combinations and condition participation, so those failures carry
/// the specific <c>semantic.*</c> codes rather than generic schema codes.
/// </para>
/// <para>
/// Every document is evaluated and all diagnostics are reported together in sorted order, and duplicate
/// identities name their earliest occurrence by (source identity, position). The outcome and the reported
/// diagnostics are therefore independent of the order documents are supplied in.
/// </para>
/// </remarks>
public sealed class SystemDefinitionCatalogLoader
{
    /// <summary>Gets the maximum number of documents one catalog load accepts.</summary>
    public const int MaximumDocuments = 16;

    /// <summary>Gets the maximum definitions per document; the schema's <c>definitions.maxItems</c> repeats it.</summary>
    public const int MaximumDefinitionsPerDocument = 256;

    /// <summary>Gets the maximum definitions across all documents of one catalog.</summary>
    public const int MaximumDefinitions = 256;

    /// <summary>
    /// Gets the maximum JSON container nesting. Valid V1 content nests four levels (root, definitions array,
    /// definition, capability block), so eight leaves headroom while bounding parser work.
    /// </summary>
    public const int MaximumDepth = 8;

    private const string PowerGenerationKind = "power-generation";
    private const string SensorsKind = "sensors";
    private const string ImpulsePropulsionKind = "impulse-propulsion";
    private const string ShieldsKind = "shields";
    private const string DirectedEnergyWeaponsKind = "directed-energy-weapons";

    private static readonly Uri SchemaBaseUri = new(
        "https://l3digital.net/star-trek-alter-course/schemas/system-definition-v1.schema.json"
    );

    private readonly JsonSchema _schema;

    /// <summary>Initializes the loader from the canonical system-definition V1 JSON Schema text.</summary>
    public SystemDefinitionCatalogLoader(string schemaText)
    {
        ArgumentNullException.ThrowIfNull(schemaText);
        _schema = JsonSchema.FromText(
            schemaText,
            new BuildOptions { SchemaRegistry = new SchemaRegistry() },
            SchemaBaseUri
        );
    }

    /// <summary>Loads every document into one catalog, or throws with every diagnostic found.</summary>
    /// <exception cref="ShipContentValidationException">Any document or the combined catalog is invalid.</exception>
    public SystemDefinitionCatalog LoadCatalog(IEnumerable<SystemDefinitionContent> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        SystemDefinitionContent[] documents = content.Take(MaximumDocuments + 1).ToArray();
        if (documents.Length > MaximumDocuments)
        {
            throw StrictContentJson.Failure(
                "catalog.too-many-documents",
                documents[MaximumDocuments].SourceIdentity,
                "#",
                $"A system definition catalog supports at most {MaximumDocuments} documents."
            );
        }

        var diagnostics = new List<ShipContentDiagnostic>();
        var loaded = new List<LoadedDefinition>();
        int authoredCount = 0;
        foreach (SystemDefinitionContent document in documents)
        {
            ArgumentNullException.ThrowIfNull(document, nameof(content));
            authoredCount += LoadDocument(document, loaded, diagnostics);
        }

        if (authoredCount > MaximumDefinitions)
        {
            // Attributed to the ordinal-last source so the diagnostic does not depend on input order.
            string source = documents.Select(document => document.SourceIdentity).Max(StringComparer.Ordinal)!;
            diagnostics.Add(
                new ShipContentDiagnostic(
                    "catalog.too-many-definitions",
                    source,
                    "#/definitions",
                    string.Empty,
                    $"The catalog declares {authoredCount} definitions; at most {MaximumDefinitions} are supported."
                )
            );
        }

        ReportDuplicateIdentities(loaded, diagnostics);
        if (diagnostics.Count > 0)
        {
            throw new ShipContentValidationException(StrictContentJson.Sort(diagnostics));
        }

        return new SystemDefinitionCatalog(loaded.Select(entry => entry.Definition));
    }

    private int LoadDocument(
        SystemDefinitionContent document,
        List<LoadedDefinition> loaded,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        JsonDocument parsed;
        try
        {
            parsed = StrictContentJson.Parse(document.Utf8Json, document.SourceIdentity, MaximumDepth);
        }
        catch (ShipContentValidationException exception)
        {
            diagnostics.AddRange(exception.Diagnostics);
            return 0;
        }

        using (parsed)
        {
            IReadOnlyList<ShipContentDiagnostic> schemaDiagnostics = StrictContentJson.EvaluateSchema(
                _schema,
                parsed.RootElement,
                document.SourceIdentity
            );
            if (schemaDiagnostics.Count > 0)
            {
                diagnostics.AddRange(schemaDiagnostics);
                return 0;
            }

            // The schema has proven the shape (required members, types, closed objects), so the reads below
            // cannot miss a member; only numeric representability and cross-field rules remain to check.
            JsonElement definitions = parsed.RootElement.GetProperty("definitions");
            int index = 0;
            foreach (JsonElement element in definitions.EnumerateArray())
            {
                string pointer = "#/definitions/" + index.ToString(CultureInfo.InvariantCulture);
                SystemDefinition? definition = ReadDefinition(element, pointer, document.SourceIdentity, diagnostics);
                if (definition is not null)
                {
                    loaded.Add(new LoadedDefinition(definition, document.SourceIdentity, index));
                }

                index++;
            }

            return index;
        }
    }

    private static void ReportDuplicateIdentities(
        List<LoadedDefinition> loaded,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        foreach (IGrouping<SystemDefinitionId, LoadedDefinition> group in loaded.GroupBy(entry => entry.Definition.Id))
        {
            LoadedDefinition[] occurrences = group
                .OrderBy(entry => entry.SourceIdentity, StringComparer.Ordinal)
                .ThenBy(entry => entry.Index)
                .ToArray();
            LoadedDefinition first = occurrences[0];
            foreach (LoadedDefinition repeat in occurrences.Skip(1))
            {
                diagnostics.Add(
                    new ShipContentDiagnostic(
                        "catalog.duplicate-id",
                        repeat.SourceIdentity,
                        $"#/definitions/{repeat.Index.ToString(CultureInfo.InvariantCulture)}/id",
                        string.Empty,
                        $"System definition identity '{group.Key.Value}' duplicates the definition at "
                            + $"'{first.SourceIdentity}' #/definitions/{first.Index.ToString(CultureInfo.InvariantCulture)}."
                    )
                );
            }
        }
    }

    private static SystemDefinition? ReadDefinition(
        JsonElement element,
        string pointer,
        string source,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        int before = diagnostics.Count;
        string kind = element.GetProperty("kind").GetString()!;
        CommonFields common = ReadCommon(element, kind, pointer, source, diagnostics);
        ValidateCapabilities(element, kind, pointer, source, diagnostics);
        if (diagnostics.Count > before)
        {
            return null;
        }

        return kind switch
        {
            PowerGenerationKind => ReadPowerGeneration(element, common, pointer, source, diagnostics),
            SensorsKind => ReadSensors(element, common, pointer, source, diagnostics),
            ImpulsePropulsionKind => ReadImpulse(element, common, pointer, source, diagnostics),
            ShieldsKind => new ShieldSystemDefinition(
                common.Id,
                common.Label,
                common.CommonOrder,
                true,
                common.Repair,
                common.Power!
            ),
            DirectedEnergyWeaponsKind => ReadDirectedEnergy(element, common, pointer, source, diagnostics),
            // The schema's kind enum makes this unreachable; failing loudly keeps a schema/loader drift visible.
            _ => throw new InvalidOperationException($"Schema admitted unsupported system kind '{kind}'."),
        };
    }

    /// <summary>
    /// Reads the kind-independent fields. The returned values are meaningful only when no diagnostic was added;
    /// callers compare the diagnostic count before constructing anything.
    /// </summary>
    private static CommonFields ReadCommon(
        JsonElement element,
        string kind,
        string pointer,
        string source,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        string idText = element.GetProperty("id").GetString()!;
        string label = element.GetProperty("componentLabel").GetString()!;
        if (string.IsNullOrWhiteSpace(label))
        {
            diagnostics.Add(
                StrictContentJson.Semantic(
                    source,
                    pointer + "/componentLabel",
                    "Component label must contain non-whitespace text."
                )
            );
        }

        if (!element.GetProperty("conditionParticipation").GetBoolean())
        {
            diagnostics.Add(
                new ShipContentDiagnostic(
                    "semantic.kind-requires-condition",
                    source,
                    pointer + "/conditionParticipation",
                    string.Empty,
                    $"Kind '{kind}' participates in condition and damage; conditionParticipation must be true."
                )
            );
        }

        if (
            StrictContentJson.TryReadInt32(
                element.GetProperty("commonOrder"),
                pointer + "/commonOrder",
                source,
                diagnostics,
                out int commonOrder
            ) && commonOrder is < 0 or > SystemDefinition.MaximumCommonOrder
        )
        {
            diagnostics.Add(
                StrictContentJson.Semantic(
                    source,
                    pointer + "/commonOrder",
                    $"Common order must be from 0 through {SystemDefinition.MaximumCommonOrder}."
                )
            );
        }

        // The schema pattern and length already match SystemDefinitionId, so construction cannot fail here.
        return new CommonFields(
            new SystemDefinitionId(idText),
            label,
            commonOrder,
            ReadRepair(element, pointer, source, diagnostics),
            ReadPower(element, pointer, source, diagnostics)
        );
    }

    private static void ValidateCapabilities(
        JsonElement element,
        string kind,
        string pointer,
        string source,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        bool hasPower = element.TryGetProperty("power", out _);
        bool hasRepair = element.TryGetProperty("repair", out _);
        if (string.Equals(kind, PowerGenerationKind, StringComparison.Ordinal))
        {
            // Generation is nonrepairable, and a generator drawing an allocation from its own supply is
            // meaningless; either block would silently change power accounting or repair legality.
            if (hasPower)
            {
                diagnostics.Add(
                    Capability(source, pointer + "/power", "A power-generation definition cannot declare power demand.")
                );
            }

            if (hasRepair)
            {
                diagnostics.Add(
                    Capability(source, pointer + "/repair", "A power-generation definition cannot be repairable.")
                );
            }
        }
        else if (!hasPower)
        {
            diagnostics.Add(Capability(source, pointer, $"A '{kind}' definition must declare power demand."));
        }
    }

    private static SystemRepairCapability? ReadRepair(
        JsonElement element,
        string pointer,
        string source,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        if (!element.TryGetProperty("repair", out JsonElement repair))
        {
            return null;
        }

        string location = pointer + "/repair";
        bool durationRead = TryReadAlignedDuration(
            repair.GetProperty("fullRepairDurationMilliseconds"),
            location + "/fullRepairDurationMilliseconds",
            source,
            diagnostics,
            out SimulationDuration duration
        );
        bool orderRead = StrictContentJson.TryReadInt32(
            repair.GetProperty("actionOrder"),
            location + "/actionOrder",
            source,
            diagnostics,
            out int actionOrder
        );
        if (orderRead && actionOrder is < 0 or > SystemRepairCapability.MaximumActionOrder)
        {
            diagnostics.Add(
                StrictContentJson.Semantic(
                    source,
                    location + "/actionOrder",
                    $"Repair action order must be from 0 through {SystemRepairCapability.MaximumActionOrder}."
                )
            );
            return null;
        }

        return durationRead && orderRead ? new SystemRepairCapability(duration, actionOrder) : null;
    }

    private static SystemPowerDemand? ReadPower(
        JsonElement element,
        string pointer,
        string source,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        if (!element.TryGetProperty("power", out JsonElement power))
        {
            return null;
        }

        return TryReadPositivePower(
            power.GetProperty("nominalDemandPowerUnits"),
            pointer + "/power/nominalDemandPowerUnits",
            source,
            diagnostics,
            out PowerUnits demand
        )
            ? new SystemPowerDemand(demand)
            : null;
    }

    private static PowerGenerationSystemDefinition? ReadPowerGeneration(
        JsonElement element,
        CommonFields common,
        string pointer,
        string source,
        List<ShipContentDiagnostic> diagnostics
    ) =>
        TryReadPositivePower(
            element.GetProperty("powerGeneration").GetProperty("nominalOutputPowerUnits"),
            pointer + "/powerGeneration/nominalOutputPowerUnits",
            source,
            diagnostics,
            out PowerUnits output
        )
            ? new PowerGenerationSystemDefinition(common.Id, common.Label, common.CommonOrder, true, output)
            : null;

    private static SensorSystemDefinition? ReadSensors(
        JsonElement element,
        CommonFields common,
        string pointer,
        string source,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        JsonElement sensors = element.GetProperty("sensors");
        bool rangeRead = TryReadNonnegative(
            sensors.GetProperty("passiveRangeKilometers"),
            pointer + "/sensors/passiveRangeKilometers",
            source,
            diagnostics,
            out double range
        );
        bool scanRead = TryReadAlignedDuration(
            sensors.GetProperty("activeScanDurationMilliseconds"),
            pointer + "/sensors/activeScanDurationMilliseconds",
            source,
            diagnostics,
            out SimulationDuration scan
        );
        return rangeRead && scanRead
            ? new SensorSystemDefinition(
                common.Id,
                common.Label,
                common.CommonOrder,
                true,
                common.Repair,
                common.Power!,
                new DistanceKilometers(range),
                scan
            )
            : null;
    }

    private static ImpulsePropulsionSystemDefinition? ReadImpulse(
        JsonElement element,
        CommonFields common,
        string pointer,
        string source,
        List<ShipContentDiagnostic> diagnostics
    ) =>
        TryReadNonnegative(
            element.GetProperty("impulsePropulsion").GetProperty("maximumTacticalSpeedKilometersPerSecond"),
            pointer + "/impulsePropulsion/maximumTacticalSpeedKilometersPerSecond",
            source,
            diagnostics,
            out double speed
        )
            ? new ImpulsePropulsionSystemDefinition(
                common.Id,
                common.Label,
                common.CommonOrder,
                true,
                common.Repair,
                common.Power!,
                new SpeedKilometersPerSecond(speed)
            )
            : null;

    private static DirectedEnergyWeaponSystemDefinition? ReadDirectedEnergy(
        JsonElement element,
        CommonFields common,
        string pointer,
        string source,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        JsonElement weapon = element.GetProperty("directedEnergyWeapons");
        string location = pointer + "/directedEnergyWeapons";
        int before = diagnostics.Count;
        if (
            StrictContentJson.TryReadFiniteDouble(
                weapon.GetProperty("rangeKilometers"),
                location + "/rangeKilometers",
                source,
                diagnostics,
                out double range
            )
            && range <= 0
        )
        {
            diagnostics.Add(
                StrictContentJson.Semantic(source, location + "/rangeKilometers", "Weapon range must be positive.")
            );
        }

        if (
            StrictContentJson.TryReadFiniteDouble(
                weapon.GetProperty("baseNormalizedDamage"),
                location + "/baseNormalizedDamage",
                source,
                diagnostics,
                out double damage
            ) && damage is <= 0 or > 1
        )
        {
            diagnostics.Add(
                StrictContentJson.Semantic(
                    source,
                    location + "/baseNormalizedDamage",
                    "Weapon damage must be positive and normalized."
                )
            );
        }

        TryReadAlignedDuration(
            weapon.GetProperty("cooldownMilliseconds"),
            location + "/cooldownMilliseconds",
            source,
            diagnostics,
            out SimulationDuration cooldown
        );
        return diagnostics.Count > before
            ? null
            : new DirectedEnergyWeaponSystemDefinition(
                common.Id,
                common.Label,
                common.CommonOrder,
                true,
                common.Repair,
                common.Power!,
                new DirectedEnergyWeaponDefinition(new DistanceKilometers(range), damage, cooldown)
            );
    }

    private static bool TryReadAlignedDuration(
        JsonElement value,
        string location,
        string source,
        List<ShipContentDiagnostic> diagnostics,
        out SimulationDuration duration
    )
    {
        duration = default;
        if (!StrictContentJson.TryReadInt64(value, location, source, diagnostics, out long milliseconds))
        {
            return false;
        }

        if (milliseconds <= 0 || milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            diagnostics.Add(
                StrictContentJson.Semantic(
                    source,
                    location,
                    "Duration must be positive and align to the 100 millisecond simulation step."
                )
            );
            return false;
        }

        duration = new SimulationDuration(milliseconds);
        return true;
    }

    private static bool TryReadPositivePower(
        JsonElement value,
        string location,
        string source,
        List<ShipContentDiagnostic> diagnostics,
        out PowerUnits power
    )
    {
        power = default;
        if (!StrictContentJson.TryReadInt32(value, location, source, diagnostics, out int units))
        {
            return false;
        }

        if (units is <= 0 or > PowerUnits.MaximumValue)
        {
            diagnostics.Add(
                StrictContentJson.Semantic(
                    source,
                    location,
                    $"Power must be positive and no greater than {PowerUnits.MaximumValue}."
                )
            );
            return false;
        }

        power = new PowerUnits(units);
        return true;
    }

    private static bool TryReadNonnegative(
        JsonElement value,
        string location,
        string source,
        List<ShipContentDiagnostic> diagnostics,
        out double result
    )
    {
        if (!StrictContentJson.TryReadFiniteDouble(value, location, source, diagnostics, out result))
        {
            return false;
        }

        if (result < 0)
        {
            diagnostics.Add(StrictContentJson.Semantic(source, location, "Value must be finite and nonnegative."));
            return false;
        }

        return true;
    }

    private static ShipContentDiagnostic Capability(string source, string location, string message) =>
        new("semantic.invalid-capability", source, location, string.Empty, message);

    private sealed record LoadedDefinition(SystemDefinition Definition, string SourceIdentity, int Index);

    private sealed record CommonFields(
        SystemDefinitionId Id,
        string Label,
        int CommonOrder,
        SystemRepairCapability? Repair,
        SystemPowerDemand? Power
    );
}
