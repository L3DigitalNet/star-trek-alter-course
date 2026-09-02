using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlterCourse.Core.Persistence;

internal static class SaveModelsV1
{
    internal sealed class SaveEnvelopeV1
    {
        public required int SchemaVersion { get; init; }

        public required string SimulationRulesVersion { get; init; }

        public required SaveMetadataV1 Metadata { get; init; }

        public required SimulationSnapshotV1 Simulation { get; init; }
    }

    internal sealed class SaveMetadataV1
    {
        public required string SaveId { get; init; }

        public required string DisplayName { get; init; }

        public required DateTimeOffset CreatedAtUtc { get; init; }

        public required DateTimeOffset SavedAtUtc { get; init; }
    }

    internal sealed class SimulationSnapshotV1
    {
        public required long TimeMilliseconds { get; init; }

        public required long ShipAllocatorNextId { get; init; }

        public required SchedulerSnapshotV1 Scheduler { get; init; }

        public required StrategicMapSnapshotV1 StrategicMap { get; init; }

        public required StrategicStateSnapshotV1 StrategicState { get; init; }

        public required PlayerShipSnapshotV1 PlayerShip { get; init; }
    }

    internal sealed class SchedulerSnapshotV1
    {
        public required long NextWorkId { get; init; }

        public required long NextSequence { get; init; }

        public required ScheduledWorkSnapshotV1[] OutstandingWork { get; init; }
    }

    internal sealed class ScheduledWorkSnapshotV1
    {
        public required long Id { get; init; }

        public required long DueTimeMilliseconds { get; init; }

        public required long Sequence { get; init; }

        public required string Kind { get; init; }
    }

    internal sealed class StrategicMapSnapshotV1
    {
        public required StrategicLocationSnapshotV1[] Locations { get; init; }

        public required StrategicRouteSnapshotV1[] Routes { get; init; }
    }

    internal sealed class StrategicLocationSnapshotV1
    {
        public required string Id { get; init; }

        public required string DisplayName { get; init; }

        public required StrategicPositionSnapshotV1 Position { get; init; }
    }

    internal sealed class StrategicPositionSnapshotV1
    {
        public required double XUnitless { get; init; }

        public required double YUnitless { get; init; }
    }

    internal sealed class StrategicRouteSnapshotV1
    {
        public required string Origin { get; init; }

        public required string Destination { get; init; }

        public required long DurationMilliseconds { get; init; }
    }

    internal sealed class StrategicStateSnapshotV1
    {
        public required string Kind { get; init; }

        public required string? LocationId { get; init; }

        public required TravelSnapshotV1? Travel { get; init; }
    }

    internal sealed class TravelSnapshotV1
    {
        public required string Origin { get; init; }

        public required string Destination { get; init; }

        public required long DepartureMilliseconds { get; init; }

        public required long ExpectedArrivalMilliseconds { get; init; }

        public required long ScheduledArrivalId { get; init; }
    }

    internal sealed class PlayerShipSnapshotV1
    {
        public required long InstanceId { get; init; }

        public required string DefinitionId { get; init; }

        public required TacticalPositionSnapshotV1 TacticalPosition { get; init; }

        public required TacticalMotionSnapshotV1 TacticalMotion { get; init; }

        public required double SensorIntegrity { get; init; }

        public required SensorRepairSnapshotV1? SensorRepair { get; init; }
    }

    internal sealed class TacticalPositionSnapshotV1
    {
        public required double XKilometers { get; init; }

        public required double YKilometers { get; init; }
    }

    internal sealed class TacticalMotionSnapshotV1
    {
        public required double HeadingDegrees { get; init; }

        public required double SpeedKilometersPerSecond { get; init; }
    }

    internal sealed class SensorRepairSnapshotV1
    {
        public required double StartingIntegrity { get; init; }

        public required double TargetIntegrity { get; init; }

        public required long StartedAtMilliseconds { get; init; }

        public required long ExpectedCompletionMilliseconds { get; init; }

        public required long ScheduledCompletionId { get; init; }
    }

    internal sealed class FiniteDoubleJsonConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (!reader.TryGetDouble(out double value) || !double.IsFinite(value))
            {
                throw new JsonException("Numeric value must be finite.");
            }

            return value == 0 ? 0 : value;
        }

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            if (!double.IsFinite(value))
            {
                throw new JsonException("Numeric value must be finite.");
            }

            writer.WriteNumberValue(value == 0 ? 0 : value);
        }
    }
}
