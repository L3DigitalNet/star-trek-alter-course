using System.Text.Json;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Ships;
using Microsoft.Extensions.Logging;
using SaveEnvelopeV8 = AlterCourse.Core.Persistence.SaveModelsV8.SaveEnvelopeV8;
using SaveEnvelopeV9 = AlterCourse.Core.Persistence.SaveModelsV9.SaveEnvelopeV9;
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

    // Fixed V9 labels. The V8→V9 migration, V9 validation, and V9 scheduler rules name these literally rather than
    // the current aliases, so adding a later schema can never relabel a V9 document or change which work kinds V9
    // admits.
    private const int V9SchemaVersion = 9;
    private const string V9SimulationRulesVersion = "first-combat-engagement-v1";

    private static SaveEnvelopeV9 MigrateV8ToV9(SaveEnvelopeV8 envelope) =>
        new()
        {
            SchemaVersion = V9SchemaVersion,
            SimulationRulesVersion = V9SimulationRulesVersion,
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

    private static LoadedGameSave LoadV9(
        byte[] json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity,
        ILogger<GameSimulation>? logger
    )
    {
        try
        {
            SaveEnvelopeV9 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV9>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            return RestoreThroughV10(envelope, catalog, factionCatalog, logger);
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

    private static void ValidateCandidateV9(SaveEnvelopeV9 envelope, ShipDefinitionCatalog catalog)
    {
        if (
            envelope.SchemaVersion != V9SchemaVersion
            || !string.Equals(envelope.SimulationRulesVersion, V9SimulationRulesVersion, StringComparison.Ordinal)
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
        // are validated against the complete V9 scheduler here and, after V9→V10 migration, by the runtime in
        // RestoreV10; the V8 loader never receives a V9 document or accepts its new work/repair semantics. The legacy
        // pass applies the frozen historical tuning, so a V9 ship whose design has no frozen row fails closed.
        SaveEnvelopeV8 legacy = LegacyShapeV9(envelope);
        ValidateCandidateV8(legacy, catalog);
        ValidateSchedulerCandidate(
            snapshot.Scheduler,
            snapshot.TimeMilliseconds,
            snapshot.Ships.Select(ship => ship.InstanceId),
            snapshot.Factions.Select(faction => faction.Id),
            V9SchemaVersion
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
