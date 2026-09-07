using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

public sealed partial class GameSimulation
{
    /// <summary>Gets the latest investigation diagnostic produced during this live session.</summary>
    internal FactionInvestigationDecisionExplanation? LastFactionInvestigationDecisionExplanation { get; private set; }

    internal sealed record ObservationPublicationCandidate(
        FactionId RecipientFactionId,
        ShipInstanceId ObserverShipId,
        SensorContactId ObserverContactId,
        LocationId ObservedAtLocationId,
        TacticalPosition ObservedPosition,
        SimulationTime ObservedAt,
        SensorContactIdentification Identification,
        string? KnownVesselDisplayName,
        string? KnownDesignDisplayName
    );

    internal sealed class ObservationPublicationCollector
    {
        private readonly Dictionary<FactionId, List<ObservationPublicationCandidate>> _byFaction = [];
        private readonly Dictionary<(FactionId FactionId, LocationId LocationId), SimulationTime> _completions = [];

        internal IEnumerable<ObservationPublicationCandidate> Candidates =>
            _byFaction.Values.SelectMany(candidates => candidates);
        internal IEnumerable<KeyValuePair<(FactionId FactionId, LocationId LocationId), SimulationTime>> Completions =>
            _completions;

        internal void RecordCompletion(FactionId factionId, LocationId locationId, SimulationTime completedAt)
        {
            (FactionId FactionId, LocationId LocationId) key = (factionId, locationId);
            if (
                !_completions.TryGetValue(key, out SimulationTime prior)
                || prior.Milliseconds < completedAt.Milliseconds
            )
            {
                _completions[key] = completedAt;
            }
        }

        internal void Add(ObservationPublicationCandidate candidate)
        {
            if (
                !_byFaction.TryGetValue(
                    candidate.RecipientFactionId,
                    out List<ObservationPublicationCandidate>? candidates
                )
            )
            {
                candidates = [];
                _byFaction.Add(candidate.RecipientFactionId, candidates);
            }

            if (
                candidates.Any(existing =>
                    existing.ObserverShipId == candidate.ObserverShipId
                    && existing.ObserverContactId == candidate.ObserverContactId
                )
            )
            {
                return;
            }

            candidates.Add(candidate);
            candidates.Sort(ComparePublicationCandidates);
            if (candidates.Count > FactionObservationState.MaximumInFlightReports)
            {
                candidates.RemoveAt(candidates.Count - 1);
            }
        }
    }

    private static int ComparePublicationCandidates(
        ObservationPublicationCandidate left,
        ObservationPublicationCandidate right
    )
    {
        int observer = left.ObserverShipId.Value.CompareTo(right.ObserverShipId.Value);
        return observer != 0 ? observer : left.ObserverContactId.Value.CompareTo(right.ObserverContactId.Value);
    }

    private static void CollectObservationPublications(
        SimulationState state,
        ShipState before,
        ShipState after,
        ObservationPublicationCollector collector
    )
    {
        if (
            after.InstanceId == state.PlayerShipId
            || after.DirectControllerFactionId is not { } factionId
            || state.GetRequiredFaction(factionId).Observation is not { Posture: ObservationResponsePosture.Enabled }
        )
        {
            return;
        }

        foreach (SensorContactTrack contact in after.SensorKnowledge.Contacts)
        {
            SensorContactTrack? prior = before.SensorKnowledge.Contacts.FirstOrDefault(item => item.Id == contact.Id);
            bool beginsEpisode =
                contact.Status == SensorContactStatus.Current
                && (prior is null || prior.Status == SensorContactStatus.Lost);
            if (!beginsEpisode || contact.ObservedAtLocationId is not { } locationId)
            {
                continue;
            }

            collector.Add(
                new ObservationPublicationCandidate(
                    factionId,
                    after.InstanceId,
                    contact.Id,
                    locationId,
                    contact.LastObservedPosition,
                    contact.LastObservedAt,
                    contact.Identification,
                    contact.KnownVesselDisplayName,
                    contact.KnownDesignDisplayName
                )
            );
        }
    }

    private static SimulationState AdmitObservationPublications(
        SimulationState state,
        ObservationPublicationCollector collector
    )
    {
        SimulationState current = state;
        foreach (
            IGrouping<FactionId, ObservationPublicationCandidate> group in collector
                .Candidates.OrderBy(candidate => candidate.RecipientFactionId.Value)
                .ThenBy(candidate => candidate.ObserverShipId.Value)
                .ThenBy(candidate => candidate.ObserverContactId.Value)
                .GroupBy(candidate => candidate.RecipientFactionId)
        )
        {
            current = AdmitFactionPublications(current, group.Key, group);
        }

        return PruneObservationWatermarks(ApplyCompletionWatermarks(current, collector));
    }

    private static SimulationState AdmitFactionPublications(
        SimulationState state,
        FactionId factionId,
        IEnumerable<ObservationPublicationCandidate> candidates
    )
    {
        SimulationState current = state;
        FactionState faction = current.GetRequiredFaction(factionId);
        FactionObservationState observation = faction.Observation ?? new FactionObservationState();
        int capacity = FactionObservationState.MaximumInFlightReports - observation.InFlightReports.Length;
        foreach (ObservationPublicationCandidate candidate in candidates.Take(capacity))
        {
            (ObservationReportIdAllocator allocator, ObservationReportId reportId) =
                current.ObservationReportIdAllocator.Allocate();
            var report = new ObservationReportSnapshot(
                reportId,
                candidate.ObserverShipId,
                candidate.ObserverContactId,
                candidate.ObservedAtLocationId,
                candidate.ObservedPosition,
                candidate.ObservedAt,
                candidate.Identification,
                candidate.KnownVesselDisplayName,
                candidate.KnownDesignDisplayName
            );
            SimulationTime due = candidate.ObservedAt.AdvanceBy(
                new SimulationDuration(FactionObservationState.ReportDeliveryDelayMilliseconds)
            );
            (SimulationScheduler scheduler, ScheduledWork work) = current.Scheduler.Schedule(
                due,
                ScheduledWorkTarget.ForFaction(faction.Id),
                ScheduledWorkKind.ObservationReportDelivery
            );
            observation = RebuildObservation(
                observation,
                inFlight: observation.InFlightReports.Add(new ObservationReportInFlight(report, work.Id, due))
            );
            current = current.ReplaceFaction(faction.Id, faction with { Observation = observation }) with
            {
                Scheduler = scheduler,
                ObservationReportIdAllocator = allocator,
            };
            faction = current.GetRequiredFaction(faction.Id);
        }
        return current;
    }

    private static SimulationState ApplyCompletionWatermarks(
        SimulationState state,
        ObservationPublicationCollector collector
    )
    {
        SimulationState current = state;
        foreach (
            KeyValuePair<
                (FactionId FactionId, LocationId LocationId),
                SimulationTime
            > completion in collector.Completions
        )
        {
            FactionState faction = current.GetRequiredFaction(completion.Key.FactionId);
            FactionObservationState observation = faction.Observation ?? new FactionObservationState();
            var completed = new ObservationLocationCompletionWatermark(completion.Key.LocationId, completion.Value);
            if (!IsWatermarkWitnessed(observation, completed))
            {
                continue;
            }
            IEnumerable<ObservationLocationCompletionWatermark> watermarks = observation
                .CompletionWatermarks.Where(item =>
                    item.LocationId != completion.Key.LocationId && IsWatermarkWitnessed(observation, item)
                )
                .Append(completed);
            current = current.ReplaceFaction(
                faction.Id,
                faction with
                {
                    Observation = RebuildObservation(observation, watermarks: watermarks),
                }
            );
        }
        return current;
    }

    private static bool IsWatermarkWitnessed(
        FactionObservationState observation,
        ObservationLocationCompletionWatermark watermark
    ) =>
        observation.InFlightReports.Any(item =>
            item.Report.ObservedAtLocationId == watermark.LocationId
            && item.Report.ObservedAt.Milliseconds <= watermark.ObservedThrough.Milliseconds
        )
        || observation.ReceivedReports.Any(item =>
            item.Report.ObservedAtLocationId == watermark.LocationId
            && item.Report.ObservedAt.Milliseconds <= watermark.ObservedThrough.Milliseconds
        );

    private static (SimulationState State, bool Delivered) CompleteObservationReportDelivery(
        SimulationState state,
        ScheduledWork work
    )
    {
        FactionState faction = state.GetRequiredFaction(work.Target.FactionId!.Value);
        FactionObservationState observation = faction.Observation ?? new FactionObservationState();
        ObservationReportInFlight? inFlight = observation.InFlightReports.FirstOrDefault(item =>
            item.DeliveryWorkId == work.Id
        );
        if (inFlight is null)
        {
            return (state, false);
        }

        if (inFlight.DueTime != work.DueTime)
        {
            throw new InvalidOperationException("Report delivery has a malformed extant runtime correlation.");
        }

        var received = new ReceivedObservationReport(inFlight.Report, state.Time);
        IEnumerable<ReceivedObservationReport> retained = observation
            .ReceivedReports.Where(item =>
                item.Handling == ObservationReportHandling.Unhandled
                && state.Time.Milliseconds - item.Report.ObservedAt.Milliseconds
                    < FactionObservationState.ReportFreshnessMilliseconds
            )
            .Append(received)
            .GroupBy(item => item.Report.ReportId)
            .Select(group => group.OrderBy(item => item.ReceivedAt.Milliseconds).First())
            .OrderByDescending(item => item.Report.ObservedAt.Milliseconds)
            .ThenBy(item => item.Report.ReportId.Value)
            .Take(FactionObservationState.MaximumReceivedReports);
        FactionObservationState updated = RebuildObservation(
            observation,
            inFlight: observation.InFlightReports.Where(item => item.DeliveryWorkId != work.Id),
            received: retained
        );
        return (state.ReplaceFaction(faction.Id, faction with { Observation = updated }), true);
    }

    internal static FactionInvestigationDecisionExplanation DecideFactionInvestigation(
        SimulationState state,
        FactionState faction,
        ObservationPublicationCollector? boundaryCollector = null
    )
    {
        FactionObservationState observation = faction.Observation ?? new FactionObservationState();
        var input = new FactionInvestigationDecisionInput(
            faction.Id,
            state.Time,
            observation.Posture,
            observation.ReceivedReports.Where(report =>
                !IsCoveredByBoundaryCompletion(boundaryCollector, faction.Id, report.Report)
            ),
            observation.CompletionWatermarks,
            observation.ActiveInvestigation is not null,
            state
                .Ships.Where(ship => ship.DirectControllerFactionId == faction.Id)
                .Select(ship => new FactionShipAssignmentSnapshot(
                    ship.InstanceId,
                    ship.InstanceId == state.PlayerShipId,
                    ship.StrategicState is AtLocationState
                        ? FactionShipStrategicStatus.AtLocation
                        : FactionShipStrategicStatus.Traveling,
                    (ship.StrategicState as AtLocationState)?.LocationId,
                    ship.ActiveOrder is not null
                )),
            state.StrategicMap.Routes.Select(route => new FactionKnownRouteSnapshot(
                route.Origin,
                route.Destination,
                route.Duration
            ))
        );
        return FactionInvestigationPolicy.Evaluate(input);
    }

    internal static FactionInvestigationApplicationResult ApplyFactionInvestigation(
        SimulationState state,
        FactionInvestigationProposal proposal,
        ShipDefinitionCatalog shipCatalog,
        ObservationPublicationCollector? boundaryCollector = null
    )
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(shipCatalog);
        FactionInvestigationApplicationOutcome? rejection = ValidateInvestigationApplication(
            state,
            proposal,
            out FactionState? faction,
            out FactionObservationState? observation,
            out ReceivedObservationReport? source,
            out ShipState? ship,
            out AtLocationState? atLocation,
            boundaryCollector
        );
        return rejection is { } outcome
            ? InvestigationRejected(outcome, state, proposal)
            : ApplyValidatedInvestigation(
                state,
                proposal,
                shipCatalog,
                faction!,
                observation!,
                source!,
                ship!,
                atLocation!,
                boundaryCollector
            );
    }

    private static FactionInvestigationApplicationOutcome? ValidateInvestigationApplication(
        SimulationState state,
        FactionInvestigationProposal proposal,
        out FactionState? faction,
        out FactionObservationState? observation,
        out ReceivedObservationReport? source,
        out ShipState? ship,
        out AtLocationState? atLocation,
        ObservationPublicationCollector? boundaryCollector
    )
    {
        faction = state.Factions.FirstOrDefault(item => item.Id == proposal.FactionId);
        observation = faction?.Observation;
        source = observation?.ReceivedReports.FirstOrDefault(item =>
            item.Report.ReportId == proposal.SourceReport.ReportId
        );
        ship = state.Ships.FirstOrDefault(item => item.InstanceId == proposal.ResponderShipId);
        atLocation = ship?.StrategicState as AtLocationState;
        if (faction is null)
            return FactionInvestigationApplicationOutcome.FactionMissing;
        observation ??= new FactionObservationState();
        if (observation.Posture != ObservationResponsePosture.Enabled)
            return FactionInvestigationApplicationOutcome.PostureDisabled;
        if (observation.ActiveInvestigation is not null)
            return FactionInvestigationApplicationOutcome.ActiveInvestigationExists;
        if (source is null || source.Report != proposal.SourceReport || source.ReceivedAt != proposal.ReceivedAt)
            return FactionInvestigationApplicationOutcome.SourceReportUnavailable;
        if (
            !IsReportActionable(state, observation, source)
            || IsCoveredByBoundaryCompletion(boundaryCollector, faction.Id, source.Report)
        )
            return FactionInvestigationApplicationOutcome.SourceReportIneligible;
        if (proposal.DecisionTime != state.Time)
            return FactionInvestigationApplicationOutcome.DecisionStale;
        if (ship is null)
            return FactionInvestigationApplicationOutcome.ShipMissing;
        if (ship.InstanceId == state.PlayerShipId)
            return FactionInvestigationApplicationOutcome.PlayerShipRejected;
        if (ship.DirectControllerFactionId != faction.Id)
            return FactionInvestigationApplicationOutcome.ControllerMismatch;
        if (ship.ActiveOrder is not null)
            return FactionInvestigationApplicationOutcome.ShipCommitted;
        if (atLocation is null)
            return FactionInvestigationApplicationOutcome.ShipTraveling;
        if (atLocation.LocationId != proposal.OriginLocationId)
            return FactionInvestigationApplicationOutcome.OriginMismatch;
        return
            atLocation.LocationId != proposal.DestinationLocationId
            && state.StrategicMap.FindRoute(atLocation.LocationId, proposal.DestinationLocationId) is null
            ? FactionInvestigationApplicationOutcome.RouteUnavailable
            : null;
    }

    private static bool IsReportActionable(
        SimulationState state,
        FactionObservationState observation,
        ReceivedObservationReport source
    ) =>
        source.Handling == ObservationReportHandling.Unhandled
        && state.Time.Milliseconds - source.Report.ObservedAt.Milliseconds
            < FactionObservationState.ReportFreshnessMilliseconds
        && !observation.CompletionWatermarks.Any(item =>
            item.LocationId == source.Report.ObservedAtLocationId
            && item.ObservedThrough.Milliseconds >= source.Report.ObservedAt.Milliseconds
        );

    private static bool IsCoveredByBoundaryCompletion(
        ObservationPublicationCollector? boundaryCollector,
        FactionId factionId,
        ObservationReportSnapshot report
    ) =>
        boundaryCollector?.Completions.Any(completion =>
            completion.Key.FactionId == factionId
            && completion.Key.LocationId == report.ObservedAtLocationId
            && completion.Value.Milliseconds >= report.ObservedAt.Milliseconds
        ) == true;

    private static FactionInvestigationApplicationResult ApplyValidatedInvestigation(
        SimulationState state,
        FactionInvestigationProposal proposal,
        ShipDefinitionCatalog shipCatalog,
        FactionState faction,
        FactionObservationState observation,
        ReceivedObservationReport source,
        ShipState ship,
        AtLocationState atLocation,
        ObservationPublicationCollector? boundaryCollector
    )
    {
        (ShipOrderIdAllocator allocator, ShipOrderId orderId) = state.OrderIdAllocator.Allocate();
        var active = new ActiveFactionInvestigation(
            source.Report,
            ship.InstanceId,
            atLocation.LocationId,
            proposal.DestinationLocationId,
            source.ReceivedAt,
            state.Time,
            orderId
        );
        SimulationState candidate = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = RebuildObservation(observation, active: active, replaceActive: true),
            }
        ) with
        {
            OrderIdAllocator = allocator,
        };

        if (atLocation.LocationId == proposal.DestinationLocationId)
        {
            return CompleteInvestigationAtCurrentLocation(
                candidate,
                proposal,
                shipCatalog,
                faction.Id,
                boundaryCollector
            );
        }

        return StartInvestigationTravel(
            candidate,
            proposal,
            faction,
            observation,
            active,
            ship,
            allocator,
            orderId,
            boundaryCollector
        );
    }

    private static FactionInvestigationApplicationResult StartInvestigationTravel(
        SimulationState state,
        FactionInvestigationProposal proposal,
        FactionState faction,
        FactionObservationState observation,
        ActiveFactionInvestigation active,
        ShipState ship,
        ShipOrderIdAllocator allocator,
        ShipOrderId orderId,
        ObservationPublicationCollector? boundaryCollector
    )
    {
        ShipTravelApplicationResult travel = ApplyShipTravel(
            state,
            new ShipTravelCommand(ship.InstanceId, proposal.DestinationLocationId)
        );
        if (travel.Outcome != TravelOutcome.Accepted)
        {
            throw new InvalidOperationException("A validated investigation route could not begin ordinary travel.");
        }
        ShipState travelingShip = travel.CandidateState.GetRequiredShip(ship.InstanceId);
        SimulationState candidate = travel.CandidateState.ReplaceShip(
            ship.InstanceId,
            travelingShip with
            {
                ActiveOrder = new TravelToOrder(orderId, proposal.DestinationLocationId),
            }
        );
        candidate = candidate.ReplaceFaction(
            faction.Id,
            candidate.GetRequiredFaction(faction.Id) with
            {
                Observation = RebuildObservation(observation, active: active, replaceActive: true),
            }
        ) with
        {
            OrderIdAllocator = allocator,
        };
        candidate = NormalizeFactionDecisionWake(candidate, faction.Id, boundaryCollector);
        return new FactionInvestigationApplicationResult(
            FactionInvestigationApplicationOutcome.Accepted,
            candidate,
            proposal
        );
    }

    private static FactionInvestigationApplicationResult CompleteInvestigationAtCurrentLocation(
        SimulationState state,
        FactionInvestigationProposal proposal,
        ShipDefinitionCatalog shipCatalog,
        FactionId factionId,
        ObservationPublicationCollector? boundaryCollector
    )
    {
        bool ownsCollector = boundaryCollector is null;
        boundaryCollector ??= new ObservationPublicationCollector();
        List<PlayerAdvanceEvent> noPlayerEvents = [];
        SimulationState candidate = ObserveAllShips(state, shipCatalog, noPlayerEvents, boundaryCollector);
        candidate = CompleteFactionInvestigation(candidate, factionId, boundaryCollector);
        if (ownsCollector)
        {
            candidate = AdmitObservationPublications(candidate, boundaryCollector);
        }
        return new FactionInvestigationApplicationResult(
            FactionInvestigationApplicationOutcome.CompletedAtCurrentLocation,
            candidate,
            proposal
        );
    }

    private static SimulationState CompleteFactionInvestigation(
        SimulationState state,
        FactionId factionId,
        ObservationPublicationCollector publicationCollector
    )
    {
        FactionState faction = state.GetRequiredFaction(factionId);
        FactionObservationState observation = faction.Observation!;
        ActiveFactionInvestigation active = observation.ActiveInvestigation!;
        IEnumerable<ReceivedObservationReport> received = observation.ReceivedReports.Select(item =>
            item.Report.ReportId == active.SourceReport.ReportId
                ? new ReceivedObservationReport(item.Report, item.ReceivedAt, ObservationReportHandling.Handled)
                : item
        );
        publicationCollector.RecordCompletion(faction.Id, active.DestinationLocationId, state.Time);
        return state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    observation.Posture,
                    observation.InFlightReports,
                    received,
                    null,
                    observation.CompletionWatermarks
                ),
            }
        );
    }

    private static (SimulationState State, FactionId? CompletedFactionId) CompleteArrivedInvestigation(
        SimulationState state,
        ScheduledWork work,
        ScheduledConsequenceTrace trace,
        ObservationPublicationCollector publicationCollector
    )
    {
        if (work.Kind != ScheduledWorkKind.TravelArrival || trace.OrderId is not { } orderId)
        {
            return (state, null);
        }

        FactionState? faction = state.Factions.FirstOrDefault(item =>
            item.Observation?.ActiveInvestigation is { } active
            && active.ResponderShipId == work.TargetShipId
            && active.OrderId == orderId
        );
        if (faction?.Observation?.ActiveInvestigation is not { } investigation)
        {
            return (state, null);
        }

        ShipState responder = state.GetRequiredShip(investigation.ResponderShipId);
        if (
            responder.StrategicState is not AtLocationState atLocation
            || atLocation.LocationId != investigation.DestinationLocationId
            || responder.ActiveOrder is not null
        )
        {
            throw new InvalidOperationException(
                "Investigation arrival did not settle its exact ordinary travel order."
            );
        }

        return (CompleteFactionInvestigation(state, faction.Id, publicationCollector), faction.Id);
    }

    private static SimulationState PruneObservationWatermarks(SimulationState state)
    {
        SimulationState current = state;
        foreach (FactionState faction in state.Factions)
        {
            FactionObservationState observation = faction.Observation ?? new FactionObservationState();
            ObservationLocationCompletionWatermark[] retained =
            [
                .. observation.CompletionWatermarks.Where(watermark =>
                    observation.InFlightReports.Any(item =>
                        item.Report.ObservedAtLocationId == watermark.LocationId
                        && item.Report.ObservedAt.Milliseconds <= watermark.ObservedThrough.Milliseconds
                    )
                    || observation.ReceivedReports.Any(item =>
                        item.Report.ObservedAtLocationId == watermark.LocationId
                        && item.Report.ObservedAt.Milliseconds <= watermark.ObservedThrough.Milliseconds
                    )
                ),
            ];
            if (retained.Length != observation.CompletionWatermarks.Length)
            {
                current = current.ReplaceFaction(
                    faction.Id,
                    faction with
                    {
                        Observation = RebuildObservation(observation, watermarks: retained),
                    }
                );
            }
        }
        return current;
    }

    private static FactionObservationState RebuildObservation(
        FactionObservationState source,
        IEnumerable<ObservationReportInFlight>? inFlight = null,
        IEnumerable<ReceivedObservationReport>? received = null,
        ActiveFactionInvestigation? active = null,
        bool replaceActive = false,
        IEnumerable<ObservationLocationCompletionWatermark>? watermarks = null
    ) =>
        new(
            source.Posture,
            inFlight ?? source.InFlightReports,
            received ?? source.ReceivedReports,
            replaceActive ? active : source.ActiveInvestigation,
            watermarks ?? source.CompletionWatermarks
        );

    private static FactionInvestigationApplicationResult InvestigationRejected(
        FactionInvestigationApplicationOutcome outcome,
        SimulationState state,
        FactionInvestigationProposal proposal
    ) => new(outcome, state, proposal);

    private void RememberLatestFactionInvestigationDecision(IReadOnlyList<ScheduledConsequenceTrace> traces)
    {
        FactionInvestigationDecisionExplanation? latest = traces
            .Select(trace => trace.FactionInvestigationDecision)
            .LastOrDefault(decision => decision is not null);
        if (latest is not null)
        {
            LastFactionInvestigationDecisionExplanation = latest;
        }
    }
}
