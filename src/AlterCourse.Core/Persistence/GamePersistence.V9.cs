using System.Text.Json;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;
using SaveEnvelopeV8 = AlterCourse.Core.Persistence.SaveModelsV8.SaveEnvelopeV8;
using SaveEnvelopeV9 = AlterCourse.Core.Persistence.SaveModelsV9.SaveEnvelopeV9;
using SaveMetadataV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveMetadataV2;
using ShipSnapshotV7 = AlterCourse.Core.Persistence.SaveModelsV7.ShipSnapshotV7;
using ShipSnapshotV9 = AlterCourse.Core.Persistence.SaveModelsV9.ShipSnapshotV9;

namespace AlterCourse.Core.Persistence;

public static partial class GamePersistence
{
    private const string GenerationKind = "power-generation";
    private const string SensorsKind = "sensors";
    private const string ImpulseKind = "impulse-propulsion";
    private const string ShieldsKind = "shields";
    private const string WeaponsKind = "directed-energy-weapons";

    // TEMPORARY BRIDGE (removed by leg L4): until V10 persistence exists, the current runtime is written in the V9
    // wire format. That is lossless only for a ship whose installations are exactly five, one per V9 fixed field, in
    // the V9 slots (installed ids 1–5, continuation 6), so any other loadout is refused rather than flattened into
    // fixed fields. L4 replaces this capture with CaptureV10, which reads installed state directly.
    //
    // While V9 is still the current wire format, the bridge resolves a V9 ship's definitions and tuning from the
    // ship's own design in the supplied catalog, exactly as pre-substrate V9 loading read that catalog. The frozen
    // historical boundary (HistoricalShipSystemsV9 keyed by "pathfinder", HistoricalShipContentV5) is not applied
    // here: it becomes the V9 rule when L4 makes V9 historical and moves this translation into MigrateV9ToV10.
    private const long BridgeNextInstalledSystemIdV9 = 6;

    private static InstalledSystemId BridgeInstalledIdV9(string kind) =>
        kind switch
        {
            GenerationKind => new InstalledSystemId(1),
            SensorsKind => new InstalledSystemId(2),
            ImpulseKind => new InstalledSystemId(3),
            ShieldsKind => new InstalledSystemId(4),
            WeaponsKind => new InstalledSystemId(5),
            _ => throw new InvalidOperationException($"V9 system kind '{kind}' has no installed-system slot."),
        };

    /// <summary>Resolves the definition the ship's design installs in a V9 slot, verifying its kind.</summary>
    private static SystemDefinition BridgeDefinitionV9(ShipDefinitionCatalog catalog, string design, string kind)
    {
        InstalledSystemId slot = BridgeInstalledIdV9(kind);
        SystemDefinition? definition = catalog
            .GetRequired(new ShipDefinitionId(design))
            .InitialLoadout.Systems.Where(system => system.Id == slot)
            .Select(system => catalog.SystemDefinitions.GetRequired(system.DefinitionId))
            .SingleOrDefault();
        return definition is not null && string.Equals(definition.Kind.Value, kind, StringComparison.Ordinal)
            ? definition
            : throw new InvalidOperationException(
                $"Ship definition '{design}' does not install a '{kind}' system in V9 slot {slot.Value}."
            );
    }

    /// <summary>Projects the ship's design into the historical tuning shape the shared V2–V8 validators read.</summary>
    private static HistoricalShipContentV5 BridgeTuningV9(ShipDefinitionCatalog catalog, string design)
    {
        T Of<T>(string kind)
            where T : SystemDefinition => (T)BridgeDefinitionV9(catalog, design, kind);
        PowerGenerationSystemDefinition generation = Of<PowerGenerationSystemDefinition>(GenerationKind);
        SensorSystemDefinition sensors = Of<SensorSystemDefinition>(SensorsKind);
        ImpulsePropulsionSystemDefinition impulse = Of<ImpulsePropulsionSystemDefinition>(ImpulseKind);
        ShieldSystemDefinition shields = Of<ShieldSystemDefinition>(ShieldsKind);
        DirectedEnergyWeaponSystemDefinition weapons = Of<DirectedEnergyWeaponSystemDefinition>(WeaponsKind);
        return new HistoricalShipContentV5(
            design,
            catalog.GetRequired(new ShipDefinitionId(design)).DesignDisplayName,
            impulse.MaximumTacticalSpeed.Value,
            generation.NominalOutput.Value,
            sensors.Power!.NominalDemand.Value,
            impulse.Power!.NominalDemand.Value,
            shields.Power!.NominalDemand.Value,
            weapons.Power!.NominalDemand.Value,
            sensors.Repair!.FullRepairDuration.Milliseconds,
            impulse.Repair!.FullRepairDuration.Milliseconds,
            shields.Repair!.FullRepairDuration.Milliseconds,
            weapons.Repair!.FullRepairDuration.Milliseconds
        );
    }

    private static SaveEnvelopeV9 CaptureV9(SimulationState state, GameSaveMetadata metadata)
    {
        if (
            state.Ships.Length > SimulationState.MaximumShips
            || state.Factions.Length > SimulationState.MaximumFactions
        )
        {
            throw new InvalidOperationException(
                "V9 persistence supports at most the simulation ship and faction limits."
            );
        }

        return new SaveEnvelopeV9
        {
            SchemaVersion = CurrentSchemaVersion,
            SimulationRulesVersion = CurrentSimulationRulesVersion,
            Metadata = new SaveMetadataV2
            {
                SaveId = metadata.SaveId,
                DisplayName = metadata.DisplayName,
                CreatedAtUtc = metadata.CreatedAtUtc,
                SavedAtUtc = metadata.SavedAtUtc,
            },
            Simulation = new SaveModelsV9.SimulationSnapshotV9
            {
                TimeMilliseconds = state.Time.Milliseconds,
                ShipAllocatorNextId = state.ShipIdAllocator.NextId,
                OrderAllocatorNextId = state.OrderIdAllocator.NextId,
                ObservationReportAllocatorNextId = state.ObservationReportIdAllocator.NextId,
                PlayerShipId = state.PlayerShipId.Value,
                Scheduler = CaptureSchedulerV7(state.Scheduler),
                StrategicMap = CaptureStrategicMapV2(state.StrategicMap),
                Ships = [.. state.Ships.OrderBy(ship => ship.InstanceId.Value).Select(CaptureShipV9)],
                Factions = [.. state.Factions.OrderBy(faction => faction.Id.Value).Select(CaptureFactionV8)],
            },
        };
    }

    private static ShipSnapshotV9 CaptureShipV9(ShipState ship)
    {
        SaveModelsV9.EngineeringSnapshotV9 engineering = CaptureEngineeringV9(ship);
        return CopyShipV9(
            UpgradeShipV8(CaptureShipV7(ship, LegacyEngineeringV9(engineering))),
            engineering,
            new SaveModelsV9.ShipCombatSnapshotV9
            {
                NextDirectedEnergyReadyAtMilliseconds = ship
                    .Combat.ReadinessOf(BridgeInstalledIdV9(WeaponsKind))!
                    .ReadyAt.Milliseconds,
                PendingStimulus = ship.Combat.PendingStimulus is not { } stimulus
                    ? null
                    : new SaveModelsV9.CombatStimulusSnapshotV9
                    {
                        ContactId = stimulus.ContactId.Value,
                        ObservedAtMilliseconds = stimulus.ObservedAt.Milliseconds,
                        DueTimeMilliseconds = stimulus.DueTime.Milliseconds,
                        ScheduledWorkId = stimulus.ScheduledWorkId.Value,
                    },
            }
        );
    }

    private static SaveModelsV9.EngineeringSnapshotV9 CaptureEngineeringV9(ShipState ship)
    {
        ShipEngineeringState engineering = ship.Engineering;
        InstalledSystem Mapped(string kind) =>
            engineering.Systems.TryGet(BridgeInstalledIdV9(kind), out InstalledSystem? system)
            && string.Equals(system.Kind.Value, kind, StringComparison.Ordinal)
                ? system
                : throw new InvalidOperationException("Heterogeneous loadouts require V10 persistence");

        InstalledSystem generation = Mapped(GenerationKind);
        InstalledSystem sensors = Mapped(SensorsKind);
        InstalledSystem impulse = Mapped(ImpulseKind);
        InstalledSystem shields = Mapped(ShieldsKind);
        InstalledSystem weapons = Mapped(WeaponsKind);
        if (engineering.Systems.Count != 5 || engineering.InstallationIds.NextId != BridgeNextInstalledSystemIdV9)
        {
            throw new InvalidOperationException("Heterogeneous loadouts require V10 persistence");
        }

        SystemRepairState? repair = engineering.ActiveRepair;
        return new()
        {
            GenerationCondition = generation.Condition.Value,
            SensorCondition = sensors.Condition.Value,
            ImpulseCondition = impulse.Condition.Value,
            ShieldCondition = shields.Condition.Value,
            DirectedEnergyCondition = weapons.Condition.Value,
            SensorAllocation = sensors.Allocation!.Value.Value,
            ImpulseAllocation = impulse.Allocation!.Value.Value,
            ShieldAllocation = shields.Allocation!.Value.Value,
            DirectedEnergyAllocation = weapons.Allocation!.Value.Value,
            ActiveRepair = repair is null
                ? null
                : new SaveModelsV5.SystemRepairSnapshotV5
                {
                    TargetSystem = engineering.Systems.GetRequired(repair.Target).Kind.Value,
                    StartingCondition = repair.StartingCondition.Value,
                    TargetCondition = repair.TargetCondition.Value,
                    StartedAtMilliseconds = repair.StartedAt.Milliseconds,
                    ExpectedCompletionMilliseconds = repair.ExpectedCompletion.Milliseconds,
                    ScheduledCompletionId = repair.ScheduledCompletionId.Value,
                },
        };
    }

    private static SaveModelsV5.EngineeringSnapshotV5 LegacyEngineeringV9(
        SaveModelsV9.EngineeringSnapshotV9 engineering
    ) =>
        new()
        {
            GenerationCondition = engineering.GenerationCondition,
            SensorCondition = engineering.SensorCondition,
            ImpulseCondition = engineering.ImpulseCondition,
            SensorAllocation = engineering.SensorAllocation,
            ImpulseAllocation = engineering.ImpulseAllocation,
            ActiveRepair = engineering.ActiveRepair,
        };

    private static SaveEnvelopeV9 MigrateV8ToV9(SaveEnvelopeV8 envelope) =>
        new()
        {
            SchemaVersion = CurrentSchemaVersion,
            SimulationRulesVersion = CurrentSimulationRulesVersion,
            Metadata = envelope.Metadata,
            Simulation = new SaveModelsV9.SimulationSnapshotV9
            {
                TimeMilliseconds = envelope.Simulation.TimeMilliseconds,
                ShipAllocatorNextId = envelope.Simulation.ShipAllocatorNextId,
                OrderAllocatorNextId = envelope.Simulation.OrderAllocatorNextId,
                ObservationReportAllocatorNextId = envelope.Simulation.ObservationReportAllocatorNextId,
                PlayerShipId = envelope.Simulation.PlayerShipId,
                Scheduler = envelope.Simulation.Scheduler,
                StrategicMap = envelope.Simulation.StrategicMap,
                Ships = [.. envelope.Simulation.Ships.Select(UpgradeShipV8)],
                Factions = envelope.Simulation.Factions,
            },
        };

    private static ShipSnapshotV9 UpgradeShipV8(ShipSnapshotV7 ship) =>
        new()
        {
            InstanceId = ship.InstanceId,
            DefinitionId = ship.DefinitionId,
            DisplayName = ship.DisplayName,
            TacticalPosition = ship.TacticalPosition,
            TacticalMotion = ship.TacticalMotion,
            Engineering = new SaveModelsV9.EngineeringSnapshotV9
            {
                GenerationCondition = ship.Engineering.GenerationCondition,
                SensorCondition = ship.Engineering.SensorCondition,
                ImpulseCondition = ship.Engineering.ImpulseCondition,
                ShieldCondition = 0,
                DirectedEnergyCondition = 0,
                SensorAllocation = ship.Engineering.SensorAllocation,
                ImpulseAllocation = ship.Engineering.ImpulseAllocation,
                ShieldAllocation = 0,
                DirectedEnergyAllocation = 0,
                ActiveRepair = ship.Engineering.ActiveRepair,
            },
            StrategicState = ship.StrategicState,
            ActiveOrder = ship.ActiveOrder,
            SensorKnowledge = ship.SensorKnowledge,
            AutonomousState = ship.AutonomousState,
            DirectControllerFactionId = ship.DirectControllerFactionId,
            Combat = new SaveModelsV9.ShipCombatSnapshotV9
            {
                NextDirectedEnergyReadyAtMilliseconds = 0,
                PendingStimulus = null,
            },
        };

    private static SaveModelsV9.SimulationSnapshotV9 CopySnapshotV9(
        SaveModelsV9.SimulationSnapshotV9 source,
        ShipSnapshotV9[] ships
    ) =>
        new()
        {
            TimeMilliseconds = source.TimeMilliseconds,
            ShipAllocatorNextId = source.ShipAllocatorNextId,
            OrderAllocatorNextId = source.OrderAllocatorNextId,
            ObservationReportAllocatorNextId = source.ObservationReportAllocatorNextId,
            PlayerShipId = source.PlayerShipId,
            Scheduler = source.Scheduler,
            StrategicMap = source.StrategicMap,
            Ships = ships,
            Factions = source.Factions,
        };

    private static ShipSnapshotV9 CopyShipV9(
        ShipSnapshotV9 source,
        SaveModelsV9.EngineeringSnapshotV9 engineering,
        SaveModelsV9.ShipCombatSnapshotV9 combat
    ) =>
        new()
        {
            InstanceId = source.InstanceId,
            DefinitionId = source.DefinitionId,
            DisplayName = source.DisplayName,
            TacticalPosition = source.TacticalPosition,
            TacticalMotion = source.TacticalMotion,
            Engineering = engineering,
            StrategicState = source.StrategicState,
            ActiveOrder = source.ActiveOrder,
            SensorKnowledge = source.SensorKnowledge,
            AutonomousState = source.AutonomousState,
            DirectControllerFactionId = source.DirectControllerFactionId,
            Combat = combat,
        };

    private static LoadedGameSave LoadV9(
        byte[] json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity
    )
    {
        try
        {
            SaveEnvelopeV9 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV9>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            return RestoreV9(envelope, catalog, factionCatalog);
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
                $"violates the V9 semantic contract: {exception.Message}",
                exception
            );
        }
    }

    private static LoadedGameSave RestoreV9(
        SaveEnvelopeV9 envelope,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog
    )
    {
        ValidateCandidateV9(envelope, catalog);
        SaveModelsV9.SimulationSnapshotV9 snapshot = envelope.Simulation;
        // Restore the original complete snapshot; the legacy structural projection must never
        // become the accepted graph or erase new repair/work correlations.
        var state = new SimulationState(
            new SimulationTime(snapshot.TimeMilliseconds),
            RestoreSchedulerV7(snapshot.Scheduler, CurrentSchemaVersion),
            ShipInstanceIdAllocator.Restore(snapshot.ShipAllocatorNextId),
            RestoreMapV2(snapshot.StrategicMap),
            new ShipInstanceId(snapshot.PlayerShipId),
            snapshot.Ships.Select(ship => RestoreShipV9(ship, catalog)),
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
        return new LoadedGameSave(metadata, GameSimulation.RestoreState(state, catalog, factionCatalog));
    }

    // TEMPORARY BRIDGE (removed by leg L4): restores a V9 ship into the V9 slots: five installations with ids 1–5
    // and continuation 6, the V9 conditions and allocations verbatim (zero condition stays an installed-but-offline
    // system, never absence), the repair target and scan source mapped by kind, and the single readiness time
    // attached to the weapon slot. Definitions come from the ship's design in the supplied catalog (see the note on
    // BridgeInstalledIdV9). No class default is read. L4 moves this translation into MigrateV9ToV10, where the
    // frozen HistoricalShipSystemsV9 map and its semantics check replace the catalog lookup.
    private static ShipState RestoreShipV9(ShipSnapshotV9 snapshot, ShipDefinitionCatalog catalog)
    {
        SaveModelsV9.EngineeringSnapshotV9 engineering = snapshot.Engineering;
        SaveModelsV9.CombatStimulusSnapshotV9? stimulus = snapshot.Combat.PendingStimulus;
        string design = snapshot.DefinitionId;
        InstalledSystem Install(string kind, double condition, int? allocation) =>
            new(
                BridgeInstalledIdV9(kind),
                BridgeDefinitionV9(catalog, design, kind),
                new SystemCondition(condition),
                allocation is null ? null : new PowerUnits(allocation.Value)
            );

        var installations = InstalledSystemCollection.Create([
            Install(GenerationKind, engineering.GenerationCondition, null),
            Install(SensorsKind, engineering.SensorCondition, engineering.SensorAllocation),
            Install(ImpulseKind, engineering.ImpulseCondition, engineering.ImpulseAllocation),
            Install(ShieldsKind, engineering.ShieldCondition, engineering.ShieldAllocation),
            Install(WeaponsKind, engineering.DirectedEnergyCondition, engineering.DirectedEnergyAllocation),
        ]);
        return new ShipState(
            new ShipInstanceId(snapshot.InstanceId),
            new ShipDefinitionId(snapshot.DefinitionId),
            snapshot.DisplayName,
            new TacticalPosition(snapshot.TacticalPosition.XKilometers, snapshot.TacticalPosition.YKilometers),
            new TacticalMotion(
                new HeadingDegrees(snapshot.TacticalMotion.HeadingDegrees),
                new SpeedKilometersPerSecond(snapshot.TacticalMotion.SpeedKilometersPerSecond)
            ),
            new ShipEngineeringState(
                installations,
                InstalledSystemIdAllocator.Restore(BridgeNextInstalledSystemIdV9),
                RestoreSystemRepairV5(engineering.ActiveRepair)
            ),
            RestoreStrategicStateV2(snapshot.StrategicState),
            RestoreOrderV3(snapshot.ActiveOrder),
            RestoreSensorKnowledgeV6(snapshot.SensorKnowledge, BridgeInstalledIdV9(SensorsKind)),
            RestoreAutonomousStateV4(snapshot.AutonomousState),
            snapshot.DirectControllerFactionId is null ? null : new FactionId(snapshot.DirectControllerFactionId.Value),
            new ShipCombatState(
                [
                    new DirectedEnergyReadiness(
                        BridgeInstalledIdV9(WeaponsKind),
                        new SimulationTime(snapshot.Combat.NextDirectedEnergyReadyAtMilliseconds)
                    ),
                ],
                stimulus is null
                    ? null
                    : new CombatStimulus(
                        new SensorContactId(stimulus.ContactId),
                        new SimulationTime(stimulus.ObservedAtMilliseconds),
                        new SimulationTime(stimulus.DueTimeMilliseconds),
                        new ScheduledWorkId(stimulus.ScheduledWorkId)
                    )
            )
        );
    }

    private static void ValidateCandidateV9(SaveEnvelopeV9 envelope, ShipDefinitionCatalog catalog)
    {
        if (
            envelope.SchemaVersion != CurrentSchemaVersion
            || !string.Equals(envelope.SimulationRulesVersion, CurrentSimulationRulesVersion, StringComparison.Ordinal)
        )
            throw new InvalidOperationException("The V9 schema or simulation rules identity is unsupported.");
        if (envelope.Metadata is null || envelope.Simulation is null)
            throw new InvalidOperationException("Required V9 envelope members cannot be null.");
        SaveModelsV9.SimulationSnapshotV9 snapshot = envelope.Simulation;
        if (
            snapshot.Ships is null
            || snapshot.Factions is null
            || snapshot.Scheduler?.OutstandingWork is null
            || snapshot.StrategicMap is null
        )
            throw new InvalidOperationException("Required V9 simulation members cannot be null.");
        EnsureCount(snapshot.Ships.Length, SimulationState.MaximumShips, "ships");
        foreach (ShipSnapshotV9? ship in snapshot.Ships)
        {
            if (ship?.Engineering is null || ship.Combat is null)
                throw new InvalidOperationException("V9 ship, Engineering, and combat state cannot be null.");
            EnsureUnitInterval(ship.Engineering.ShieldCondition, "Shield condition");
            EnsureUnitInterval(ship.Engineering.DirectedEnergyCondition, "Directed-energy condition");
            if (ship.Engineering.ActiveRepair is { } repair)
            {
                ValidateRepairTimingV5(repair, snapshot.TimeMilliseconds);
                var target = ShipSystemKind.Parse(repair.TargetSystem);
                if (target == ShipSystemKind.PowerGeneration)
                    throw new InvalidOperationException("Power generation is not repairable.");
            }
        }

        // Only unchanged structural contracts use the legacy projection. New repairs and combat work
        // are validated against the complete V9 scheduler and the original graph in RestoreV9; the V8 loader
        // never receives a V9 document or accepts its new work/repair semantics.
        SaveEnvelopeV8 legacy = LegacyShapeV9(envelope);
        ValidateCandidateV8(legacy, catalog, design => BridgeTuningV9(catalog, design));
        ValidateSchedulerCandidate(
            snapshot.Scheduler,
            snapshot.TimeMilliseconds,
            snapshot.Ships.Select(ship => ship.InstanceId),
            snapshot.Factions.Select(faction => faction.Id),
            CurrentSchemaVersion
        );
    }

    private static bool IsCombatRepair(SaveModelsV5.SystemRepairSnapshotV5? repair) =>
        repair is not null
        && (
            string.Equals(repair.TargetSystem, ShipSystemKind.Shields.Value, StringComparison.Ordinal)
            || string.Equals(repair.TargetSystem, ShipSystemKind.DirectedEnergyWeapons.Value, StringComparison.Ordinal)
        );

    private static ShipSnapshotV7 LegacyShipShapeV9(ShipSnapshotV9 ship) =>
        new()
        {
            InstanceId = ship.InstanceId,
            DefinitionId = ship.DefinitionId,
            DisplayName = ship.DisplayName,
            TacticalPosition = ship.TacticalPosition,
            TacticalMotion = ship.TacticalMotion,
            Engineering = new SaveModelsV5.EngineeringSnapshotV5
            {
                GenerationCondition = ship.Engineering.GenerationCondition,
                SensorCondition = ship.Engineering.SensorCondition,
                ImpulseCondition = ship.Engineering.ImpulseCondition,
                SensorAllocation = ship.Engineering.SensorAllocation,
                ImpulseAllocation = ship.Engineering.ImpulseAllocation,
                ActiveRepair = IsCombatRepair(ship.Engineering.ActiveRepair) ? null : ship.Engineering.ActiveRepair,
            },
            StrategicState = ship.StrategicState,
            ActiveOrder = ship.ActiveOrder,
            SensorKnowledge = ship.SensorKnowledge,
            AutonomousState = ship.AutonomousState,
            DirectControllerFactionId = ship.DirectControllerFactionId,
        };

    private static SaveEnvelopeV8 LegacyShapeV9(SaveEnvelopeV9 envelope)
    {
        SaveModelsV9.SimulationSnapshotV9 source = envelope.Simulation;
        HashSet<long> combatRepairIds =
        [
            .. source
                .Ships.Where(ship => IsCombatRepair(ship.Engineering.ActiveRepair))
                .Select(ship => ship.Engineering.ActiveRepair!.ScheduledCompletionId),
        ];
        return new SaveEnvelopeV8
        {
            SchemaVersion = V8SchemaVersion,
            SimulationRulesVersion = V8SimulationRulesVersion,
            Metadata = envelope.Metadata,
            Simulation = new SaveModelsV8.SimulationSnapshotV8
            {
                TimeMilliseconds = source.TimeMilliseconds,
                ShipAllocatorNextId = source.ShipAllocatorNextId,
                OrderAllocatorNextId = source.OrderAllocatorNextId,
                ObservationReportAllocatorNextId = source.ObservationReportAllocatorNextId,
                PlayerShipId = source.PlayerShipId,
                Scheduler = new SaveModelsV7.SchedulerSnapshotV7
                {
                    NextWorkId = source.Scheduler.NextWorkId,
                    NextSequence = source.Scheduler.NextSequence,
                    OutstandingWork =
                    [
                        .. source.Scheduler.OutstandingWork.Where(work =>
                            work is null
                            || (
                                !string.Equals(work.Kind, ShipCombatDecisionWakeKind, StringComparison.Ordinal)
                                && !combatRepairIds.Contains(work.Id)
                            )
                        ),
                    ],
                },
                StrategicMap = source.StrategicMap,
                Ships = [.. source.Ships.Select(LegacyShipShapeV9)],
                Factions = source.Factions,
            },
        };
    }
}
