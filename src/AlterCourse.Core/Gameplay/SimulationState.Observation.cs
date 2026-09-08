using AlterCourse.Core.AI;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

internal sealed partial record SimulationState
{
    private void ValidateFactionObservation(FactionState faction)
    {
        FactionObservationState observation = faction.Observation ?? new FactionObservationState();
        if (!Enum.IsDefined(observation.Posture))
        {
            throw new InvalidOperationException("Faction observation posture is unsupported.");
        }
        if (
            observation.Posture == ObservationResponsePosture.Disabled
            && (
                observation.InFlightReports.Length != 0
                || observation.ReceivedReports.Length != 0
                || observation.ActiveInvestigation is not null
                || observation.CompletionWatermarks.Length != 0
            )
        )
        {
            throw new InvalidOperationException("A disabled faction cannot retain observation-response state.");
        }

        ValidateInFlightReports(faction, observation);
        ValidateReceivedReports(faction, observation);
        ValidateCompletionWatermarks(observation);
        if (observation.ActiveInvestigation is { } active)
        {
            ValidateActiveInvestigation(faction, observation, active);
        }
    }

    private void ValidateInFlightReports(FactionState faction, FactionObservationState observation)
    {
        var deliveryWorkIds = new HashSet<ScheduledWorkId>();
        foreach (ObservationReportInFlight inFlight in observation.InFlightReports)
        {
            ValidateReport(faction, inFlight.Report);
            SimulationTime expectedDue = inFlight.Report.ObservedAt.AdvanceBy(
                new SimulationDuration(FactionObservationState.ReportDeliveryDelayMilliseconds)
            );
            if (
                !deliveryWorkIds.Add(inFlight.DeliveryWorkId)
                || inFlight.DueTime != expectedDue
                || !Scheduler.ContainsExact(
                    inFlight.DeliveryWorkId,
                    ScheduledWorkTarget.ForFaction(faction.Id),
                    inFlight.DueTime,
                    ScheduledWorkKind.ObservationReportDelivery
                )
            )
            {
                throw new InvalidOperationException("An in-flight report lacks its exact delayed delivery authority.");
            }
        }
    }

    private void ValidateReceivedReports(FactionState faction, FactionObservationState observation)
    {
        foreach (ReceivedObservationReport received in observation.ReceivedReports)
        {
            ValidateReport(faction, received.Report);
            if (
                received.ReceivedAt
                    != received.Report.ObservedAt.AdvanceBy(
                        new SimulationDuration(FactionObservationState.ReportDeliveryDelayMilliseconds)
                    )
                || received.ReceivedAt.Milliseconds > Time.Milliseconds
                || !Enum.IsDefined(received.Handling)
            )
            {
                throw new InvalidOperationException("A received report has invalid receipt time or handling state.");
            }
            if (
                received.Handling == ObservationReportHandling.Handled
                && !observation.CompletionWatermarks.Any(watermark =>
                    watermark.LocationId == received.Report.ObservedAtLocationId
                    && watermark.ObservedThrough.Milliseconds >= received.ReceivedAt.Milliseconds
                )
            )
            {
                throw new InvalidOperationException("A handled report requires its covering completion watermark.");
            }
        }
    }

    private void ValidateCompletionWatermarks(FactionObservationState observation)
    {
        foreach (ObservationLocationCompletionWatermark watermark in observation.CompletionWatermarks)
        {
            StrategicMap.GetLocation(watermark.LocationId);
            if (
                watermark.ObservedThrough.Milliseconds < FactionObservationState.ReportDeliveryDelayMilliseconds
                || watermark.ObservedThrough.Milliseconds > Time.Milliseconds
            )
            {
                throw new InvalidOperationException("A completion watermark has an impossible completion time.");
            }
            bool witnessed =
                observation.InFlightReports.Any(item =>
                    item.Report.ObservedAtLocationId == watermark.LocationId
                    && item.Report.ObservedAt.Milliseconds <= watermark.ObservedThrough.Milliseconds
                )
                || observation.ReceivedReports.Any(item =>
                    item.Report.ObservedAtLocationId == watermark.LocationId
                    && item.Report.ObservedAt.Milliseconds <= watermark.ObservedThrough.Milliseconds
                );
            if (!witnessed)
            {
                throw new InvalidOperationException("A completion watermark requires a retained report witness.");
            }
        }
    }

    private void ValidateReport(FactionState recipient, ObservationReportSnapshot report)
    {
        ShipState source;
        try
        {
            source = GetRequiredShip(report.ObserverShipId);
        }
        catch (KeyNotFoundException exception)
        {
            throw new InvalidOperationException("An observation report source must exist.", exception);
        }
        if (source.InstanceId == PlayerShipId || source.DirectControllerFactionId != recipient.Id)
        {
            throw new InvalidOperationException("An observation report requires an authorized NPC source.");
        }
        if (report.ObserverContactId.Value >= source.SensorKnowledge.NextContactId)
        {
            throw new InvalidOperationException("A report contact must precede its source observer's local allocator.");
        }
        StrategicMap.GetLocation(report.ObservedAtLocationId);
        if (
            report.ObservedAt.Milliseconds > Time.Milliseconds
            || !double.IsFinite(report.ObservedPosition.XKilometers)
            || !double.IsFinite(report.ObservedPosition.YKilometers)
        )
        {
            throw new InvalidOperationException("An observation report contains impossible historical facts.");
        }
        bool validIdentification = report.Identification switch
        {
            SensorContactIdentification.Detected => report.KnownVesselDisplayName is null
                && report.KnownDesignDisplayName is null,
            SensorContactIdentification.Identified => !string.IsNullOrWhiteSpace(report.KnownVesselDisplayName)
                && !string.IsNullOrWhiteSpace(report.KnownDesignDisplayName)
                && report.KnownVesselDisplayName.Length <= ShipState.MaximumVesselDisplayNameLength
                && report.KnownDesignDisplayName.Length <= ShipDefinition.MaximumDesignDisplayNameLength,
            _ => false,
        };
        if (!validIdentification)
        {
            throw new InvalidOperationException("An observation report contains invalid learned identification facts.");
        }
    }

    private void ValidateActiveInvestigation(
        FactionState faction,
        FactionObservationState observation,
        ActiveFactionInvestigation active
    )
    {
        ValidateReport(faction, active.SourceReport);
        if (
            active.SourceReceivedAt
            != active.SourceReport.ObservedAt.AdvanceBy(
                new SimulationDuration(FactionObservationState.ReportDeliveryDelayMilliseconds)
            )
        )
        {
            throw new InvalidOperationException("An active investigation requires its exact report receipt time.");
        }
        ReceivedObservationReport? retained = observation.ReceivedReports.FirstOrDefault(item =>
            item.Report.ReportId == active.SourceReport.ReportId
        );
        if (
            retained is not null
            && (
                retained.Report != active.SourceReport
                || retained.ReceivedAt != active.SourceReceivedAt
                || retained.Handling != ObservationReportHandling.Unhandled
            )
        )
        {
            throw new InvalidOperationException("An active investigation disagrees with its retained source report.");
        }
        ShipState responder = GetRequiredShip(active.ResponderShipId);
        if (
            responder.InstanceId == PlayerShipId
            || responder.DirectControllerFactionId != faction.Id
            || responder.ActiveOrder is not TravelToOrder order
            || order.Id != active.OrderId
            || order.Destination != active.DestinationLocationId
            || responder.StrategicState is not TravelingState traveling
            || traveling.Travel.Origin != active.OriginLocationId
            || traveling.Travel.Destination != active.DestinationLocationId
            || traveling.Travel.Departure != active.AssignedAt
            || active.AssignedAt.Milliseconds > Time.Milliseconds
            || active.SourceReceivedAt.Milliseconds > active.AssignedAt.Milliseconds
            || active.AssignedAt.Milliseconds - active.SourceReport.ObservedAt.Milliseconds
                >= FactionObservationState.ReportFreshnessMilliseconds
        )
        {
            throw new InvalidOperationException(
                "An active investigation lacks its fixed controlled travel commitment."
            );
        }
        ValidateActiveCompletionChronology(observation, active);
        if (
            faction.PresenceObjective?.AssignedShipId == active.ResponderShipId
            || observation.CompletionWatermarks.Any(watermark =>
                watermark.LocationId == active.DestinationLocationId
                && watermark.ObservedThrough.Milliseconds >= active.SourceReport.ObservedAt.Milliseconds
            )
            || StrategicMap.FindRoute(active.OriginLocationId, active.DestinationLocationId) is null
        )
        {
            throw new InvalidOperationException(
                "An investigation responder or route conflicts with another commitment."
            );
        }
    }

    private static void ValidateActiveCompletionChronology(
        FactionObservationState observation,
        ActiveFactionInvestigation active
    )
    {
        if (
            observation.CompletionWatermarks.Any(watermark =>
                watermark.ObservedThrough.Milliseconds > active.AssignedAt.Milliseconds
            )
        )
        {
            throw new InvalidOperationException(
                "An active investigation cannot coexist with a later investigation completion."
            );
        }
    }

    private void ValidateObservationReportIdentities()
    {
        var owners = new Dictionary<ObservationReportId, (FactionId FactionId, ObservationReportSnapshot Report)>();
        foreach (FactionState faction in Factions)
        {
            IEnumerable<ObservationReportSnapshot> retained = (faction.Observation?.InFlightReports ?? [])
                .Select(item => item.Report)
                .Concat((faction.Observation?.ReceivedReports ?? []).Select(item => item.Report));
            foreach (ObservationReportSnapshot report in retained)
            {
                if (!owners.TryAdd(report.ReportId, (faction.Id, report)))
                {
                    throw new InvalidOperationException(
                        "Observation report identities must be unique across the aggregate."
                    );
                }
            }

            if (faction.Observation?.ActiveInvestigation is not { } active)
            {
                continue;
            }
            if (
                owners.TryGetValue(
                    active.SourceReport.ReportId,
                    out (FactionId FactionId, ObservationReportSnapshot Report) owner
                )
            )
            {
                bool matchesRetainedSource =
                    owner.FactionId == faction.Id
                    && owner.Report == active.SourceReport
                    && faction.Observation.ReceivedReports.Any(item =>
                        item.Report.ReportId == active.SourceReport.ReportId
                    );
                if (!matchesRetainedSource)
                {
                    throw new InvalidOperationException("An active source report reuses another report identity.");
                }
            }
            else
            {
                owners.Add(active.SourceReport.ReportId, (faction.Id, active.SourceReport));
            }
        }
        long maximum = owners.Keys.Select(id => id.Value).DefaultIfEmpty(0).Max();
        if (ObservationReportIdAllocator.NextId <= maximum)
        {
            throw new InvalidOperationException("Observation report allocator must follow every live report identity.");
        }
    }

    private void ValidateCoordinatedFactionWake(FactionState faction)
    {
        SimulationTime? expected = FindExpectedFactionDecisionTime(faction);
        PendingFactionDecisionWake? wake = faction.PendingDecisionWake;
        if ((expected is null) != (wake is null) || (expected is { } due && wake!.DueTime != due))
        {
            throw new InvalidOperationException("A faction decision wake is not the earliest justified continuation.");
        }
        if (
            wake is not null
            && !Scheduler.ContainsExact(
                wake.WorkId,
                ScheduledWorkTarget.ForFaction(faction.Id),
                wake.DueTime,
                ScheduledWorkKind.FactionDecisionWake
            )
        )
        {
            throw new InvalidOperationException("A faction decision wake lacks its exact scheduled work.");
        }
    }

    private SimulationTime? FindExpectedFactionDecisionTime(FactionState faction)
    {
        List<SimulationTime> candidates = [];
        if (faction.PresenceObjective is { Status: FactionObjectiveStatus.Assigned } assigned)
        {
            candidates.Add(
                ((TravelingState)GetRequiredShip(assigned.AssignedShipId!.Value).StrategicState).Travel.ExpectedArrival
            );
        }
        else if (faction.PresenceObjective is { Status: FactionObjectiveStatus.Pending } pending)
        {
            // Bootstrap and restored intent may carry one immediate wake whose job is to discover whether the
            // objective can act or must normalize to a future release/dormancy at this boundary.
            if (faction.PendingDecisionWake?.DueTime == Time)
            {
                candidates.Add(Time);
            }
            else
            {
                FactionAssignmentDecisionExplanation decision = GameSimulation.DecideFactionAssignment(
                    this,
                    faction,
                    pending
                );
                if (decision.Outcome == FactionAssignmentDecisionOutcome.AssignmentProposed)
                {
                    candidates.Add(Time);
                }
                else if (GameSimulation.FindNextFactionOpportunity(this, faction) is { } release)
                {
                    candidates.Add(release);
                }
            }
        }

        FactionObservationState observation = faction.Observation ?? new FactionObservationState();
        if (observation.ActiveInvestigation is { } active)
        {
            candidates.Add(
                ((TravelingState)GetRequiredShip(active.ResponderShipId).StrategicState).Travel.ExpectedArrival
            );
        }
        else
        {
            if (GameSimulation.FindResponseReleaseOpportunity(this, faction, observation) is { } release)
            {
                candidates.Add(release);
            }
        }
        return candidates.OrderBy(value => value.Milliseconds).Cast<SimulationTime?>().FirstOrDefault();
    }
}
