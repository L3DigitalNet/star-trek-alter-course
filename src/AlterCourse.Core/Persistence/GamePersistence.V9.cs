using System.Text.Json;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using SaveEnvelopeV8 = AlterCourse.Core.Persistence.SaveModelsV8.SaveEnvelopeV8;
using SaveEnvelopeV9 = AlterCourse.Core.Persistence.SaveModelsV9.SaveEnvelopeV9;
using ShipSnapshotV7 = AlterCourse.Core.Persistence.SaveModelsV7.ShipSnapshotV7;
using ShipSnapshotV9 = AlterCourse.Core.Persistence.SaveModelsV9.ShipSnapshotV9;

namespace AlterCourse.Core.Persistence;

public static partial class GamePersistence
{
    private static SaveEnvelopeV9 CaptureV9(SimulationState state, GameSaveMetadata metadata)
    {
        SaveEnvelopeV8 legacy = CaptureV8(state, metadata);
        SaveEnvelopeV9 current = MigrateV8ToV9(legacy);
        return new SaveEnvelopeV9
        {
            SchemaVersion = current.SchemaVersion,
            SimulationRulesVersion = current.SimulationRulesVersion,
            Metadata = current.Metadata,
            Simulation = CopySnapshotV9(
                current.Simulation,
                [
                    .. state
                        .Ships.OrderBy(ship => ship.InstanceId.Value)
                        .Select(ship =>
                        {
                            ShipSnapshotV9 snapshot = UpgradeShipV8(CaptureShipV7(ship));
                            return CopyShipV9(
                                snapshot,
                                CaptureEngineeringV9(ship),
                                new SaveModelsV9.ShipCombatSnapshotV9
                                {
                                    NextDirectedEnergyReadyAtMilliseconds = ship.Combat
                                        .NextDirectedEnergyReadyAt
                                        .Milliseconds,
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
                        }),
                ]
            ),
        };
    }

    private static SaveModelsV9.EngineeringSnapshotV9 CaptureEngineeringV9(ShipState ship) =>
        new()
        {
            GenerationCondition = ship.Engineering.GenerationCondition.Value,
            SensorCondition = ship.Engineering.SensorCondition.Value,
            ImpulseCondition = ship.Engineering.ImpulseCondition.Value,
            ShieldCondition = ship.Engineering.ShieldCondition.Value,
            DirectedEnergyCondition = ship.Engineering.DirectedEnergyCondition.Value,
            SensorAllocation = ship.Engineering.Allocation.Sensors.Value,
            ImpulseAllocation = ship.Engineering.Allocation.ImpulsePropulsion.Value,
            ShieldAllocation = ship.Engineering.Allocation.Shields.Value,
            DirectedEnergyAllocation = ship.Engineering.Allocation.DirectedEnergyWeapons.Value,
            ActiveRepair = CaptureShipV7(ship).Engineering.ActiveRepair,
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
            snapshot.Ships.Select(RestoreShipV9),
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

    private static ShipState RestoreShipV9(ShipSnapshotV9 snapshot)
    {
        ShipState legacy = RestoreShipV7(LegacyShipShapeV9(snapshot));
        SaveModelsV9.EngineeringSnapshotV9 engineering = snapshot.Engineering;
        SaveModelsV9.CombatStimulusSnapshotV9? stimulus = snapshot.Combat.PendingStimulus;
        return legacy with
        {
            Engineering = new ShipEngineeringState(
                new SystemCondition(engineering.GenerationCondition),
                new SystemCondition(engineering.SensorCondition),
                new SystemCondition(engineering.ImpulseCondition),
                new SystemCondition(engineering.ShieldCondition),
                new SystemCondition(engineering.DirectedEnergyCondition),
                new PowerAllocation(
                    new PowerUnits(engineering.SensorAllocation),
                    new PowerUnits(engineering.ImpulseAllocation),
                    new PowerUnits(engineering.ShieldAllocation),
                    new PowerUnits(engineering.DirectedEnergyAllocation)
                ),
                RestoreSystemRepairV5(engineering.ActiveRepair)
            ),
            Combat = new ShipCombatState(
                new SimulationTime(snapshot.Combat.NextDirectedEnergyReadyAtMilliseconds),
                stimulus is null
                    ? null
                    : new CombatStimulus(
                        new SensorContactId(stimulus.ContactId),
                        new SimulationTime(stimulus.ObservedAtMilliseconds),
                        new SimulationTime(stimulus.DueTimeMilliseconds),
                        new ScheduledWorkId(stimulus.ScheduledWorkId)
                    )
            ),
        };
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
                var target = ShipSystemId.Parse(repair.TargetSystem);
                if (target == ShipSystemId.PowerGeneration)
                    throw new InvalidOperationException("Power generation is not repairable.");
            }
        }

        // Only unchanged structural contracts use the legacy projection. New repairs and combat work
        // are validated against the complete V9 scheduler and the original graph in RestoreV9; the V8 loader
        // never receives a V9 document or accepts its new work/repair semantics.
        SaveEnvelopeV8 legacy = LegacyShapeV9(envelope);
        ValidateCandidateV8(legacy, catalog);
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
            string.Equals(repair.TargetSystem, ShipSystemId.Shields.Value, StringComparison.Ordinal)
            || string.Equals(repair.TargetSystem, ShipSystemId.DirectedEnergyWeapons.Value, StringComparison.Ordinal)
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
