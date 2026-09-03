using SaveMetadataV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveMetadataV2;
using SchedulerSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SchedulerSnapshotV2;
using SensorRepairSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SensorRepairSnapshotV2;
using ShipOrderSnapshotV3 = AlterCourse.Core.Persistence.SaveModelsV3.ShipOrderSnapshotV3;
using StrategicMapSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicMapSnapshotV2;
using StrategicStateSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicStateSnapshotV2;
using TacticalMotionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalMotionSnapshotV2;
using TacticalPositionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalPositionSnapshotV2;

namespace AlterCourse.Core.Persistence;

internal static class SaveModelsV4
{
    internal sealed class SaveEnvelopeV4
    {
        public required int SchemaVersion { get; init; }
        public required string SimulationRulesVersion { get; init; }
        public required SaveMetadataV2 Metadata { get; init; }
        public required SimulationSnapshotV4 Simulation { get; init; }
    }

    internal sealed class SimulationSnapshotV4
    {
        public required long TimeMilliseconds { get; init; }
        public required long ShipAllocatorNextId { get; init; }
        public required long OrderAllocatorNextId { get; init; }
        public required long PlayerShipId { get; init; }
        public required SchedulerSnapshotV2 Scheduler { get; init; }
        public required StrategicMapSnapshotV2 StrategicMap { get; init; }
        public required ShipSnapshotV4[] Ships { get; init; }
    }

    internal sealed class ShipSnapshotV4
    {
        public required long InstanceId { get; init; }
        public required string DefinitionId { get; init; }
        public required string DisplayName { get; init; }
        public required TacticalPositionSnapshotV2 TacticalPosition { get; init; }
        public required TacticalMotionSnapshotV2 TacticalMotion { get; init; }
        public required double SensorIntegrity { get; init; }
        public required SensorRepairSnapshotV2? SensorRepair { get; init; }
        public required StrategicStateSnapshotV2 StrategicState { get; init; }
        public required ShipOrderSnapshotV3? ActiveOrder { get; init; }
        public required SensorKnowledgeSnapshotV4 SensorKnowledge { get; init; }
        public required ShipAutonomousSnapshotV4 AutonomousState { get; init; }
    }

    internal sealed class SensorKnowledgeSnapshotV4
    {
        public required long NextContactId { get; init; }
        public required SensorContactSnapshotV4[] Contacts { get; init; }
        public required ActiveSensorScanSnapshotV4? ActiveScan { get; init; }
    }

    internal sealed class SensorContactSnapshotV4
    {
        public required long Id { get; init; }
        public required long TargetShipId { get; init; }
        public required TacticalPositionSnapshotV2 LastObservedPosition { get; init; }
        public required long LastObservedAtMilliseconds { get; init; }
        public required string Status { get; init; }
        public required string Identification { get; init; }
        public required string? KnownVesselDisplayName { get; init; }
        public required string? KnownDesignDisplayName { get; init; }
        public required long? LossWorkId { get; init; }
        public required long? LossDueTimeMilliseconds { get; init; }
    }

    internal sealed class ActiveSensorScanSnapshotV4
    {
        public required long TargetContactId { get; init; }
        public required long StartedAtMilliseconds { get; init; }
        public required long ExpectedCompletionMilliseconds { get; init; }
        public required long ScheduledCompletionId { get; init; }
    }

    internal sealed class ShipAutonomousSnapshotV4
    {
        public required string? ContactPosture { get; init; }
        public required ContactDecisionWakeSnapshotV4? PendingContactDecisionWake { get; init; }
    }

    internal sealed class ContactDecisionWakeSnapshotV4
    {
        public required long ScheduledWorkId { get; init; }
        public required long DueTimeMilliseconds { get; init; }
    }
}
