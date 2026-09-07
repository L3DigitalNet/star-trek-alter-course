using AlterCourse.Core.Factions;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.AI;

/// <summary>Evaluates deterministic investigation response from bounded actor-safe faction facts.</summary>
public static class FactionInvestigationPolicy
{
    /// <summary>Returns an optional typed proposal and complete explanation without mutating the supplied snapshot.</summary>
    public static FactionInvestigationDecisionExplanation Evaluate(FactionInvestigationDecisionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var reports = new List<FactionInvestigationReportEvaluation>(input.ReceivedReports.Count);
        var candidates = new List<FactionInvestigationDecisionCandidate>();

        if (input.Posture == ObservationResponsePosture.Disabled)
        {
            return BoundResult(
                input,
                FactionInvestigationReportReason.PostureDisabled,
                FactionInvestigationDecisionOutcome.PostureDisabled
            );
        }

        if (input.HasActiveInvestigation)
        {
            return BoundResult(
                input,
                FactionInvestigationReportReason.ActiveInvestigationExists,
                FactionInvestigationDecisionOutcome.ActiveInvestigationExists
            );
        }

        return EvaluateAvailableSlot(input, reports, candidates);
    }

    private static FactionInvestigationDecisionExplanation EvaluateAvailableSlot(
        FactionInvestigationDecisionInput input,
        List<FactionInvestigationReportEvaluation> reports,
        List<FactionInvestigationDecisionCandidate> candidates
    )
    {
        bool foundActionableReport = false;
        foreach (ReceivedObservationReport received in input.ReceivedReports)
        {
            FactionInvestigationReportReason? rejection = EvaluateReport(input, received);
            if (rejection is not null)
            {
                reports.Add(Evaluation(received, rejection.Value));
                continue;
            }

            foundActionableReport = true;
            FactionInvestigationDecisionCandidate[] reportCandidates = input
                .Assets.Select(asset => EvaluateCandidate(input, received, asset))
                .ToArray();
            candidates.AddRange(reportCandidates);
            FactionInvestigationDecisionCandidate? selected = reportCandidates
                .Where(candidate => candidate.IsEligible)
                .OrderBy(candidate => candidate.DirectRouteDuration!.Value.Milliseconds)
                .ThenBy(candidate => candidate.ShipId.Value)
                .FirstOrDefault();
            if (selected is null)
            {
                reports.Add(Evaluation(received, FactionInvestigationReportReason.NoEligibleResponder));
                continue;
            }

            reports.Add(Evaluation(received, FactionInvestigationReportReason.Selected));
            var proposal = new FactionInvestigationProposal(
                input.FactionId,
                received.Report,
                received.ReceivedAt,
                selected.ShipId,
                selected.CurrentLocationId!.Value,
                received.Report.ObservedAtLocationId,
                input.DecisionTime
            );
            return Result(
                input,
                reports,
                candidates,
                FactionInvestigationDecisionOutcome.InvestigationProposed,
                proposal
            );
        }

        return Result(
            input,
            reports,
            candidates,
            foundActionableReport
                ? FactionInvestigationDecisionOutcome.NoEligibleResponder
                : FactionInvestigationDecisionOutcome.NoActionableReport,
            null
        );
    }

    private static FactionInvestigationDecisionExplanation BoundResult(
        FactionInvestigationDecisionInput input,
        FactionInvestigationReportReason reportReason,
        FactionInvestigationDecisionOutcome outcome
    ) => Result(input, input.ReceivedReports.Select(report => Evaluation(report, reportReason)), [], outcome, null);

    private static FactionInvestigationReportReason? EvaluateReport(
        FactionInvestigationDecisionInput input,
        ReceivedObservationReport received
    )
    {
        if (received.Report.ObservedAt.Milliseconds > input.DecisionTime.Milliseconds)
        {
            return FactionInvestigationReportReason.FutureObservation;
        }

        if (received.ReceivedAt.Milliseconds > input.DecisionTime.Milliseconds)
        {
            return FactionInvestigationReportReason.ReceivedAfterDecision;
        }

        if (
            input.DecisionTime.Milliseconds - received.Report.ObservedAt.Milliseconds
            >= FactionObservationState.ReportFreshnessMilliseconds
        )
        {
            return FactionInvestigationReportReason.Expired;
        }

        if (received.Handling == ObservationReportHandling.Handled)
        {
            return FactionInvestigationReportReason.Handled;
        }

        ObservationLocationCompletionWatermark? watermark = input.CompletionWatermarks.FirstOrDefault(value =>
            value.LocationId == received.Report.ObservedAtLocationId
        );
        return
            watermark is not null && received.Report.ObservedAt.Milliseconds <= watermark.ObservedThrough.Milliseconds
            ? FactionInvestigationReportReason.CoveredByCompletionWatermark
            : null;
    }

    private static FactionInvestigationDecisionCandidate EvaluateCandidate(
        FactionInvestigationDecisionInput input,
        ReceivedObservationReport received,
        FactionShipAssignmentSnapshot asset
    )
    {
        FactionInvestigationCandidateReason reason;
        SimulationDuration? duration = null;
        if (asset.ShipId == received.Report.ObserverShipId)
        {
            reason = FactionInvestigationCandidateReason.ReportingObserverRejected;
        }
        else if (asset.IsPlayerShip)
        {
            reason = FactionInvestigationCandidateReason.PlayerShipRejected;
        }
        else if (asset.HasActiveOrder)
        {
            reason = FactionInvestigationCandidateReason.AlreadyCommitted;
        }
        else if (asset.StrategicStatus != FactionShipStrategicStatus.AtLocation)
        {
            reason = FactionInvestigationCandidateReason.NotAtLocation;
        }
        else if (asset.CurrentLocationId == received.Report.ObservedAtLocationId)
        {
            reason = FactionInvestigationCandidateReason.Eligible;
            duration = new SimulationDuration(0);
        }
        else
        {
            FactionKnownRouteSnapshot? route = input.KnownRoutes.FirstOrDefault(value =>
                value.Connects(asset.CurrentLocationId!.Value, received.Report.ObservedAtLocationId)
            );
            if (route is null)
            {
                reason = FactionInvestigationCandidateReason.DirectRouteUnavailable;
            }
            else
            {
                reason = FactionInvestigationCandidateReason.Eligible;
                duration = route.Duration;
            }
        }

        return new FactionInvestigationDecisionCandidate(
            received.Report.ReportId,
            asset.ShipId,
            asset.CurrentLocationId,
            reason,
            duration
        );
    }

    private static FactionInvestigationReportEvaluation Evaluation(
        ReceivedObservationReport report,
        FactionInvestigationReportReason reason
    ) => new(report, reason);

    private static FactionInvestigationDecisionExplanation Result(
        FactionInvestigationDecisionInput input,
        IEnumerable<FactionInvestigationReportEvaluation> reports,
        IEnumerable<FactionInvestigationDecisionCandidate> candidates,
        FactionInvestigationDecisionOutcome outcome,
        FactionInvestigationProposal? proposal
    ) => new(input, reports, candidates, outcome, proposal);
}
