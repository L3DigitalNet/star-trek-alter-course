using System.Text.Json;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;
using Microsoft.Extensions.Logging;
using SaveEnvelopeV10 = AlterCourse.Core.Persistence.SaveModelsV10.SaveEnvelopeV10;
using SaveEnvelopeV9 = AlterCourse.Core.Persistence.SaveModelsV9.SaveEnvelopeV9;
using SaveMetadataV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveMetadataV2;
using ShipSnapshotV10 = AlterCourse.Core.Persistence.SaveModelsV10.ShipSnapshotV10;
using ShipSnapshotV9 = AlterCourse.Core.Persistence.SaveModelsV9.ShipSnapshotV9;
using SimulationSnapshotV10 = AlterCourse.Core.Persistence.SaveModelsV10.SimulationSnapshotV10;
using TacticalMotionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalMotionSnapshotV2;
using TacticalPositionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalPositionSnapshotV2;

namespace AlterCourse.Core.Persistence;

/// <summary>
/// V10 persistence: direct capture of installed ship-system state, strict V10 loading, and the adjacent V9→V10
/// migration through the frozen <see cref="HistoricalShipSystemsV9"/> map.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RestoreV10"/> is the only save-DTO→runtime constructor. Every supported version reaches it through
/// the adjacent chain (…→V9→<see cref="MigrateV9ToV10"/>) and every candidate passes <see cref="ValidateCandidateV10"/>
/// first, so there is one runtime reconstruction path and one set of V10 rules. Load builds a new aggregate and
/// returns it only on success; no live simulation is referenced, which is what makes a failed load atomic.
/// </para>
/// <para>
/// Content is consulted in exactly two places, both compatibility checks that fail closed with
/// <see cref="GamePersistenceFailure.IncompatibleContent"/>: V10 descriptor comparison and the V9→V10 map
/// verification. No load reads a ship design's default loadout, so a compatible class-default change never alters a
/// saved installation.
/// </para>
/// </remarks>
public static partial class GamePersistence
{
    private const int V10SchemaVersion = 10;

    // Godot's gameplay shell asserts this literal and V10SchemaVersion from the written save in
    // src/AlterCourse.Godot/tests/GameplayShellTest.gd, so changing either requires updating that end.
    private const string V10SimulationRulesVersion = "installed-ship-system-substrate-v1";

    /// <summary>The largest V10 definition-reference table: one row per distinct referenced definition.</summary>
    internal const int MaximumSystemDefinitionReferences = 256;

    private static SaveEnvelopeV10 CaptureV10(
        SimulationState state,
        ShipDefinitionCatalog catalog,
        GameSaveMetadata metadata
    )
    {
        if (
            state.Ships.Length > SimulationState.MaximumShips
            || state.Factions.Length > SimulationState.MaximumFactions
        )
        {
            throw new InvalidOperationException(
                "V10 persistence supports at most the simulation ship and faction limits."
            );
        }

        SaveModelsV10.SystemDefinitionReferenceSnapshotV10[] definitions =
        [
            .. state
                .Ships.SelectMany(ship => ship.Engineering.Systems.ByIdentity)
                .Select(system => system.Definition)
                .DistinctBy(definition => definition.Id.Value, StringComparer.Ordinal)
                .OrderBy(definition => definition.Id.Value, StringComparer.Ordinal)
                .Select(definition => new SaveModelsV10.SystemDefinitionReferenceSnapshotV10
                {
                    DefinitionId = definition.Id.Value,
                    Semantics = SystemDefinitionSemantics.Describe(definition),
                }),
        ];
        return new SaveEnvelopeV10
        {
            SchemaVersion = V10SchemaVersion,
            SimulationRulesVersion = V10SimulationRulesVersion,
            Metadata = new SaveMetadataV2
            {
                SaveId = metadata.SaveId,
                DisplayName = metadata.DisplayName,
                CreatedAtUtc = metadata.CreatedAtUtc,
                SavedAtUtc = metadata.SavedAtUtc,
            },
            Simulation = new SimulationSnapshotV10
            {
                TimeMilliseconds = state.Time.Milliseconds,
                ShipAllocatorNextId = state.ShipIdAllocator.NextId,
                OrderAllocatorNextId = state.OrderIdAllocator.NextId,
                ObservationReportAllocatorNextId = state.ObservationReportIdAllocator.NextId,
                PlayerShipId = state.PlayerShipId.Value,
                Scheduler = CaptureSchedulerV7(state.Scheduler),
                StrategicMap = CaptureStrategicMapV2(state.StrategicMap),
                SystemDefinitions = definitions,
                AimVocabulary = AimVocabularySemantics.Describe(catalog.SystemDefinitions.DamageTargetKinds),
                Ships = [.. state.Ships.OrderBy(ship => ship.InstanceId.Value).Select(CaptureShipV10)],
                Factions = [.. state.Factions.OrderBy(faction => faction.Id.Value).Select(CaptureFactionV8)],
            },
        };
    }

    private static ShipSnapshotV10 CaptureShipV10(ShipState ship)
    {
        return new ShipSnapshotV10
        {
            InstanceId = ship.InstanceId.Value,
            DefinitionId = ship.DefinitionId.Value,
            DisplayName = ship.VesselDisplayName,
            TacticalPosition = new TacticalPositionSnapshotV2
            {
                XKilometers = ship.TacticalPosition.XKilometers,
                YKilometers = ship.TacticalPosition.YKilometers,
            },
            TacticalMotion = new TacticalMotionSnapshotV2
            {
                HeadingDegrees = ship.TacticalMotion.Heading.Value,
                SpeedKilometersPerSecond = ship.TacticalMotion.Speed.Value,
            },
            Engineering = CaptureEngineeringV10(ship.Engineering),
            StrategicState = CaptureStrategicStateV2(ship.StrategicState),
            ActiveOrder = CaptureOrderV3(ship.ActiveOrder),
            SensorKnowledge = CaptureSensorKnowledgeV10(ship.SensorKnowledge),
            AutonomousState = CaptureAutonomousStateV4(ship.AutonomousState),
            DirectControllerFactionId = ship.DirectControllerFactionId?.Value,
            Combat = CaptureCombatV10(ship.Combat),
        };
    }

    private static SaveModelsV10.EngineeringSnapshotV10 CaptureEngineeringV10(ShipEngineeringState engineering)
    {
        SystemRepairState? repair = engineering.ActiveRepair;
        return new SaveModelsV10.EngineeringSnapshotV10
        {
            NextInstalledSystemId = engineering.InstallationIds.NextId,
            InstalledSystems =
            [
                .. engineering.Systems.ByIdentity.Select(system => new SaveModelsV10.InstalledSystemSnapshotV10
                {
                    InstalledSystemId = system.Id.Value,
                    DefinitionId = system.Definition.Id.Value,
                    Condition = system.Condition.Value,
                    Allocation = system.Allocation?.Value,
                }),
            ],
            ActiveRepair = repair is null
                ? null
                : new SaveModelsV10.SystemRepairSnapshotV10
                {
                    TargetInstalledSystemId = repair.Target.Value,
                    StartingCondition = repair.StartingCondition.Value,
                    TargetCondition = repair.TargetCondition.Value,
                    StartedAtMilliseconds = repair.StartedAt.Milliseconds,
                    ExpectedCompletionMilliseconds = repair.ExpectedCompletion.Milliseconds,
                    ScheduledCompletionId = repair.ScheduledCompletionId.Value,
                },
        };
    }

    private static SaveModelsV10.SensorKnowledgeSnapshotV10 CaptureSensorKnowledgeV10(SensorKnowledge knowledge)
    {
        ActiveSensorScanState? scan = knowledge.ActiveScan;
        return new SaveModelsV10.SensorKnowledgeSnapshotV10
        {
            NextContactId = knowledge.NextContactId,
            Contacts = [.. knowledge.Contacts.Select(CaptureContactV6)],
            ActiveScan = scan is null
                ? null
                : new SaveModelsV10.ActiveSensorScanSnapshotV10
                {
                    TargetContactId = scan.TargetContactId.Value,
                    SensorInstalledSystemId = scan.Sensor.Value,
                    StartedAtMilliseconds = scan.StartedAt.Milliseconds,
                    ExpectedCompletionMilliseconds = scan.ExpectedCompletion.Milliseconds,
                    ScheduledCompletionId = scan.ScheduledCompletionId.Value,
                },
        };
    }

    private static SaveModelsV10.ShipCombatSnapshotV10 CaptureCombatV10(ShipCombatState combat)
    {
        CombatStimulus? stimulus = combat.PendingStimulus;
        return new SaveModelsV10.ShipCombatSnapshotV10
        {
            DirectedEnergyReadiness =
            [
                .. combat
                    .WeaponReadiness.OrderBy(entry => entry.Weapon.Value)
                    .Select(entry => new SaveModelsV10.DirectedEnergyReadinessSnapshotV10
                    {
                        WeaponInstalledSystemId = entry.Weapon.Value,
                        ReadyAtMilliseconds = entry.ReadyAt.Milliseconds,
                    }),
            ],
            PendingStimulus = stimulus is null
                ? null
                : new SaveModelsV9.CombatStimulusSnapshotV9
                {
                    ContactId = stimulus.ContactId.Value,
                    ObservedAtMilliseconds = stimulus.ObservedAt.Milliseconds,
                    DueTimeMilliseconds = stimulus.DueTime.Milliseconds,
                    ScheduledWorkId = stimulus.ScheduledWorkId.Value,
                },
        };
    }

    private static LoadedGameSave LoadV10(
        byte[] json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity,
        ILogger<GameSimulation>? logger
    )
    {
        try
        {
            SaveEnvelopeV10 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV10>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            ValidateCandidateV10(envelope, catalog.SystemDefinitions, "V10");
            return RestoreV10(envelope, catalog, factionCatalog, logger);
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or OverflowException
            )
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"violates the V10 semantic contract: {exception.Message}",
                exception
            );
        }
    }

    /// <summary>
    /// Validates the V9 candidate, migrates it to V10, and restores it through the single V10 path. Every V1–V9
    /// document ends here after its own adjacent chain.
    /// </summary>
    private static LoadedGameSave RestoreThroughV10(
        SaveEnvelopeV9 envelope,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        ILogger<GameSimulation>? logger
    )
    {
        ValidateCandidateV9(envelope, catalog);
        SaveEnvelopeV10 migrated = MigrateV9ToV10(envelope, catalog.SystemDefinitions);
        ValidateCandidateV10(migrated, catalog.SystemDefinitions, "migrated V9");
        return RestoreV10(migrated, catalog, factionCatalog, logger);
    }

    /// <summary>
    /// Converts a validated V9 envelope into V10 through the frozen version-qualified map, inventing and dropping
    /// nothing.
    /// </summary>
    /// <remarks>
    /// Each V9 fixed field becomes the installation the map names: the five conditions verbatim — zero stays an
    /// installed-but-offline system, never absence — the four allocations verbatim (generation is a nonconsumer),
    /// the repair target and scan source through the same map, and the single readiness time attached to the mapped
    /// weapon. Everything else (clock, allocators, scheduler identities and order, strategic, order, contact,
    /// autonomous, stimulus, and faction state) is copied. The output labels are the V10 constants written literally
    /// here, never the mutable current aliases, so a later schema cannot relabel this migration's output.
    /// </remarks>
    /// <exception cref="SaveContentIncompatibleException">
    /// A ship definition has no V9 mapping, a mapped definition is absent or changed meaning, or the catalog's aim
    /// vocabulary differs from the one V9 play used.
    /// </exception>
    private static SaveEnvelopeV10 MigrateV9ToV10(SaveEnvelopeV9 envelope, SystemDefinitionCatalog systems)
    {
        SaveModelsV9.SimulationSnapshotV9 source = envelope.Simulation;
        SortedDictionary<string, string> referenced = VerifyHistoricalDefinitionsV9(source.Ships, systems);
        string currentVocabulary = AimVocabularySemantics.Describe(systems.DamageTargetKinds);
        if (!string.Equals(currentVocabulary, AimVocabularySemantics.HistoricalV9, StringComparison.Ordinal))
        {
            throw AimVocabularyMismatch("V9", AimVocabularySemantics.HistoricalV9, currentVocabulary);
        }

        return new SaveEnvelopeV10
        {
            SchemaVersion = V10SchemaVersion,
            SimulationRulesVersion = V10SimulationRulesVersion,
            Metadata = envelope.Metadata,
            Simulation = new SimulationSnapshotV10
            {
                TimeMilliseconds = source.TimeMilliseconds,
                ShipAllocatorNextId = source.ShipAllocatorNextId,
                OrderAllocatorNextId = source.OrderAllocatorNextId,
                ObservationReportAllocatorNextId = source.ObservationReportAllocatorNextId,
                PlayerShipId = source.PlayerShipId,
                Scheduler = source.Scheduler,
                StrategicMap = source.StrategicMap,
                SystemDefinitions =
                [
                    .. referenced.Select(pair => new SaveModelsV10.SystemDefinitionReferenceSnapshotV10
                    {
                        DefinitionId = pair.Key,
                        Semantics = pair.Value,
                    }),
                ],
                AimVocabulary = AimVocabularySemantics.HistoricalV9,
                // V9 accepted ships in any order (the runtime sorts them); V10 requires canonical ascending order, and
                // reordering invents and drops nothing.
                Ships = [.. source.Ships.OrderBy(ship => ship.InstanceId).Select(MigrateShipV9)],
                Factions = source.Factions,
            },
        };
    }

    /// <summary>
    /// Resolves every V9 ship through the frozen map and verifies each mapped definition against the supplied content,
    /// returning the verified descriptors keyed (ordinally) by definition id.
    /// </summary>
    private static SortedDictionary<string, string> VerifyHistoricalDefinitionsV9(
        ShipSnapshotV9[] ships,
        SystemDefinitionCatalog systems
    )
    {
        var referenced = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (ShipSnapshotV9 ship in ships)
        {
            if (
                !HistoricalShipSystemsV9.TryGetRows(
                    ship.DefinitionId,
                    out IReadOnlyList<HistoricalShipSystemsV9.Row>? rows
                )
            )
            {
                throw new SaveContentIncompatibleException(
                    $"is a V9 save whose ship {ship.InstanceId} uses ship definition '{ship.DefinitionId}', which has "
                        + "no V10 installed-system mapping; load it with a build that supports it or start a new game."
                );
            }

            foreach (HistoricalShipSystemsV9.Row row in rows)
            {
                referenced[row.DefinitionId] = VerifyDefinitionSemantics(
                    systems,
                    row.DefinitionId,
                    HistoricalShipSystemsV9.ExpectedSemanticsFor(ship.DefinitionId, row.Kind),
                    $"V9 ship {ship.InstanceId} ('{ship.DefinitionId}' {row.Kind})"
                );
            }
        }

        return referenced;
    }

    private static ShipSnapshotV10 MigrateShipV9(ShipSnapshotV9 ship) =>
        new()
        {
            InstanceId = ship.InstanceId,
            DefinitionId = ship.DefinitionId,
            DisplayName = ship.DisplayName,
            TacticalPosition = ship.TacticalPosition,
            TacticalMotion = ship.TacticalMotion,
            Engineering = MigrateEngineeringV9(ship.DefinitionId, ship.Engineering),
            StrategicState = ship.StrategicState,
            ActiveOrder = ship.ActiveOrder,
            SensorKnowledge = MigrateSensorKnowledgeV9(ship.DefinitionId, ship.SensorKnowledge),
            AutonomousState = ship.AutonomousState,
            DirectControllerFactionId = ship.DirectControllerFactionId,
            Combat = new SaveModelsV10.ShipCombatSnapshotV10
            {
                DirectedEnergyReadiness =
                [
                    new SaveModelsV10.DirectedEnergyReadinessSnapshotV10
                    {
                        WeaponInstalledSystemId = HistoricalShipSystemsV9
                            .InstalledIdFor(ship.DefinitionId, WeaponsKind)
                            .Value,
                        ReadyAtMilliseconds = ship.Combat.NextDirectedEnergyReadyAtMilliseconds,
                    },
                ],
                PendingStimulus = ship.Combat.PendingStimulus,
            },
        };

    private static SaveModelsV10.EngineeringSnapshotV10 MigrateEngineeringV9(
        string design,
        SaveModelsV9.EngineeringSnapshotV9 engineering
    )
    {
        SaveModelsV10.InstalledSystemSnapshotV10 Installed(string kind, double condition, int? allocation) =>
            new()
            {
                InstalledSystemId = HistoricalShipSystemsV9.InstalledIdFor(design, kind).Value,
                DefinitionId = HistoricalShipSystemsV9.DefinitionIdFor(design, kind),
                Condition = condition,
                Allocation = allocation,
            };

        SaveModelsV5.SystemRepairSnapshotV5? repair = engineering.ActiveRepair;
        return new SaveModelsV10.EngineeringSnapshotV10
        {
            NextInstalledSystemId = HistoricalShipSystemsV9.NextInstalledSystemId,
            InstalledSystems =
            [
                .. new[]
                {
                    Installed(GenerationKind, engineering.GenerationCondition, null),
                    Installed(SensorsKind, engineering.SensorCondition, engineering.SensorAllocation),
                    Installed(ImpulseKind, engineering.ImpulseCondition, engineering.ImpulseAllocation),
                    Installed(ShieldsKind, engineering.ShieldCondition, engineering.ShieldAllocation),
                    Installed(WeaponsKind, engineering.DirectedEnergyCondition, engineering.DirectedEnergyAllocation),
                }.OrderBy(system => system.InstalledSystemId),
            ],
            ActiveRepair = repair is null
                ? null
                : new SaveModelsV10.SystemRepairSnapshotV10
                {
                    TargetInstalledSystemId = HistoricalShipSystemsV9.InstalledIdFor(design, repair.TargetSystem).Value,
                    StartingCondition = repair.StartingCondition,
                    TargetCondition = repair.TargetCondition,
                    StartedAtMilliseconds = repair.StartedAtMilliseconds,
                    ExpectedCompletionMilliseconds = repair.ExpectedCompletionMilliseconds,
                    ScheduledCompletionId = repair.ScheduledCompletionId,
                },
        };
    }

    private static SaveModelsV10.SensorKnowledgeSnapshotV10 MigrateSensorKnowledgeV9(
        string design,
        SaveModelsV6.SensorKnowledgeSnapshotV6 knowledge
    )
    {
        SaveModelsV4.ActiveSensorScanSnapshotV4? scan = knowledge.ActiveScan;
        return new SaveModelsV10.SensorKnowledgeSnapshotV10
        {
            NextContactId = knowledge.NextContactId,
            // Contacts verbatim, including legacy null observation frames: migration must not synthesize a location.
            Contacts = knowledge.Contacts,
            ActiveScan = scan is null
                ? null
                : new SaveModelsV10.ActiveSensorScanSnapshotV10
                {
                    TargetContactId = scan.TargetContactId,
                    SensorInstalledSystemId = HistoricalShipSystemsV9.InstalledIdFor(design, SensorsKind).Value,
                    StartedAtMilliseconds = scan.StartedAtMilliseconds,
                    ExpectedCompletionMilliseconds = scan.ExpectedCompletionMilliseconds,
                    ScheduledCompletionId = scan.ScheduledCompletionId,
                },
        };
    }

    /// <summary>
    /// Test seam: runs the production adjacent migrations V1→…→V10 over a V1 document, without the intervening
    /// validation, and returns each migration's output labels in chain order.
    /// </summary>
    /// <remarks>
    /// Exists so a regression test can pin that every migration emits its own fixed labels: if a migration read the
    /// current aliases, bumping them would change its entry here.
    /// </remarks>
    internal static IReadOnlyList<(int SchemaVersion, string SimulationRulesVersion)> MigrationOutputLabelsFromV1(
        byte[] v1Json,
        ShipDefinitionCatalog catalog
    )
    {
        SaveModelsV1.SaveEnvelopeV1 v1 =
            JsonSerializer.Deserialize<SaveModelsV1.SaveEnvelopeV1>(v1Json, SerializerOptions)
            ?? throw new JsonException("The save root must be an object.");
        SaveModelsV2.SaveEnvelopeV2 v2 = MigrateV1ToV2(v1);
        SaveModelsV3.SaveEnvelopeV3 v3 = MigrateV2ToV3(v2);
        SaveModelsV4.SaveEnvelopeV4 v4 = MigrateV3ToV4(v3);
        SaveModelsV5.SaveEnvelopeV5 v5 = MigrateV4ToV5(v4, catalog);
        SaveModelsV6.SaveEnvelopeV6 v6 = MigrateV5ToV6(v5);
        SaveModelsV7.SaveEnvelopeV7 v7 = MigrateV6ToV7(v6);
        SaveModelsV8.SaveEnvelopeV8 v8 = MigrateV7ToV8(v7);
        SaveEnvelopeV9 v9 = MigrateV8ToV9(v8);
        SaveEnvelopeV10 v10 = MigrateV9ToV10(v9, catalog.SystemDefinitions);
        return
        [
            (v2.SchemaVersion, v2.SimulationRulesVersion),
            (v3.SchemaVersion, v3.SimulationRulesVersion),
            (v4.SchemaVersion, v4.SimulationRulesVersion),
            (v5.SchemaVersion, v5.SimulationRulesVersion),
            (v6.SchemaVersion, v6.SimulationRulesVersion),
            (v7.SchemaVersion, v7.SimulationRulesVersion),
            (v8.SchemaVersion, v8.SimulationRulesVersion),
            (v9.SchemaVersion, v9.SimulationRulesVersion),
            (v10.SchemaVersion, v10.SimulationRulesVersion),
        ];
    }

    /// <summary>Returns the verified descriptor, or fails closed when the definition is absent or changed.</summary>
    private static string VerifyDefinitionSemantics(
        SystemDefinitionCatalog systems,
        string definitionId,
        string savedSemantics,
        string subject
    )
    {
        string current = systems.TryGet(new SystemDefinitionId(definitionId), out SystemDefinition? definition)
            ? SystemDefinitionSemantics.Describe(definition)
            : "absent";
        return string.Equals(current, savedSemantics, StringComparison.Ordinal)
            ? current
            : throw new SaveContentIncompatibleException(
                $"references system definition '{definitionId}' for {subject} with saved semantics '{savedSemantics}', "
                    + $"but the supplied content's definition is '{current}'; load with the content this save was "
                    + "created with, or start a new game."
            );
    }

    private static SaveContentIncompatibleException AimVocabularyMismatch(
        string version,
        string saved,
        string current
    ) =>
        new(
            $"is a {version} save played under aim vocabulary '{saved}', but the supplied content derives '{current}'; "
                + "load with the content this save was created with, or start a new game."
        );

    /// <summary>
    /// Validates an untrusted V10 candidate before any runtime construction.
    /// </summary>
    /// <remarks>
    /// Order matters for the failure category: every structural check (nulls, bounds, strings, canonical order,
    /// references) runs first and fails as invalid data; the content comparison runs only on a well-formed document
    /// and fails as incompatible content; definition-dependent checks (allocation presence, specialized-reference
    /// kinds) run last because they need the verified definitions. Tuning-dependent rules (demand and supply, speed,
    /// repair duration and condition, scan duration, readiness window, exact scheduler correlation) and per-kind
    /// cardinality are left to <c>SimulationState.Validate</c> during <see cref="RestoreV10"/>: they are the runtime's
    /// own invariants, and the common snapshot boundary must be able to represent several installations of one kind
    /// so that the typed refusal stays a distinct error.
    /// </remarks>
    private static void ValidateCandidateV10(SaveEnvelopeV10 envelope, SystemDefinitionCatalog systems, string label)
    {
        if (
            envelope.SchemaVersion != V10SchemaVersion
            || !string.Equals(envelope.SimulationRulesVersion, V10SimulationRulesVersion, StringComparison.Ordinal)
        )
            throw new InvalidOperationException($"The {label} schema or simulation rules identity is unsupported.");
        if (envelope.Metadata is null || envelope.Simulation is null)
            throw new InvalidOperationException("Required V10 envelope members cannot be null.");
        ValidateMetadata(
            new GameSaveMetadata(
                envelope.Metadata.SaveId,
                envelope.Metadata.DisplayName,
                envelope.Metadata.CreatedAtUtc,
                envelope.Metadata.SavedAtUtc
            )
        );

        SimulationSnapshotV10 snapshot = envelope.Simulation;
        if (
            snapshot.Ships is null
            || snapshot.Factions is null
            || snapshot.Scheduler?.OutstandingWork is null
            || snapshot.StrategicMap is null
            || snapshot.SystemDefinitions is null
        )
            throw new InvalidOperationException("Required V10 simulation members cannot be null.");
        AimVocabularySemantics.ValidateFormat(snapshot.AimVocabulary);
        Dictionary<string, string> table = ValidateDefinitionTableV10(snapshot.SystemDefinitions);
        EnsureCount(snapshot.Ships.Length, SimulationState.MaximumShips, "ships");
        EnsureCount(snapshot.Factions.Length, SimulationState.MaximumFactions, "factions");
        var used = new HashSet<string>(StringComparer.Ordinal);
        long previousShip = 0;
        foreach (ShipSnapshotV10? ship in snapshot.Ships)
        {
            if (ship is null)
                throw new InvalidOperationException("Ship snapshots cannot be null.");
            if (ship.InstanceId <= previousShip)
                throw new InvalidOperationException("V10 ships must be unique and in ascending identity order.");
            previousShip = ship.InstanceId;
            ValidateShipStructureV10(ship, table, used, snapshot.TimeMilliseconds);
        }

        if (table.Keys.Any(id => !used.Contains(id)))
            throw new InvalidOperationException("The V10 system-definition table lists an unreferenced definition.");

        ValidateSharedStructureV10(snapshot);
        ValidateContentV10(snapshot, table, systems);
    }

    /// <summary>
    /// Compares the saved descriptors with the supplied content, then applies the checks that need the verified
    /// definitions. Runs only on a well-formed document, so a mismatch here is incompatibility, not invalid data.
    /// </summary>
    private static void ValidateContentV10(
        SimulationSnapshotV10 snapshot,
        Dictionary<string, string> table,
        SystemDefinitionCatalog systems
    )
    {
        foreach ((string definitionId, string semantics) in table)
        {
            VerifyDefinitionSemantics(systems, definitionId, semantics, "the saved world");
        }

        string currentVocabulary = AimVocabularySemantics.Describe(systems.DamageTargetKinds);
        if (!string.Equals(snapshot.AimVocabulary, currentVocabulary, StringComparison.Ordinal))
            throw AimVocabularyMismatch("V10", snapshot.AimVocabulary, currentVocabulary);

        foreach (ShipSnapshotV10 ship in snapshot.Ships)
        {
            ValidateShipDefinitionsV10(ship, systems);
        }
    }

    private static Dictionary<string, string> ValidateDefinitionTableV10(
        SaveModelsV10.SystemDefinitionReferenceSnapshotV10[] rows
    )
    {
        EnsureCount(rows.Length, MaximumSystemDefinitionReferences, "system definition references");
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        string? previous = null;
        foreach (SaveModelsV10.SystemDefinitionReferenceSnapshotV10? row in rows)
        {
            if (row is null)
                throw new InvalidOperationException("System definition references cannot be null.");
            _ = new SystemDefinitionId(row.DefinitionId);
            if (
                row.Semantics is null
                || row.Semantics.Length > SystemDefinitionSemantics.MaximumLength
                || !row.Semantics.StartsWith(
                    SystemDefinitionSemantics.DescriptorVersion + ";",
                    StringComparison.Ordinal
                )
                || !row.Semantics.All(IsDefinitionDescriptorCharacter)
            )
                throw new InvalidOperationException(
                    $"System definition '{row.DefinitionId}' has a malformed semantics descriptor."
                );
            if (previous is not null && string.CompareOrdinal(previous, row.DefinitionId) >= 0)
                throw new InvalidOperationException(
                    "System definition references must be unique and in ascending ordinal order."
                );
            previous = row.DefinitionId;
            table.Add(row.DefinitionId, row.Semantics);
        }

        return table;
    }

    private static void ValidateShipStructureV10(
        ShipSnapshotV10 ship,
        Dictionary<string, string> table,
        HashSet<string> used,
        long currentTime
    )
    {
        if (
            ship.Engineering?.InstalledSystems is null
            || ship.Combat?.DirectedEnergyReadiness is null
            || ship.SensorKnowledge?.Contacts is null
            || ship.TacticalPosition is null
            || ship.TacticalMotion is null
            || ship.StrategicState is null
            || ship.AutonomousState is null
        )
            throw new InvalidOperationException("V10 ship members cannot be null.");

        SaveModelsV10.EngineeringSnapshotV10 engineering = ship.Engineering;
        HashSet<long> installed = ValidateInstallationsV10(engineering.InstalledSystems, table, used);
        long previous = installed.Count == 0 ? 0 : installed.Max();
        // Continuation, never recomputation: the allocator must follow every retained identity (gaps are legal), and
        // long.MaxValue is the representable exhausted state.
        _ = InstalledSystemIdAllocator.Restore(engineering.NextInstalledSystemId);
        if (engineering.NextInstalledSystemId <= previous)
            throw new InvalidOperationException("The installed-system allocator must follow every installed identity.");

        if (engineering.ActiveRepair is { } repair)
        {
            if (!installed.Contains(repair.TargetInstalledSystemId))
                throw new InvalidOperationException("Active repair must target an installation on the same ship.");
            ValidateRepairTimingV5(
                new SaveModelsV5.SystemRepairSnapshotV5
                {
                    TargetSystem = string.Empty,
                    StartingCondition = repair.StartingCondition,
                    TargetCondition = repair.TargetCondition,
                    StartedAtMilliseconds = repair.StartedAtMilliseconds,
                    ExpectedCompletionMilliseconds = repair.ExpectedCompletionMilliseconds,
                    ScheduledCompletionId = repair.ScheduledCompletionId,
                },
                currentTime
            );
        }

        if (ship.SensorKnowledge.ActiveScan is { } scan && !installed.Contains(scan.SensorInstalledSystemId))
            throw new InvalidOperationException("An active scan must name a sensor installed on the same ship.");

        ValidateReadinessStructureV10(ship.Combat.DirectedEnergyReadiness, installed);
    }

    private static HashSet<long> ValidateInstallationsV10(
        SaveModelsV10.InstalledSystemSnapshotV10[] systems,
        Dictionary<string, string> table,
        HashSet<string> used
    )
    {
        EnsureCount(systems.Length, ShipSystemLimits.MaximumInstalledSystemsPerShip, "installed systems per ship");
        var installed = new HashSet<long>();
        long previous = 0;
        foreach (SaveModelsV10.InstalledSystemSnapshotV10? system in systems)
        {
            if (system is null)
                throw new InvalidOperationException("Installed systems cannot be null.");
            _ = new InstalledSystemId(system.InstalledSystemId);
            if (system.InstalledSystemId <= previous)
                throw new InvalidOperationException(
                    "Installed systems must be unique and in ascending identity order."
                );
            previous = system.InstalledSystemId;
            installed.Add(system.InstalledSystemId);
            _ = new SystemDefinitionId(system.DefinitionId);
            if (!table.ContainsKey(system.DefinitionId))
                throw new InvalidOperationException(
                    $"Installed system {system.InstalledSystemId} references definition '{system.DefinitionId}', "
                        + "which the V10 system-definition table does not list."
                );
            used.Add(system.DefinitionId);
            EnsureUnitInterval(system.Condition, "Installed system condition");
            if (system.Allocation is { } allocation)
                _ = new PowerUnits(allocation);
        }

        return installed;
    }

    private static void ValidateReadinessStructureV10(
        SaveModelsV10.DirectedEnergyReadinessSnapshotV10[] readiness,
        HashSet<long> installed
    )
    {
        EnsureCount(
            readiness.Length,
            ShipSystemLimits.MaximumInstalledSystemsPerShip,
            "weapon readiness entries per ship"
        );
        long previousWeapon = 0;
        foreach (SaveModelsV10.DirectedEnergyReadinessSnapshotV10? entry in readiness)
        {
            if (entry is null)
                throw new InvalidOperationException("Weapon readiness entries cannot be null.");
            if (entry.WeaponInstalledSystemId <= previousWeapon)
                throw new InvalidOperationException("Weapon readiness must be unique and in ascending identity order.");
            previousWeapon = entry.WeaponInstalledSystemId;
            if (!installed.Contains(entry.WeaponInstalledSystemId))
                throw new InvalidOperationException("Weapon readiness must name a weapon installed on the same ship.");
            EnsureFixedStep(entry.ReadyAtMilliseconds, "Weapon readiness");
        }
    }

    /// <summary>
    /// Runs the unchanged, tuning-free structural validators of the domains V10 shares with V8 (map, identities,
    /// allocators, scheduler, strategic state, orders, contacts, observed frames, factions, observation).
    /// </summary>
    /// <remarks>
    /// The shared validators are typed on the historical snapshots, so the V10 candidate is projected into that shape
    /// with engineering omitted: none of the validators called here reads engineering, and V10 engineering is
    /// validated by <see cref="ValidateShipStructureV10"/> and the runtime instead. Deliberately not
    /// <c>ValidateCandidateV8</c>: that path applies the frozen historical tuning, which a current-content world with
    /// arbitrary designs must never be judged against.
    /// </remarks>
    private static void ValidateSharedStructureV10(SimulationSnapshotV10 snapshot)
    {
        if (snapshot.TimeMilliseconds < 0)
            throw new InvalidOperationException("Current simulation time cannot be negative.");
        if (snapshot.TimeMilliseconds > long.MaxValue - SimulationFixedStep.Duration.Milliseconds)
            throw new InvalidOperationException(
                "Current simulation time lacks one fixed-step of continuation headroom."
            );
        EnsureFixedStep(snapshot.TimeMilliseconds, "Current simulation time");
        ValidateMapCandidateV2(snapshot.StrategicMap);
        if (snapshot.Ships.Length == 0)
            throw new InvalidOperationException("The save must contain at least one ship.");
        HashSet<long> shipIds = [.. snapshot.Ships.Select(ship => ship.InstanceId)];
        if (snapshot.PlayerShipId <= 0 || !shipIds.Contains(snapshot.PlayerShipId))
            throw new InvalidOperationException("Player ship identity must resolve exactly once.");
        if (snapshot.ShipAllocatorNextId <= shipIds.Max() || snapshot.ShipAllocatorNextId == long.MaxValue)
            throw new InvalidOperationException(
                "Ship allocator must follow every ship identity and retain continuation headroom."
            );

        SaveModelsV8.SimulationSnapshotV8 structural = StructuralShapeV10(snapshot);
        SaveModelsV7.SimulationSnapshotV7 v7 = ToV7Snapshot(structural);
        SaveModelsV5.SimulationSnapshotV5 v5 = ToBaseSnapshotV5(v7);
        SaveModelsV3.SimulationSnapshotV3 v3 = ToBaseSnapshotV3(v5);
        SaveModelsV2.SimulationSnapshotV2 v2 = ToBaseSnapshotV2(v3);
        SaveModelsV2.SchedulerSnapshotV2 shipScheduler = ToShipSchedulerV2(snapshot.Scheduler);
        foreach (ShipSnapshotV10 ship in snapshot.Ships)
        {
            ValidateText(ship.DefinitionId, "Ship definition identity", ShipDefinitionId.MaximumLength);
            ValidateText(ship.DisplayName, "Ship display name", ShipState.MaximumVesselDisplayNameLength);
            EnsureFinite(ship.TacticalPosition.XKilometers, "Ship tactical X position");
            EnsureFinite(ship.TacticalPosition.YKilometers, "Ship tactical Y position");
            EnsureFinite(ship.TacticalMotion.HeadingDegrees, "Ship tactical heading");
            EnsureFinite(ship.TacticalMotion.SpeedKilometersPerSecond, "Ship tactical speed");
            if (ship.TacticalMotion.SpeedKilometersPerSecond < 0)
                throw new InvalidOperationException("Ship tactical speed cannot be negative.");
        }

        foreach (SaveModelsV2.ShipSnapshotV2 ship in v2.Ships)
        {
            ValidateStrategicCandidateV2(ship, snapshot.StrategicMap, shipScheduler, snapshot.TimeMilliseconds);
        }

        ValidateOrderCandidatesV3(v3);
        ValidateSensorCandidatesV4(ToSensorSnapshotV4(v5));
        ValidateObservedLocationCandidatesV6(ToObservedSnapshotV6(v7));
        ValidateFactionShapeV8(structural);
        ValidateSchedulerCandidate(
            snapshot.Scheduler,
            snapshot.TimeMilliseconds,
            shipIds,
            snapshot.Factions.Select(faction => faction?.Id ?? 0),
            V10SchemaVersion
        );
        ValidateObservationShapeV8(structural);
    }

    private static SaveModelsV8.SimulationSnapshotV8 StructuralShapeV10(SimulationSnapshotV10 snapshot) =>
        new()
        {
            TimeMilliseconds = snapshot.TimeMilliseconds,
            ShipAllocatorNextId = snapshot.ShipAllocatorNextId,
            OrderAllocatorNextId = snapshot.OrderAllocatorNextId,
            ObservationReportAllocatorNextId = snapshot.ObservationReportAllocatorNextId,
            PlayerShipId = snapshot.PlayerShipId,
            Scheduler = snapshot.Scheduler,
            StrategicMap = snapshot.StrategicMap,
            Ships =
            [
                .. snapshot.Ships.Select(ship => new SaveModelsV7.ShipSnapshotV7
                {
                    InstanceId = ship.InstanceId,
                    DefinitionId = ship.DefinitionId,
                    DisplayName = ship.DisplayName,
                    TacticalPosition = ship.TacticalPosition,
                    TacticalMotion = ship.TacticalMotion,
                    // Omitted on purpose: see ValidateSharedStructureV10.
                    Engineering = null!,
                    StrategicState = ship.StrategicState,
                    ActiveOrder = ship.ActiveOrder,
                    SensorKnowledge = new SaveModelsV6.SensorKnowledgeSnapshotV6
                    {
                        NextContactId = ship.SensorKnowledge.NextContactId,
                        Contacts = ship.SensorKnowledge.Contacts,
                        ActiveScan = null,
                    },
                    AutonomousState = ship.AutonomousState,
                    DirectControllerFactionId = ship.DirectControllerFactionId,
                }),
            ],
            Factions = snapshot.Factions,
        };

    /// <summary>
    /// Applies the checks that need each installation's verified definition: allocation present exactly for power
    /// consumers, a repair target that is repairable, a scan source that is a sensor, and readiness keyed by weapons.
    /// </summary>
    private static void ValidateShipDefinitionsV10(ShipSnapshotV10 ship, SystemDefinitionCatalog systems)
    {
        var definitions = new Dictionary<long, SystemDefinition>();
        foreach (SaveModelsV10.InstalledSystemSnapshotV10 system in ship.Engineering.InstalledSystems)
        {
            SystemDefinition definition = systems.GetRequired(new SystemDefinitionId(system.DefinitionId));
            if ((definition.Power is null) != (system.Allocation is null))
                throw new InvalidOperationException(
                    $"Ship {ship.InstanceId} installation {system.InstalledSystemId} must carry an allocation exactly "
                        + "when its definition consumes power."
                );
            definitions.Add(system.InstalledSystemId, definition);
        }

        if (ship.Engineering.ActiveRepair is { } repair && definitions[repair.TargetInstalledSystemId].Repair is null)
            throw new InvalidOperationException("Active repair must target a repairable installation.");
        if (
            ship.SensorKnowledge.ActiveScan is { } scan
            && definitions[scan.SensorInstalledSystemId] is not SensorSystemDefinition
        )
            throw new InvalidOperationException("An active scan's source installation must be a sensor.");
        if (
            ship.Combat.DirectedEnergyReadiness.Any(entry =>
                definitions[entry.WeaponInstalledSystemId] is not DirectedEnergyWeaponSystemDefinition
            )
        )
            throw new InvalidOperationException(
                "Weapon readiness must be keyed by directed-energy weapon installations."
            );
    }

    private static LoadedGameSave RestoreV10(
        SaveEnvelopeV10 envelope,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        ILogger<GameSimulation>? logger
    )
    {
        SimulationSnapshotV10 snapshot = envelope.Simulation;
        var state = new SimulationState(
            new SimulationTime(snapshot.TimeMilliseconds),
            RestoreSchedulerV7(snapshot.Scheduler, V10SchemaVersion),
            ShipInstanceIdAllocator.Restore(snapshot.ShipAllocatorNextId),
            RestoreMapV2(snapshot.StrategicMap),
            new ShipInstanceId(snapshot.PlayerShipId),
            snapshot.Ships.Select(ship => RestoreShipV10(ship, catalog.SystemDefinitions)),
            ShipOrderIdAllocator.Restore(snapshot.OrderAllocatorNextId),
            snapshot.Factions.Select(RestoreFactionV8),
            ObservationReportIdAllocator.Restore(snapshot.ObservationReportAllocatorNextId)
        );
        var metadata = new GameSaveMetadata(
            envelope.Metadata.SaveId,
            envelope.Metadata.DisplayName,
            envelope.Metadata.CreatedAtUtc,
            envelope.Metadata.SavedAtUtc
        );
        return new LoadedGameSave(metadata, GameSimulation.RestoreState(state, catalog, factionCatalog, logger));
    }

    private static ShipState RestoreShipV10(ShipSnapshotV10 snapshot, SystemDefinitionCatalog systems)
    {
        SaveModelsV10.ActiveSensorScanSnapshotV10? scan = snapshot.SensorKnowledge.ActiveScan;
        return new ShipState(
            new ShipInstanceId(snapshot.InstanceId),
            new ShipDefinitionId(snapshot.DefinitionId),
            snapshot.DisplayName,
            new TacticalPosition(snapshot.TacticalPosition.XKilometers, snapshot.TacticalPosition.YKilometers),
            new TacticalMotion(
                new HeadingDegrees(snapshot.TacticalMotion.HeadingDegrees),
                new SpeedKilometersPerSecond(snapshot.TacticalMotion.SpeedKilometersPerSecond)
            ),
            RestoreEngineeringV10(snapshot.Engineering, systems),
            RestoreStrategicStateV2(snapshot.StrategicState),
            RestoreOrderV3(snapshot.ActiveOrder),
            RestoreSensorKnowledge(
                snapshot.SensorKnowledge.NextContactId,
                snapshot.SensorKnowledge.Contacts,
                scan is null
                    ? null
                    : new ActiveSensorScanState(
                        new SensorContactId(scan.TargetContactId),
                        new InstalledSystemId(scan.SensorInstalledSystemId),
                        new SimulationTime(scan.StartedAtMilliseconds),
                        new SimulationTime(scan.ExpectedCompletionMilliseconds),
                        new ScheduledWorkId(scan.ScheduledCompletionId)
                    )
            ),
            RestoreAutonomousStateV4(snapshot.AutonomousState),
            snapshot.DirectControllerFactionId is null ? null : new FactionId(snapshot.DirectControllerFactionId.Value),
            RestoreCombatV10(snapshot.Combat)
        );
    }

    private static ShipEngineeringState RestoreEngineeringV10(
        SaveModelsV10.EngineeringSnapshotV10 engineering,
        SystemDefinitionCatalog systems
    )
    {
        SaveModelsV10.SystemRepairSnapshotV10? repair = engineering.ActiveRepair;
        return new ShipEngineeringState(
            InstalledSystemCollection.Create(
                engineering.InstalledSystems.Select(system => new InstalledSystem(
                    new InstalledSystemId(system.InstalledSystemId),
                    systems.GetRequired(new SystemDefinitionId(system.DefinitionId)),
                    new SystemCondition(system.Condition),
                    system.Allocation is null ? null : new PowerUnits(system.Allocation.Value)
                ))
            ),
            InstalledSystemIdAllocator.Restore(engineering.NextInstalledSystemId),
            repair is null
                ? null
                : new SystemRepairState(
                    new InstalledSystemId(repair.TargetInstalledSystemId),
                    new SystemCondition(repair.StartingCondition),
                    new SystemCondition(repair.TargetCondition),
                    new SimulationTime(repair.StartedAtMilliseconds),
                    new SimulationTime(repair.ExpectedCompletionMilliseconds),
                    new ScheduledWorkId(repair.ScheduledCompletionId)
                )
        );
    }

    private static ShipCombatState RestoreCombatV10(SaveModelsV10.ShipCombatSnapshotV10 combat)
    {
        SaveModelsV9.CombatStimulusSnapshotV9? stimulus = combat.PendingStimulus;
        return new ShipCombatState(
            combat.DirectedEnergyReadiness.Select(entry => new DirectedEnergyReadiness(
                new InstalledSystemId(entry.WeaponInstalledSystemId),
                new SimulationTime(entry.ReadyAtMilliseconds)
            )),
            stimulus is null
                ? null
                : new CombatStimulus(
                    new SensorContactId(stimulus.ContactId),
                    new SimulationTime(stimulus.ObservedAtMilliseconds),
                    new SimulationTime(stimulus.DueTimeMilliseconds),
                    new ScheduledWorkId(stimulus.ScheduledWorkId)
                )
        );
    }

    private static bool IsDefinitionDescriptorCharacter(char character) =>
        character
            is >= 'a'
                and <= 'z'
                or >= 'A'
                and <= 'Z'
                or >= '0'
                and <= '9'
                or '.'
                or '='
                or ';'
                or '_'
                or '+'
                or '-';
}
