namespace AlterCourse.Core.Persistence;

internal static class SaveModelsV2
{
    internal sealed class SaveEnvelopeV2
    {
        public required int SchemaVersion { get; init; }
        public required string SimulationRulesVersion { get; init; }
        public required SaveMetadataV2 Metadata { get; init; }
        public required SimulationSnapshotV2 Simulation { get; init; }
    }

    internal sealed class SaveMetadataV2
    {
        public required string SaveId { get; init; }
        public required string DisplayName { get; init; }
        public required DateTimeOffset CreatedAtUtc { get; init; }
        public required DateTimeOffset SavedAtUtc { get; init; }
    }

    internal sealed class SimulationSnapshotV2
    {
        public required long TimeMilliseconds { get; init; }
        public required long ShipAllocatorNextId { get; init; }
        public required long PlayerShipId { get; init; }
        public required SchedulerSnapshotV2 Scheduler { get; init; }
        public required StrategicMapSnapshotV2 StrategicMap { get; init; }
        public required ShipSnapshotV2[] Ships { get; init; }
    }

    internal sealed class SchedulerSnapshotV2
    {
        public required long NextWorkId { get; init; }
        public required long NextSequence { get; init; }
        public required ScheduledWorkSnapshotV2[] OutstandingWork { get; init; }
    }

    internal sealed class ScheduledWorkSnapshotV2
    {
        public required long Id { get; init; }
        public required long DueTimeMilliseconds { get; init; }
        public required long Sequence { get; init; }
        public required string Kind { get; init; }
        public required long TargetShipId { get; init; }
    }

    internal sealed class StrategicMapSnapshotV2
    {
        public required StrategicLocationSnapshotV2[] Locations { get; init; }
        public required StrategicRouteSnapshotV2[] Routes { get; init; }
    }

    internal sealed class StrategicLocationSnapshotV2
    {
        public required string Id { get; init; }
        public required string DisplayName { get; init; }
        public required StrategicPositionSnapshotV2 Position { get; init; }
    }

    internal sealed class StrategicPositionSnapshotV2
    {
        public required double XUnitless { get; init; }
        public required double YUnitless { get; init; }
    }

    internal sealed class StrategicRouteSnapshotV2
    {
        public required string Origin { get; init; }
        public required string Destination { get; init; }
        public required long DurationMilliseconds { get; init; }
    }

    internal sealed class ShipSnapshotV2
    {
        public required long InstanceId { get; init; }
        public required string DefinitionId { get; init; }
        public required string DisplayName { get; init; }
        public required TacticalPositionSnapshotV2 TacticalPosition { get; init; }
        public required TacticalMotionSnapshotV2 TacticalMotion { get; init; }
        public required double SensorIntegrity { get; init; }
        public required SensorRepairSnapshotV2? SensorRepair { get; init; }
        public required StrategicStateSnapshotV2 StrategicState { get; init; }
    }

    internal sealed class TacticalPositionSnapshotV2
    {
        public required double XKilometers { get; init; }
        public required double YKilometers { get; init; }
    }

    internal sealed class TacticalMotionSnapshotV2
    {
        public required double HeadingDegrees { get; init; }
        public required double SpeedKilometersPerSecond { get; init; }
    }

    internal sealed class SensorRepairSnapshotV2
    {
        public required double StartingIntegrity { get; init; }
        public required double TargetIntegrity { get; init; }
        public required long StartedAtMilliseconds { get; init; }
        public required long ExpectedCompletionMilliseconds { get; init; }
        public required long ScheduledCompletionId { get; init; }
    }

    internal sealed class StrategicStateSnapshotV2
    {
        public required string Kind { get; init; }
        public required string? LocationId { get; init; }
        public required TravelSnapshotV2? Travel { get; init; }
    }

    internal sealed class TravelSnapshotV2
    {
        public required string Origin { get; init; }
        public required string Destination { get; init; }
        public required long DepartureMilliseconds { get; init; }
        public required long ExpectedArrivalMilliseconds { get; init; }
        public required long ScheduledArrivalId { get; init; }
    }
}
