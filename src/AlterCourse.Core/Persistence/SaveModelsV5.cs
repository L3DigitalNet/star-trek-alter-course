using SaveMetadataV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveMetadataV2;
using SchedulerSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SchedulerSnapshotV2;
using SensorKnowledgeSnapshotV4 = AlterCourse.Core.Persistence.SaveModelsV4.SensorKnowledgeSnapshotV4;
using ShipAutonomousSnapshotV4 = AlterCourse.Core.Persistence.SaveModelsV4.ShipAutonomousSnapshotV4;
using ShipOrderSnapshotV3 = AlterCourse.Core.Persistence.SaveModelsV3.ShipOrderSnapshotV3;
using StrategicMapSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicMapSnapshotV2;
using StrategicStateSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicStateSnapshotV2;
using TacticalMotionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalMotionSnapshotV2;
using TacticalPositionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalPositionSnapshotV2;

namespace AlterCourse.Core.Persistence;

internal static class SaveModelsV5
{
    internal sealed class SaveEnvelopeV5
    {
        public required int SchemaVersion { get; init; }
        public required string SimulationRulesVersion { get; init; }
        public required SaveMetadataV2 Metadata { get; init; }
        public required SimulationSnapshotV5 Simulation { get; init; }
    }

    internal sealed class SimulationSnapshotV5
    {
        public required long TimeMilliseconds { get; init; }
        public required long ShipAllocatorNextId { get; init; }
        public required long OrderAllocatorNextId { get; init; }
        public required long PlayerShipId { get; init; }
        public required SchedulerSnapshotV2 Scheduler { get; init; }
        public required StrategicMapSnapshotV2 StrategicMap { get; init; }
        public required ShipSnapshotV5[] Ships { get; init; }
    }

    internal sealed class ShipSnapshotV5
    {
        public required long InstanceId { get; init; }
        public required string DefinitionId { get; init; }
        public required string DisplayName { get; init; }
        public required TacticalPositionSnapshotV2 TacticalPosition { get; init; }
        public required TacticalMotionSnapshotV2 TacticalMotion { get; init; }
        public required EngineeringSnapshotV5 Engineering { get; init; }
        public required StrategicStateSnapshotV2 StrategicState { get; init; }
        public required ShipOrderSnapshotV3? ActiveOrder { get; init; }
        public required SensorKnowledgeSnapshotV4 SensorKnowledge { get; init; }
        public required ShipAutonomousSnapshotV4 AutonomousState { get; init; }
    }

    internal sealed class EngineeringSnapshotV5
    {
        public required double GenerationCondition { get; init; }
        public required double SensorCondition { get; init; }
        public required double ImpulseCondition { get; init; }
        public required int SensorAllocation { get; init; }
        public required int ImpulseAllocation { get; init; }
        public required SystemRepairSnapshotV5? ActiveRepair { get; init; }
    }

    internal sealed class SystemRepairSnapshotV5
    {
        public required string TargetSystem { get; init; }
        public required double StartingCondition { get; init; }
        public required double TargetCondition { get; init; }
        public required long StartedAtMilliseconds { get; init; }
        public required long ExpectedCompletionMilliseconds { get; init; }
        public required long ScheduledCompletionId { get; init; }
    }
}
