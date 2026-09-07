using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

public sealed partial class GameSimulation
{
    /// <summary>Gets the latest faction decision diagnostic produced during this live session.</summary>
    internal FactionAssignmentDecisionExplanation? LastFactionDecisionExplanation { get; private set; }

    private static (SimulationState State, ScheduledConsequenceTrace Trace) CompleteFactionDecisionWake(
        SimulationState state,
        ScheduledWork work,
        FactionDefinitionCatalog factionCatalog
    )
    {
        FactionState faction = state.GetRequiredFaction(work.Target.FactionId!.Value);
        if (faction.PendingDecisionWake is not { } wake || wake.WorkId != work.Id || wake.DueTime != work.DueTime)
        {
            throw new InvalidOperationException("Faction decision wake lacks its exact runtime correlation.");
        }

        factionCatalog.GetRequired(faction.DefinitionId);
        SimulationState cleared = state.ReplaceFaction(faction.Id, faction with { PendingDecisionWake = null });
        return (
            cleared,
            new ScheduledConsequenceTrace(
                work.Id,
                work.Target,
                work.Kind,
                state.Time,
                faction.PresenceObjective?.AssignedOrderId,
                faction.PresenceObjective?.AssignedOrderId is null ? null : ShipOrderKind.TravelTo,
                ScheduledConsequenceRule.FactionDecisionWake,
                ScheduledConsequenceAction.WakeFactionDecision,
                true,
                false
            )
        );
    }

    private sealed record CoordinatedFactionDecision(
        SimulationState State,
        FactionAssignmentDecisionExplanation? PresenceDecision,
        FactionInvestigationDecisionExplanation InvestigationDecision
    );

    private static CoordinatedFactionDecision CoordinateFactionDecision(
        SimulationState state,
        FactionId factionId,
        ShipDefinitionCatalog shipCatalog,
        ObservationPublicationCollector publicationCollector,
        List<PlayerAdvanceEvent> playerEvents
    )
    {
        SimulationState current = CancelPendingFactionWake(state, factionId);
        FactionState faction = current.GetRequiredFaction(factionId);
        FactionAssignmentDecisionExplanation? presenceDecision = null;
        if (faction.PresenceObjective is { Status: FactionObjectiveStatus.Pending } objective)
        {
            presenceDecision = DecideFactionAssignment(current, faction, objective);
            current = ApplyFactionDecision(current, faction, presenceDecision);
            current = ObserveAllShips(current, shipCatalog, playerEvents, publicationCollector);
        }
        else if (faction.PresenceObjective is { Status: FactionObjectiveStatus.Assigned } assigned)
        {
            ShipState assignedShip = current.GetRequiredShip(assigned.AssignedShipId!.Value);
            if (
                assignedShip.StrategicState is AtLocationState atLocation
                && atLocation.LocationId == assigned.TargetLocationId
            )
            {
                presenceDecision = DecideFactionAssignment(current, faction, assigned);
                current = ApplyFactionDecision(current, faction, presenceDecision);
                current = ObserveAllShips(current, shipCatalog, playerEvents, publicationCollector);
            }
        }

        faction = current.GetRequiredFaction(factionId);
        FactionInvestigationDecisionExplanation investigationDecision = DecideFactionInvestigation(
            current,
            faction,
            publicationCollector
        );
        if (investigationDecision.Proposal is { } proposal)
        {
            FactionInvestigationApplicationResult application = ApplyFactionInvestigation(
                current,
                proposal,
                shipCatalog,
                publicationCollector,
                playerEvents
            );
            current = application.CandidateState;
        }

        current = NormalizeFactionDecisionWake(current, factionId, publicationCollector);
        return new CoordinatedFactionDecision(current, presenceDecision, investigationDecision);
    }

    private static SimulationState CancelPendingFactionWake(SimulationState state, FactionId factionId)
    {
        FactionState faction = state.GetRequiredFaction(factionId);
        if (faction.PendingDecisionWake is not { } wake)
        {
            return state;
        }

        (SimulationScheduler scheduler, _) = state.Scheduler.Cancel(wake.WorkId);
        return state.ReplaceFaction(faction.Id, faction with { PendingDecisionWake = null }) with
        {
            Scheduler = scheduler,
        };
    }

    private static SimulationState NormalizeFactionDecisionWake(
        SimulationState state,
        FactionId factionId,
        ObservationPublicationCollector? boundaryCollector = null
    )
    {
        SimulationState current = CancelPendingFactionWake(state, factionId);
        FactionState faction = current.GetRequiredFaction(factionId);
        List<SimulationTime> opportunities = [];

        if (faction.PresenceObjective is { Status: FactionObjectiveStatus.Assigned } assigned)
        {
            ShipState ship = current.GetRequiredShip(assigned.AssignedShipId!.Value);
            if (ship.StrategicState is TravelingState traveling)
            {
                opportunities.Add(traveling.Travel.ExpectedArrival);
            }
        }
        else if (faction.PresenceObjective is { Status: FactionObjectiveStatus.Pending })
        {
            SimulationTime? release = FindNextFactionOpportunity(current, faction);
            if (release is { } due)
            {
                opportunities.Add(due);
            }
        }

        FactionObservationState observation = faction.Observation ?? new FactionObservationState();
        if (observation.ActiveInvestigation is { } active)
        {
            ShipState responder = current.GetRequiredShip(active.ResponderShipId);
            if (responder.StrategicState is TravelingState traveling)
            {
                opportunities.Add(traveling.Travel.ExpectedArrival);
            }
        }
        else if (
            FindResponseReleaseOpportunity(current, faction, observation, boundaryCollector) is { } responseRelease
        )
        {
            opportunities.Add(responseRelease);
        }

        SimulationTime? next = opportunities
            .Where(value => value.Milliseconds > current.Time.Milliseconds)
            .OrderBy(value => value.Milliseconds)
            .Cast<SimulationTime?>()
            .FirstOrDefault();
        if (next is null)
        {
            return current;
        }

        return ScheduleFactionDecisionWake(current, faction, next.Value);
    }

    private static SimulationState ScheduleFactionDecisionWake(
        SimulationState state,
        FactionState faction,
        SimulationTime dueTime
    )
    {
        (SimulationScheduler scheduler, ScheduledWork work) = state.Scheduler.Schedule(
            dueTime,
            ScheduledWorkTarget.ForFaction(faction.Id),
            ScheduledWorkKind.FactionDecisionWake
        );
        return state.ReplaceFaction(
            faction.Id,
            faction with
            {
                PendingDecisionWake = new PendingFactionDecisionWake(work.Id, work.DueTime),
            }
        ) with
        {
            Scheduler = scheduler,
        };
    }

    internal static SimulationTime? FindResponseReleaseOpportunity(
        SimulationState state,
        FactionState faction,
        FactionObservationState observation,
        ObservationPublicationCollector? boundaryCollector = null
    )
    {
        SimulationTime? boundary = FindNextFactionOpportunity(state, faction);
        if (boundary is not { } candidate)
        {
            return null;
        }

        bool reportSurvives = observation.ReceivedReports.Any(report =>
            report.Handling == ObservationReportHandling.Unhandled
            && report.Report.ObservedAt.Milliseconds <= state.Time.Milliseconds
            && candidate.Milliseconds - report.Report.ObservedAt.Milliseconds
                < FactionObservationState.ReportFreshnessMilliseconds
            && !observation.CompletionWatermarks.Any(watermark =>
                watermark.LocationId == report.Report.ObservedAtLocationId
                && watermark.ObservedThrough.Milliseconds >= report.Report.ObservedAt.Milliseconds
            )
            && !IsCoveredByBoundaryCompletion(boundaryCollector, faction.Id, report.Report)
        );
        return reportSurvives ? candidate : null;
    }

    internal static FactionAssignmentDecisionExplanation DecideFactionAssignment(
        SimulationState state,
        FactionState faction,
        EstablishPresenceObjectiveState objective
    )
    {
        FactionShipAssignmentSnapshot[] assets = state
            .Ships.Where(ship => ship.DirectControllerFactionId == faction.Id)
            .Select(ship => new FactionShipAssignmentSnapshot(
                ship.InstanceId,
                ship.InstanceId == state.PlayerShipId,
                ship.StrategicState is AtLocationState
                    ? FactionShipStrategicStatus.AtLocation
                    : FactionShipStrategicStatus.Traveling,
                (ship.StrategicState as AtLocationState)?.LocationId,
                ship.ActiveOrder is not null
            ))
            .ToArray();
        FactionKnownRouteSnapshot[] routes = state
            .StrategicMap.Routes.Select(route => new FactionKnownRouteSnapshot(
                route.Origin,
                route.Destination,
                route.Duration
            ))
            .ToArray();
        var input = new FactionAssignmentDecisionInput(
            faction.Id,
            state.Time,
            new EstablishPresenceObjectiveSnapshot(objective.TargetLocationId),
            assets,
            routes
        );
        return FactionAssignmentPolicy.Evaluate(input);
    }

    private static SimulationState ApplyFactionDecision(
        SimulationState state,
        FactionState faction,
        FactionAssignmentDecisionExplanation decision
    )
    {
        if (decision.Outcome == FactionAssignmentDecisionOutcome.ObjectiveAlreadySatisfied)
        {
            EstablishPresenceObjectiveState satisfied = faction.PresenceObjective! with
            {
                Status = FactionObjectiveStatus.Satisfied,
                AssignedShipId = null,
                AssignedOrderId = null,
            };
            return state.ReplaceFaction(faction.Id, faction with { PresenceObjective = satisfied });
        }

        if (decision.Proposal is { } proposal)
        {
            FactionAssignmentApplicationResult application = ApplyFactionAssignment(state, proposal);
            if (application.Outcome != FactionAssignmentApplicationOutcome.Accepted)
            {
                return ScheduleNextFactionOpportunity(state, faction);
            }
            return application.CandidateState;
        }

        return ScheduleNextFactionOpportunity(state, faction);
    }

    internal static FactionAssignmentApplicationResult ApplyFactionAssignment(
        SimulationState state,
        FactionAssignmentProposal proposal
    )
    {
        FactionState? faction = state.Factions.FirstOrDefault(candidate => candidate.Id == proposal.FactionId);
        if (faction is null)
        {
            return Rejected(FactionAssignmentApplicationOutcome.FactionMissing, state);
        }
        if (
            faction.PresenceObjective is not { Status: FactionObjectiveStatus.Pending } objective
            || objective.TargetLocationId != proposal.Destination
        )
        {
            return Rejected(FactionAssignmentApplicationOutcome.ObjectiveMismatch, state);
        }
        if (faction.PendingDecisionWake is not null)
        {
            return Rejected(FactionAssignmentApplicationOutcome.DecisionWakePending, state);
        }

        ShipState? ship = state.Ships.FirstOrDefault(candidate => candidate.InstanceId == proposal.ShipId);
        FactionAssignmentApplicationOutcome? rejection = ValidateAssignmentShip(state, faction, ship, proposal);
        if (rejection is not null)
        {
            return Rejected(rejection.Value, state);
        }

        return ApplyValidatedFactionAssignment(state, faction, ship!, proposal);
    }

    private static FactionAssignmentApplicationOutcome? ValidateAssignmentShip(
        SimulationState state,
        FactionState faction,
        ShipState? ship,
        FactionAssignmentProposal proposal
    )
    {
        if (ship is null)
            return FactionAssignmentApplicationOutcome.ShipMissing;
        if (ship.InstanceId == state.PlayerShipId)
            return FactionAssignmentApplicationOutcome.PlayerShipRejected;
        if (ship.DirectControllerFactionId != faction.Id)
            return FactionAssignmentApplicationOutcome.ControllerMismatch;
        if (ship.ActiveOrder is not null)
            return FactionAssignmentApplicationOutcome.ShipCommitted;
        if (ship.StrategicState is not AtLocationState atLocation)
            return FactionAssignmentApplicationOutcome.ShipTraveling;
        return state.StrategicMap.FindRoute(atLocation.LocationId, proposal.Destination) is null
            ? FactionAssignmentApplicationOutcome.RouteUnavailable
            : null;
    }

    private static FactionAssignmentApplicationResult ApplyValidatedFactionAssignment(
        SimulationState state,
        FactionState faction,
        ShipState ship,
        FactionAssignmentProposal proposal
    )
    {
        (ShipOrderIdAllocator allocator, ShipOrderId orderId) = state.OrderIdAllocator.Allocate();
        ShipTravelApplicationResult travel = ApplyShipTravel(
            state,
            new ShipTravelCommand(ship.InstanceId, proposal.Destination)
        );
        if (travel.Outcome != TravelOutcome.Accepted)
        {
            throw new InvalidOperationException("A fully validated faction travel assignment was rejected.");
        }

        ShipState travelingShip = travel.CandidateState.GetRequiredShip(ship.InstanceId);
        var traveling = (TravelingState)travelingShip.StrategicState;
        var order = new TravelToOrder(orderId, proposal.Destination);
        (SimulationScheduler scheduler, ScheduledWork wake) = travel.CandidateState.Scheduler.Schedule(
            traveling.Travel.ExpectedArrival,
            ScheduledWorkTarget.ForFaction(faction.Id),
            ScheduledWorkKind.FactionDecisionWake
        );
        EstablishPresenceObjectiveState assigned = faction.PresenceObjective! with
        {
            Status = FactionObjectiveStatus.Assigned,
            AssignedShipId = ship.InstanceId,
            AssignedOrderId = orderId,
        };
        SimulationState candidate = travel.CandidateState.ReplaceShip(
            ship.InstanceId,
            travelingShip with
            {
                ActiveOrder = order,
            }
        );
        candidate = candidate.ReplaceFaction(
            faction.Id,
            faction with
            {
                PresenceObjective = assigned,
                PendingDecisionWake = new PendingFactionDecisionWake(wake.Id, wake.DueTime),
            }
        );
        return new FactionAssignmentApplicationResult(
            FactionAssignmentApplicationOutcome.Accepted,
            candidate with
            {
                Scheduler = scheduler,
                OrderIdAllocator = allocator,
            }
        );
    }

    private static SimulationState ScheduleNextFactionOpportunity(SimulationState state, FactionState faction)
    {
        SimulationTime? next = FindNextFactionOpportunity(state, faction);
        if (next is null)
        {
            return state;
        }

        (SimulationScheduler scheduler, ScheduledWork wake) = state.Scheduler.Schedule(
            next.Value,
            ScheduledWorkTarget.ForFaction(faction.Id),
            ScheduledWorkKind.FactionDecisionWake
        );
        return state.ReplaceFaction(
            faction.Id,
            faction with
            {
                PendingDecisionWake = new PendingFactionDecisionWake(wake.Id, wake.DueTime),
            }
        ) with
        {
            Scheduler = scheduler,
        };
    }

    internal static SimulationTime? FindNextFactionOpportunity(SimulationState state, FactionState faction) =>
        state
            .Ships.Where(ship => ship.DirectControllerFactionId == faction.Id)
            .Select(NextReleaseBoundary)
            .Where(boundary => boundary is not null && boundary.Value.Milliseconds > state.Time.Milliseconds)
            .OrderBy(boundary => boundary!.Value.Milliseconds)
            .FirstOrDefault();

    private static SimulationTime? NextReleaseBoundary(ShipState ship) =>
        ship.ActiveOrder switch
        {
            HoldUntilOrder hold => hold.Until,
            TravelToOrder when ship.StrategicState is TravelingState traveling => traveling.Travel.ExpectedArrival,
            null when ship.StrategicState is TravelingState traveling => traveling.Travel.ExpectedArrival,
            _ => null,
        };

    private static FactionAssignmentApplicationResult Rejected(
        FactionAssignmentApplicationOutcome outcome,
        SimulationState state
    ) => new(outcome, state);

    private void RememberLatestFactionDecision(IReadOnlyList<ScheduledConsequenceTrace> traces)
    {
        FactionAssignmentDecisionExplanation? latest = traces
            .Select(trace => trace.FactionDecision)
            .LastOrDefault(decision => decision is not null);
        if (latest is not null)
        {
            LastFactionDecisionExplanation = latest;
        }
    }
}
