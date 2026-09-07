using System.Collections.Immutable;
using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

internal sealed partial record SimulationState
{
    internal FactionState GetRequiredFaction(FactionId factionId) =>
        Factions.FirstOrDefault(faction => faction.Id == factionId)
        ?? throw new KeyNotFoundException($"No faction exists with identity '{factionId.Value}'.");

    internal SimulationState ReplaceFaction(FactionId factionId, FactionState replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        int index = -1;
        for (int candidateIndex = 0; candidateIndex < Factions.Length; candidateIndex++)
        {
            if (Factions[candidateIndex].Id == factionId)
            {
                index = candidateIndex;
                break;
            }
        }
        if (index < 0 || replacement.Id != factionId)
        {
            throw new InvalidOperationException("Faction replacement must preserve an existing faction identity.");
        }

        return this with
        {
            Factions = Factions.SetItem(index, replacement),
        };
    }

    private static ImmutableArray<FactionState> MaterializeFactions(IEnumerable<FactionState> factions)
    {
        FactionState[] materialized = factions.Take(MaximumFactions + 1).ToArray();
        if (materialized.Length > MaximumFactions || materialized.Any(faction => faction is null))
        {
            throw new ArgumentException(
                $"Simulation state supports at most {MaximumFactions} nonnull factions.",
                nameof(factions)
            );
        }

        if (
            materialized.Any(faction => faction.Id.Value <= 0)
            || materialized.Select(faction => faction.Id).Distinct().Count() != materialized.Length
        )
        {
            throw new ArgumentException("Simulation factions require unique initialized identities.", nameof(factions));
        }

        return [.. materialized.OrderBy(faction => faction.Id.Value)];
    }

    private void ValidateFactions(FactionDefinitionCatalog factionCatalog)
    {
        if (
            Factions.IsDefault
            || Factions.Length > MaximumFactions
            || Factions.Any(faction => faction is null || faction.Id.Value <= 0)
            || Factions.Select(faction => faction.Id).Distinct().Count() != Factions.Length
        )
        {
            throw new InvalidOperationException(
                "Simulation factions must be a bounded set of unique initialized identities."
            );
        }

        HashSet<FactionId> factionIds = [.. Factions.Select(faction => faction.Id)];
        foreach (ShipState ship in Ships)
        {
            if (ship.DirectControllerFactionId is { } controller && !factionIds.Contains(controller))
            {
                throw new InvalidOperationException("A ship direct controller must resolve to an existing faction.");
            }
        }

        if (GetRequiredShip(PlayerShipId).DirectControllerFactionId is not null)
        {
            throw new InvalidOperationException(
                "The player ship cannot have autonomous faction control in this proof."
            );
        }

        foreach (FactionState faction in Factions)
        {
            factionCatalog.GetRequired(faction.DefinitionId);
            ValidateFactionObjective(faction);
            ValidateFactionObservation(faction);
            ValidateCoordinatedFactionWake(faction);
        }

        ValidateObservationReportIdentities();
    }

    private void ValidateFactionObjective(FactionState faction)
    {
        if (faction.PresenceObjective is not { } objective)
        {
            return;
        }

        StrategicMap.GetLocation(objective.TargetLocationId);
        if (!Enum.IsDefined(objective.Status))
        {
            throw new InvalidOperationException("Faction objective status is unsupported.");
        }

        bool assigned = objective.Status == FactionObjectiveStatus.Assigned;
        bool hasBothAssignments = objective.AssignedShipId is not null && objective.AssignedOrderId is not null;
        bool hasAnyAssignment = objective.AssignedShipId is not null || objective.AssignedOrderId is not null;
        if ((assigned && !hasBothAssignments) || (!assigned && hasAnyAssignment))
        {
            throw new InvalidOperationException(
                "Only an assigned objective can retain exact ship and order correlations."
            );
        }

        if (assigned && faction.PendingDecisionWake is null)
        {
            throw new InvalidOperationException("An assigned objective requires its exact arrival-time decision wake.");
        }

        if (assigned)
        {
            ValidateAssignedObjective(faction, objective);
        }

        if (
            faction.PendingDecisionWake is { } wake
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

    private void ValidatePendingFactionWake(FactionState faction, EstablishPresenceObjectiveState objective)
    {
        if (faction.PendingDecisionWake is not { } wake)
        {
            ValidateFactionDormancy(faction, objective);
            return;
        }

        // Correlation alone cannot justify an arbitrary delay. A pending wake is either
        // ready now or tied to the same finite release boundary used by runtime scheduling.
        if (
            wake.DueTime != Time
            && (
                wake.DueTime != GameSimulation.FindNextFactionOpportunity(this, faction)
                || GameSimulation.DecideFactionAssignment(this, faction, objective).Outcome
                    == FactionAssignmentDecisionOutcome.AssignmentProposed
            )
        )
        {
            throw new InvalidOperationException(
                "A pending faction wake requires its next strategic decision boundary."
            );
        }
    }

    private void ValidateFactionDormancy(FactionState faction, EstablishPresenceObjectiveState objective)
    {
        // Losing a wake must not silently disable actionable intent. Reuse the runtime's own
        // actor-safe decision and release rules instead of maintaining a second eligibility policy.
        FactionAssignmentDecisionExplanation decision = GameSimulation.DecideFactionAssignment(
            this,
            faction,
            objective
        );
        if (
            decision.Outcome != FactionAssignmentDecisionOutcome.NoEligibleCandidate
            || GameSimulation.FindNextFactionOpportunity(this, faction) is not null
        )
        {
            throw new InvalidOperationException("An actionable pending faction objective requires a decision wake.");
        }
    }

    private void ValidateAssignedObjective(FactionState faction, EstablishPresenceObjectiveState objective)
    {
        ShipState ship = GetRequiredShip(objective.AssignedShipId!.Value);
        if (
            ship.DirectControllerFactionId != faction.Id
            || ship.InstanceId == PlayerShipId
            || ship.ActiveOrder is not TravelToOrder order
            || order.Id != objective.AssignedOrderId
            || order.Destination != objective.TargetLocationId
            || ship.StrategicState is not TravelingState
        )
        {
            throw new InvalidOperationException(
                "An assigned faction objective lacks its exact controlled TravelTo commitment."
            );
        }

        var traveling = (TravelingState)ship.StrategicState;
        PendingFactionDecisionWake wake = faction.PendingDecisionWake!;
        ScheduledWork arrival = Scheduler.OutstandingWork.Single(work =>
            work.Id == traveling.Travel.ScheduledArrivalId
        );
        ScheduledWork factionWork = Scheduler.OutstandingWork.Single(work => work.Id == wake.WorkId);
        if (
            wake.DueTime.Milliseconds > traveling.Travel.ExpectedArrival.Milliseconds
            || (wake.DueTime == traveling.Travel.ExpectedArrival && arrival.Sequence >= factionWork.Sequence)
        )
        {
            throw new InvalidOperationException(
                "Faction satisfaction must wake after its exact same-time ship arrival."
            );
        }
    }

    private void ValidateFactionScheduledWork(ScheduledWork work)
    {
        FactionState faction = GetRequiredFaction(work.Target.FactionId!.Value);
        if (work.Kind == ScheduledWorkKind.ObservationReportDelivery)
        {
            ObservationReportInFlight? delivery = faction.Observation?.InFlightReports.FirstOrDefault(item =>
                item.DeliveryWorkId == work.Id
            );
            if (delivery is null || delivery.DueTime != work.DueTime)
            {
                throw new InvalidOperationException("Report delivery work lacks its exact in-flight correlation.");
            }
            return;
        }

        PendingFactionDecisionWake? wake = faction.PendingDecisionWake;
        if (
            work.Kind != ScheduledWorkKind.FactionDecisionWake
            || wake?.WorkId != work.Id
            || wake.DueTime != work.DueTime
        )
        {
            throw new InvalidOperationException("Faction work has no exactly correlated decision wake.");
        }
    }
}
