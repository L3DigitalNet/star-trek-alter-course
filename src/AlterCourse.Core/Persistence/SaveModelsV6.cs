using ActiveSensorScanSnapshotV4 = AlterCourse.Core.Persistence.SaveModelsV4.ActiveSensorScanSnapshotV4;
using EngineeringSnapshotV5 = AlterCourse.Core.Persistence.SaveModelsV5.EngineeringSnapshotV5;
using SaveMetadataV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveMetadataV2;
using SchedulerSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SchedulerSnapshotV2;
using ShipAutonomousSnapshotV4 = AlterCourse.Core.Persistence.SaveModelsV4.ShipAutonomousSnapshotV4;
using ShipOrderSnapshotV3 = AlterCourse.Core.Persistence.SaveModelsV3.ShipOrderSnapshotV3;
using StrategicMapSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicMapSnapshotV2;
using StrategicStateSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicStateSnapshotV2;
using TacticalMotionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalMotionSnapshotV2;
using TacticalPositionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalPositionSnapshotV2;

namespace AlterCourse.Core.Persistence;

/// <summary>
/// Declares the V6 save contract: the V5 shape with every sensor contact qualified by the strategic
/// location its observation was recorded in.
/// </summary>
/// <remarks>
/// Only the envelope, simulation, ship, and sensor-knowledge nodes are redeclared, because those are
/// the nodes on the path to the changed contact record. Everything else is aliased forward from its
/// owning historical file, exactly as <c>SaveModelsV5</c> aliases the shapes V5 did not change; the
/// alias keeps one declaration of each unchanged wire shape so a future edit cannot make two schema
/// versions disagree about a member that never changed.
/// </remarks>
internal static class SaveModelsV6
{
    internal sealed class SaveEnvelopeV6
    {
        public required int SchemaVersion { get; init; }
        public required string SimulationRulesVersion { get; init; }
        public required SaveMetadataV2 Metadata { get; init; }
        public required SimulationSnapshotV6 Simulation { get; init; }
    }

    internal sealed class SimulationSnapshotV6
    {
        public required long TimeMilliseconds { get; init; }
        public required long ShipAllocatorNextId { get; init; }
        public required long OrderAllocatorNextId { get; init; }
        public required long PlayerShipId { get; init; }
        public required SchedulerSnapshotV2 Scheduler { get; init; }
        public required StrategicMapSnapshotV2 StrategicMap { get; init; }
        public required ShipSnapshotV6[] Ships { get; init; }
    }

    internal sealed class ShipSnapshotV6
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
    }

    internal sealed class SensorKnowledgeSnapshotV6
    {
        public required long NextContactId { get; init; }
        public required SensorContactSnapshotV6[] Contacts { get; init; }
        public required ActiveSensorScanSnapshotV4? ActiveScan { get; init; }
    }

    internal sealed class SensorContactSnapshotV6
    {
        public required long Id { get; init; }
        public required long TargetShipId { get; init; }
        public required TacticalPositionSnapshotV2 LastObservedPosition { get; init; }
        public required long LastObservedAtMilliseconds { get; init; }

        /// <summary>
        /// Carries the strategic location the observation was recorded in as a plain location-identity
        /// string, the same wire convention as <c>StrategicStateSnapshotV2.LocationId</c>.
        /// </summary>
        /// <remarks>
        /// Null means an explicitly unqualified legacy observation migrated from a schema that never
        /// recorded a frame; it is not "unknown location" and no live observation path can produce it.
        /// The member is <c>required</c> so a new writer cannot silently omit the frame, and nullable so
        /// the V5 migration can be honest instead of inventing one.
        /// </remarks>
        public required string? ObservedAtLocationId { get; init; }
        public required string Status { get; init; }
        public required string Identification { get; init; }
        public required string? KnownVesselDisplayName { get; init; }
        public required string? KnownDesignDisplayName { get; init; }
        public required long? LossWorkId { get; init; }
        public required long? LossDueTimeMilliseconds { get; init; }
    }
}
