namespace AlterCourse.Core.Persistence;

/// <summary>
/// Declares the V10 save contract: installed ship systems keyed by ship-local identity, replacing V9's fixed
/// per-kind engineering and combat fields. Frozen once released; a later change is a new schema version.
/// </summary>
/// <remarks>
/// <para>
/// Only authoritative state is persisted: installations with their definition reference, condition, and exact
/// allocation (present iff the definition consumes power), the installed-identity allocator continuation, the
/// repair target, the scan source, and per-weapon readiness. Labels, kinds, capability, totals, and rows are derived
/// on load and never written. Every array is emitted in canonical order (ships and installations ascending by id,
/// definitions ascending ordinal) and validated as such, so re-serialization is byte-stable.
/// </para>
/// <para>
/// <c>SystemDefinitions</c> and <c>AimVocabulary</c> are compatibility descriptors, not state: they pin what every
/// referenced definition and the catalog-derived aim-kind vocabulary meant when the save was written, so a load under
/// changed content fails closed instead of silently reinterpreting the world. Unchanged subrecords of other domains
/// reuse their earlier DTOs.
/// </para>
/// </remarks>
internal static class SaveModelsV10
{
    internal sealed class SaveEnvelopeV10
    {
        public required int SchemaVersion { get; init; }
        public required string SimulationRulesVersion { get; init; }
        public required SaveModelsV2.SaveMetadataV2 Metadata { get; init; }
        public required SimulationSnapshotV10 Simulation { get; init; }
    }

    internal sealed class SimulationSnapshotV10
    {
        public required long TimeMilliseconds { get; init; }
        public required long ShipAllocatorNextId { get; init; }
        public required long OrderAllocatorNextId { get; init; }
        public required long ObservationReportAllocatorNextId { get; init; }
        public required long PlayerShipId { get; init; }
        public required SaveModelsV7.SchedulerSnapshotV7 Scheduler { get; init; }
        public required SaveModelsV2.StrategicMapSnapshotV2 StrategicMap { get; init; }
        public required SystemDefinitionReferenceSnapshotV10[] SystemDefinitions { get; init; }
        public required string AimVocabulary { get; init; }
        public required ShipSnapshotV10[] Ships { get; init; }
        public required SaveModelsV8.FactionSnapshotV8[] Factions { get; init; }
    }

    internal sealed class SystemDefinitionReferenceSnapshotV10
    {
        public required string DefinitionId { get; init; }
        public required string Semantics { get; init; }
    }

    internal sealed class ShipSnapshotV10
    {
        public required long InstanceId { get; init; }
        public required string DefinitionId { get; init; }
        public required string DisplayName { get; init; }
        public required SaveModelsV2.TacticalPositionSnapshotV2 TacticalPosition { get; init; }
        public required SaveModelsV2.TacticalMotionSnapshotV2 TacticalMotion { get; init; }
        public required EngineeringSnapshotV10 Engineering { get; init; }
        public required SaveModelsV2.StrategicStateSnapshotV2 StrategicState { get; init; }
        public required SaveModelsV3.ShipOrderSnapshotV3? ActiveOrder { get; init; }
        public required SensorKnowledgeSnapshotV10 SensorKnowledge { get; init; }
        public required SaveModelsV4.ShipAutonomousSnapshotV4 AutonomousState { get; init; }
        public required long? DirectControllerFactionId { get; init; }
        public required ShipCombatSnapshotV10 Combat { get; init; }
    }

    internal sealed class EngineeringSnapshotV10
    {
        public required long NextInstalledSystemId { get; init; }
        public required InstalledSystemSnapshotV10[] InstalledSystems { get; init; }
        public required SystemRepairSnapshotV10? ActiveRepair { get; init; }
    }

    internal sealed class InstalledSystemSnapshotV10
    {
        public required long InstalledSystemId { get; init; }
        public required string DefinitionId { get; init; }
        public required double Condition { get; init; }

        // A required member whose null is meaningful: null exactly for a definition without power demand, so a
        // missing member (malformed) stays distinguishable from a nonconsumer (valid).
        public required int? Allocation { get; init; }
    }

    internal sealed class SystemRepairSnapshotV10
    {
        public required long TargetInstalledSystemId { get; init; }
        public required double StartingCondition { get; init; }
        public required double TargetCondition { get; init; }
        public required long StartedAtMilliseconds { get; init; }
        public required long ExpectedCompletionMilliseconds { get; init; }
        public required long ScheduledCompletionId { get; init; }
    }

    internal sealed class SensorKnowledgeSnapshotV10
    {
        public required long NextContactId { get; init; }
        public required SaveModelsV6.SensorContactSnapshotV6[] Contacts { get; init; }
        public required ActiveSensorScanSnapshotV10? ActiveScan { get; init; }
    }

    internal sealed class ActiveSensorScanSnapshotV10
    {
        public required long TargetContactId { get; init; }
        public required long SensorInstalledSystemId { get; init; }
        public required long StartedAtMilliseconds { get; init; }
        public required long ExpectedCompletionMilliseconds { get; init; }
        public required long ScheduledCompletionId { get; init; }
    }

    internal sealed class ShipCombatSnapshotV10
    {
        public required DirectedEnergyReadinessSnapshotV10[] DirectedEnergyReadiness { get; init; }
        public required SaveModelsV9.CombatStimulusSnapshotV9? PendingStimulus { get; init; }
    }

    internal sealed class DirectedEnergyReadinessSnapshotV10
    {
        public required long WeaponInstalledSystemId { get; init; }
        public required long ReadyAtMilliseconds { get; init; }
    }
}
