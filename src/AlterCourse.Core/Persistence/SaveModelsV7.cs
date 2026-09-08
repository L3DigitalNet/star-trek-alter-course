using EngineeringSnapshotV5 = AlterCourse.Core.Persistence.SaveModelsV5.EngineeringSnapshotV5;
using SaveMetadataV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveMetadataV2;
using SensorKnowledgeSnapshotV6 = AlterCourse.Core.Persistence.SaveModelsV6.SensorKnowledgeSnapshotV6;
using ShipAutonomousSnapshotV4 = AlterCourse.Core.Persistence.SaveModelsV4.ShipAutonomousSnapshotV4;
using ShipOrderSnapshotV3 = AlterCourse.Core.Persistence.SaveModelsV3.ShipOrderSnapshotV3;
using StrategicMapSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicMapSnapshotV2;
using StrategicStateSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicStateSnapshotV2;
using TacticalMotionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalMotionSnapshotV2;
using TacticalPositionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalPositionSnapshotV2;

namespace AlterCourse.Core.Persistence;

/// <summary>Declares the V7 save contract for faction intent, control, and typed scheduled-work owners.</summary>
internal static class SaveModelsV7
{
    internal sealed class SaveEnvelopeV7
    {
        public required int SchemaVersion { get; init; }
        public required string SimulationRulesVersion { get; init; }
        public required SaveMetadataV2 Metadata { get; init; }
        public required SimulationSnapshotV7 Simulation { get; init; }
    }

    internal sealed class SimulationSnapshotV7
    {
        public required long TimeMilliseconds { get; init; }
        public required long ShipAllocatorNextId { get; init; }
        public required long OrderAllocatorNextId { get; init; }
        public required long PlayerShipId { get; init; }
        public required SchedulerSnapshotV7 Scheduler { get; init; }
        public required StrategicMapSnapshotV2 StrategicMap { get; init; }
        public required ShipSnapshotV7[] Ships { get; init; }
        public required FactionSnapshotV7[] Factions { get; init; }
    }

    internal sealed class SchedulerSnapshotV7
    {
        public required long NextWorkId { get; init; }
        public required long NextSequence { get; init; }
        public required ScheduledWorkSnapshotV7[] OutstandingWork { get; init; }
    }

    internal sealed class ScheduledWorkSnapshotV7
    {
        public required long Id { get; init; }
        public required long DueTimeMilliseconds { get; init; }
        public required long Sequence { get; init; }
        public required string Kind { get; init; }
        public required string TargetKind { get; init; }
        public required long? TargetShipId { get; init; }
        public required long? TargetFactionId { get; init; }
    }

    internal sealed class ShipSnapshotV7
    {
        public required long InstanceId { get; init; }
        public required string DefinitionId { get; init; }
        public required string DisplayName { get; init; }
        public required TacticalPositionSnapshotV2 TacticalPosition { get; init; }
        public required TacticalMotionSnapshotV2 TacticalMotion { get; init; }
        public required EngineeringSnapshotV5 Engineering { get; init; }
        public required StrategicStateSnapshotV2 StrategicState { get; init; }
        public required ShipOrderSnapshotV3? ActiveOrder { get; init; }
        public required SensorKnowledgeSnapshotV6 SensorKnowledge { get; init; }
        public required ShipAutonomousSnapshotV4 AutonomousState { get; init; }
        public required long? DirectControllerFactionId { get; init; }
    }

    internal sealed class FactionSnapshotV7
    {
        public required long Id { get; init; }
        public required string DefinitionId { get; init; }
        public required EstablishPresenceObjectiveSnapshotV7? PresenceObjective { get; init; }
        public required PendingFactionDecisionWakeSnapshotV7? PendingDecisionWake { get; init; }
    }

    internal sealed class EstablishPresenceObjectiveSnapshotV7
    {
        public required string TargetLocationId { get; init; }
        public required string Status { get; init; }
        public required long? AssignedShipId { get; init; }
        public required long? AssignedOrderId { get; init; }
    }

    internal sealed class PendingFactionDecisionWakeSnapshotV7
    {
        public required long WorkId { get; init; }
        public required long DueTimeMilliseconds { get; init; }
    }
}
