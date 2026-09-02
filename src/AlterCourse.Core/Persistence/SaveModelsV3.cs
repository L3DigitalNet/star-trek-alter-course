using System.Text.Json.Serialization;
using SaveMetadataV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveMetadataV2;
using SchedulerSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SchedulerSnapshotV2;
using SensorRepairSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SensorRepairSnapshotV2;
using StrategicMapSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicMapSnapshotV2;
using StrategicStateSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicStateSnapshotV2;
using TacticalMotionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalMotionSnapshotV2;
using TacticalPositionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalPositionSnapshotV2;

namespace AlterCourse.Core.Persistence;

internal static class SaveModelsV3
{
    internal sealed class SaveEnvelopeV3
    {
        public required int SchemaVersion { get; init; }
        public required string SimulationRulesVersion { get; init; }
        public required SaveMetadataV2 Metadata { get; init; }
        public required SimulationSnapshotV3 Simulation { get; init; }
    }

    internal sealed class SimulationSnapshotV3
    {
        public required long TimeMilliseconds { get; init; }
        public required long ShipAllocatorNextId { get; init; }
        public required long OrderAllocatorNextId { get; init; }
        public required long PlayerShipId { get; init; }
        public required SchedulerSnapshotV2 Scheduler { get; init; }
        public required StrategicMapSnapshotV2 StrategicMap { get; init; }
        public required ShipSnapshotV3[] Ships { get; init; }
    }

    internal sealed class ShipSnapshotV3
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
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(TravelToOrderSnapshotV3), "travelTo")]
    [JsonDerivedType(typeof(PatrolRouteOrderSnapshotV3), "patrolRoute")]
    [JsonDerivedType(typeof(HoldUntilOrderSnapshotV3), "holdUntil")]
    internal abstract class ShipOrderSnapshotV3
    {
        [JsonPropertyOrder(1)]
        public required long Id { get; init; }
    }

    internal sealed class TravelToOrderSnapshotV3 : ShipOrderSnapshotV3
    {
        [JsonPropertyOrder(2)]
        public required string Destination { get; init; }
    }

    internal sealed class PatrolRouteOrderSnapshotV3 : ShipOrderSnapshotV3
    {
        [JsonPropertyOrder(2)]
        public required string[] Waypoints { get; init; }

        [JsonPropertyOrder(3)]
        public required int NextWaypointIndex { get; init; }
    }

    internal sealed class HoldUntilOrderSnapshotV3 : ShipOrderSnapshotV3
    {
        [JsonPropertyOrder(2)]
        public required long UntilMilliseconds { get; init; }

        [JsonPropertyOrder(3)]
        public required long ScheduledWakeId { get; init; }
    }
}
