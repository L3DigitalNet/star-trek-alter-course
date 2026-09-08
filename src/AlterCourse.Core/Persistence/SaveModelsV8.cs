using SaveMetadataV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveMetadataV2;
using StrategicMapSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicMapSnapshotV2;
using TacticalPositionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalPositionSnapshotV2;

namespace AlterCourse.Core.Persistence;

/// <summary>Declares the V8 save contract for bounded observation reports and faction response.</summary>
internal static class SaveModelsV8
{
    internal sealed class SaveEnvelopeV8
    {
        public required int SchemaVersion { get; init; }
        public required string SimulationRulesVersion { get; init; }
        public required SaveMetadataV2 Metadata { get; init; }
        public required SimulationSnapshotV8 Simulation { get; init; }
    }

    internal sealed class SimulationSnapshotV8
    {
        public required long TimeMilliseconds { get; init; }
        public required long ShipAllocatorNextId { get; init; }
        public required long OrderAllocatorNextId { get; init; }
        public required long ObservationReportAllocatorNextId { get; init; }
        public required long PlayerShipId { get; init; }
        public required SaveModelsV7.SchedulerSnapshotV7 Scheduler { get; init; }
        public required StrategicMapSnapshotV2 StrategicMap { get; init; }
        public required SaveModelsV7.ShipSnapshotV7[] Ships { get; init; }
        public required FactionSnapshotV8[] Factions { get; init; }
    }

    internal sealed class FactionSnapshotV8
    {
        public required long Id { get; init; }
        public required string DefinitionId { get; init; }
        public required SaveModelsV7.EstablishPresenceObjectiveSnapshotV7? PresenceObjective { get; init; }
        public required SaveModelsV7.PendingFactionDecisionWakeSnapshotV7? PendingDecisionWake { get; init; }
        public required FactionObservationSnapshotV8 Observation { get; init; }
    }

    internal sealed class FactionObservationSnapshotV8
    {
        public required string Posture { get; init; }
        public required ObservationReportInFlightSnapshotV8[] InFlightReports { get; init; }
        public required ReceivedObservationReportSnapshotV8[] ReceivedReports { get; init; }
        public required ActiveFactionInvestigationSnapshotV8? ActiveInvestigation { get; init; }
        public required ObservationLocationCompletionWatermarkSnapshotV8[] CompletionWatermarks { get; init; }
    }

    internal sealed class ObservationReportSnapshotV8
    {
        public required long ReportId { get; init; }
        public required long ObserverShipId { get; init; }
        public required long ObserverContactId { get; init; }
        public required string ObservedAtLocationId { get; init; }
        public required TacticalPositionSnapshotV2 ObservedPosition { get; init; }
        public required long ObservedAtMilliseconds { get; init; }
        public required string Identification { get; init; }
        public required string? KnownVesselDisplayName { get; init; }
        public required string? KnownDesignDisplayName { get; init; }
    }

    internal sealed class ObservationReportInFlightSnapshotV8
    {
        public required ObservationReportSnapshotV8 Report { get; init; }
        public required long DeliveryWorkId { get; init; }
        public required long DueTimeMilliseconds { get; init; }
    }

    internal sealed class ReceivedObservationReportSnapshotV8
    {
        public required ObservationReportSnapshotV8 Report { get; init; }
        public required long ReceivedAtMilliseconds { get; init; }
        public required string Handling { get; init; }
    }

    internal sealed class ActiveFactionInvestigationSnapshotV8
    {
        public required ObservationReportSnapshotV8 SourceReport { get; init; }
        public required long ResponderShipId { get; init; }
        public required string OriginLocationId { get; init; }
        public required string DestinationLocationId { get; init; }
        public required long SourceReceivedAtMilliseconds { get; init; }
        public required long AssignedAtMilliseconds { get; init; }
        public required long OrderId { get; init; }
    }

    internal sealed class ObservationLocationCompletionWatermarkSnapshotV8
    {
        public required string LocationId { get; init; }
        public required long ObservedThroughMilliseconds { get; init; }
    }
}
