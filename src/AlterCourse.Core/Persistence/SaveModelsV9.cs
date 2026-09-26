namespace AlterCourse.Core.Persistence;

/// <summary>Declares authoritative combat additions while retaining frozen historical DTO contracts.</summary>
internal static class SaveModelsV9
{
    internal sealed class SaveEnvelopeV9
    {
        public required int SchemaVersion { get; init; }
        public required string SimulationRulesVersion { get; init; }
        public required SaveModelsV2.SaveMetadataV2 Metadata { get; init; }
        public required SimulationSnapshotV9 Simulation { get; init; }
    }

    internal sealed class SimulationSnapshotV9
    {
        public required long TimeMilliseconds { get; init; }
        public required long ShipAllocatorNextId { get; init; }
        public required long OrderAllocatorNextId { get; init; }
        public required long ObservationReportAllocatorNextId { get; init; }
        public required long PlayerShipId { get; init; }
        public required SaveModelsV7.SchedulerSnapshotV7 Scheduler { get; init; }
        public required SaveModelsV2.StrategicMapSnapshotV2 StrategicMap { get; init; }
        public required ShipSnapshotV9[] Ships { get; init; }
        public required SaveModelsV8.FactionSnapshotV8[] Factions { get; init; }
    }

    internal sealed class ShipSnapshotV9
    {
        public required long InstanceId { get; init; }
        public required string DefinitionId { get; init; }
        public required string DisplayName { get; init; }
        public required SaveModelsV2.TacticalPositionSnapshotV2 TacticalPosition { get; init; }
        public required SaveModelsV2.TacticalMotionSnapshotV2 TacticalMotion { get; init; }
        public required EngineeringSnapshotV9 Engineering { get; init; }
        public required SaveModelsV2.StrategicStateSnapshotV2 StrategicState { get; init; }
        public required SaveModelsV3.ShipOrderSnapshotV3? ActiveOrder { get; init; }
        public required SaveModelsV6.SensorKnowledgeSnapshotV6 SensorKnowledge { get; init; }
        public required SaveModelsV4.ShipAutonomousSnapshotV4 AutonomousState { get; init; }
        public required long? DirectControllerFactionId { get; init; }
        public required ShipCombatSnapshotV9 Combat { get; init; }
    }

    internal sealed class EngineeringSnapshotV9
    {
        public required double GenerationCondition { get; init; }
        public required double SensorCondition { get; init; }
        public required double ImpulseCondition { get; init; }
        public required double ShieldCondition { get; init; }
        public required double DirectedEnergyCondition { get; init; }
        public required int SensorAllocation { get; init; }
        public required int ImpulseAllocation { get; init; }
        public required int ShieldAllocation { get; init; }
        public required int DirectedEnergyAllocation { get; init; }
        public required SaveModelsV5.SystemRepairSnapshotV5? ActiveRepair { get; init; }
    }

    internal sealed class ShipCombatSnapshotV9
    {
        public required long NextDirectedEnergyReadyAtMilliseconds { get; init; }
        public required CombatStimulusSnapshotV9? PendingStimulus { get; init; }
    }

    internal sealed class CombatStimulusSnapshotV9
    {
        public required long ContactId { get; init; }
        public required long ObservedAtMilliseconds { get; init; }
        public required long DueTimeMilliseconds { get; init; }
        public required long ScheduledWorkId { get; init; }
    }
}
