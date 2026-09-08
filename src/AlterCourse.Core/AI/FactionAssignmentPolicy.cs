using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.AI;

/// <summary>Evaluates deterministic establish-presence assignments from bounded actor-safe facts.</summary>
public static class FactionAssignmentPolicy
{
    /// <summary>Returns an optional typed proposal and complete explanation without mutating the supplied snapshot.</summary>
    public static FactionAssignmentDecisionExplanation Evaluate(FactionAssignmentDecisionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        FactionAssignmentDecisionCandidate[] candidates = input
            .Assets.Select(asset => EvaluateCandidate(input, asset))
            .ToArray();
        bool objectiveSatisfied = input.Assets.Any(asset =>
            !asset.IsPlayerShip
            && asset.StrategicStatus == FactionShipStrategicStatus.AtLocation
            && asset.CurrentLocationId == input.Objective.TargetLocationId
        );

        FactionAssignmentDecisionCandidate? selected = objectiveSatisfied
            ? null
            : candidates
                .Where(candidate => candidate.IsEligible)
                .OrderBy(candidate => candidate.DirectRouteDuration!.Value.Milliseconds)
                .ThenBy(candidate => candidate.ShipId.Value)
                .FirstOrDefault();
        FactionAssignmentProposal? proposal = selected is null
            ? null
            : new FactionAssignmentProposal(input.FactionId, selected.ShipId, input.Objective.TargetLocationId);
        FactionAssignmentDecisionOutcome outcome =
            objectiveSatisfied ? FactionAssignmentDecisionOutcome.ObjectiveAlreadySatisfied
            : proposal is null ? FactionAssignmentDecisionOutcome.NoEligibleCandidate
            : FactionAssignmentDecisionOutcome.AssignmentProposed;

        return new FactionAssignmentDecisionExplanation(
            input,
            candidates,
            FactionAssignmentDecisionTieRule.ShortestRouteThenLowestShipId,
            outcome,
            proposal,
            false
        );
    }

    private static FactionAssignmentDecisionCandidate EvaluateCandidate(
        FactionAssignmentDecisionInput input,
        FactionShipAssignmentSnapshot asset
    )
    {
        FactionAssignmentCandidateReason reason;
        SimulationDuration? duration = null;
        if (asset.IsPlayerShip)
        {
            reason = FactionAssignmentCandidateReason.PlayerShipRejected;
        }
        else if (asset.HasActiveOrder)
        {
            reason = FactionAssignmentCandidateReason.AlreadyCommitted;
        }
        else if (asset.StrategicStatus != FactionShipStrategicStatus.AtLocation)
        {
            reason = FactionAssignmentCandidateReason.NotAtLocation;
        }
        else if (asset.CurrentLocationId == input.Objective.TargetLocationId)
        {
            reason = FactionAssignmentCandidateReason.Eligible;
            duration = new SimulationDuration(0);
        }
        else
        {
            FactionKnownRouteSnapshot? route = input.KnownRoutes.FirstOrDefault(route =>
                route.Connects(asset.CurrentLocationId!.Value, input.Objective.TargetLocationId)
            );
            if (route is null)
            {
                reason = FactionAssignmentCandidateReason.DirectRouteUnavailable;
            }
            else
            {
                reason = FactionAssignmentCandidateReason.Eligible;
                duration = route.Duration;
            }
        }

        return new FactionAssignmentDecisionCandidate(asset.ShipId, asset.CurrentLocationId, reason, duration);
    }
}
