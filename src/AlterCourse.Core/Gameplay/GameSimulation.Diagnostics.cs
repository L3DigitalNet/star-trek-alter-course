using AlterCourse.Core.AI;
using Microsoft.Extensions.Logging;

namespace AlterCourse.Core.Gameplay;

public sealed partial class GameSimulation
{
    private void LogDecisionTraces(IReadOnlyList<ScheduledConsequenceTrace> traces)
    {
        if (_diagnosticLogger is null)
        {
            return;
        }
        foreach (ScheduledConsequenceTrace trace in traces)
        {
            if (trace.ContactDecision is { } contact)
            {
                LogContactDecision(contact);
            }
            if (trace.FactionDecision is { } faction)
            {
                LogFactionDecision(faction);
            }
            if (trace.FactionInvestigationDecision is { } investigation)
            {
                LogInvestigationDecision(investigation);
            }
            if (trace.CombatDecision is { } combat)
            {
                LogCombatDecision(trace, combat);
            }
        }
    }

    private void LogFactionDecision(FactionAssignmentDecisionExplanation faction) =>
        EmitDiagnostic(() =>
        {
            FactionDecisionLog(
                _diagnosticLogger!,
                faction.Outcome,
                faction.ActorKnownFacts.DecisionTime.Milliseconds,
                faction.ActorKnownFacts.FactionId.Value,
                faction.Proposal?.ShipId.Value,
                faction.TieRule,
                faction.RandomnessUsed
            );
            if (_diagnosticLogger!.IsEnabled(LogLevel.Debug))
            {
                foreach (FactionAssignmentDecisionCandidate candidate in faction.Candidates)
                {
                    CandidateLog(
                        _diagnosticLogger!,
                        faction.ActorKnownFacts.DecisionTime.Milliseconds,
                        faction.ActorKnownFacts.FactionId.Value,
                        "FactionAssignment",
                        candidate.ShipId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "RouteDurationMilliseconds",
                        candidate.DirectRouteDuration?.Milliseconds,
                        candidate.Reason.ToString()
                    );
                }
            }
        });

    private void LogInvestigationDecision(FactionInvestigationDecisionExplanation investigation) =>
        EmitDiagnostic(() =>
        {
            InvestigationDecisionLog(
                _diagnosticLogger!,
                investigation.Outcome,
                investigation.ActorKnownFacts.DecisionTime.Milliseconds,
                investigation.ActorKnownFacts.FactionId.Value,
                investigation.TieRule,
                investigation.RandomnessUsed
            );
            if (_diagnosticLogger!.IsEnabled(LogLevel.Debug))
            {
                foreach (FactionInvestigationDecisionCandidate candidate in investigation.Candidates)
                {
                    CandidateLog(
                        _diagnosticLogger!,
                        investigation.ActorKnownFacts.DecisionTime.Milliseconds,
                        investigation.ActorKnownFacts.FactionId.Value,
                        "FactionInvestigation",
                        candidate.ShipId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "RouteDurationMilliseconds",
                        candidate.DirectRouteDuration?.Milliseconds,
                        candidate.Reason.ToString()
                    );
                }
            }
        });

    private void LogCombatDecision(ScheduledConsequenceTrace trace, DefensiveCombatDecisionExplanation combat) =>
        EmitDiagnostic(() =>
        {
            CombatDecisionLog(
                _diagnosticLogger!,
                combat.SelectedAction,
                trace.ResolutionTime.Milliseconds,
                trace.Target.ShipId?.Value,
                combat.ContactId.Value,
                combat.TieRule,
                combat.ApplicationOutcome
            );
            if (_diagnosticLogger!.IsEnabled(LogLevel.Debug))
            {
                foreach (DefensiveCombatDecisionCandidate candidate in combat.Candidates)
                {
                    CandidateLog(
                        _diagnosticLogger!,
                        trace.ResolutionTime.Milliseconds,
                        trace.Target.ShipId?.Value ?? 0,
                        "DefensiveCombat",
                        candidate.Action.ToString(),
                        "Rank",
                        candidate.Rank,
                        candidate.FireRejection?.ToString() ?? "None"
                    );
                    foreach (DefensiveCombatConstraintEvaluation constraint in candidate.Constraints)
                    {
                        ConstraintLog(
                            _diagnosticLogger!,
                            trace.ResolutionTime.Milliseconds,
                            trace.Target.ShipId?.Value ?? 0,
                            "DefensiveCombat",
                            candidate.Action.ToString(),
                            constraint.Constraint.ToString(),
                            constraint.Satisfied
                        );
                    }
                }
            }
        });

    private void LogContactDecision(ShipContactDecisionExplanation decision) =>
        EmitDiagnostic(() =>
        {
            ContactDecisionLog(
                _diagnosticLogger!,
                decision.SelectedAction,
                decision.DecisionTime.Milliseconds,
                decision.DecidingShipId.Value,
                decision.PrimaryContactId?.Value,
                decision.TieRule,
                decision.RandomnessUsed
            );
            if (_diagnosticLogger!.IsEnabled(LogLevel.Debug))
            {
                foreach (ShipContactDecisionCandidate candidate in decision.Candidates)
                {
                    CandidateLog(
                        _diagnosticLogger,
                        decision.DecisionTime.Milliseconds,
                        decision.DecidingShipId.Value,
                        "ShipContact",
                        candidate.Action.ToString(),
                        "Score",
                        candidate.Score,
                        candidate.PolicyReason.ToString()
                    );
                    foreach (ShipContactConstraintEvaluation constraint in candidate.Constraints)
                    {
                        ConstraintLog(
                            _diagnosticLogger,
                            decision.DecisionTime.Milliseconds,
                            decision.DecidingShipId.Value,
                            "ShipContact",
                            candidate.Action.ToString(),
                            constraint.Constraint.ToString(),
                            constraint.Satisfied
                        );
                    }
                }
            }
        });

    private void EmitDiagnostic(Action emit)
    {
        // These observations run only after Commit. A provider cannot turn a committed result into a domain failure.
        try
        {
            if (_diagnosticLogger?.IsEnabled(LogLevel.Information) == true)
            {
                emit();
            }
        }
        catch (Exception)
        { /* Logging is optional; domain failures still follow their original path. */
        }
    }

    [LoggerMessage(
        1400,
        LogLevel.Information,
        "Contact decision {SelectedAction} at {SimulationTimeMilliseconds} actor {ActorId} contact {ContactId} tie {TieRule} random {RandomnessUsed}",
        EventName = "ShipContactDecision"
    )]
    private static partial void ContactDecisionLog(
        ILogger logger,
        ShipContactDecisionAction? selectedAction,
        long simulationTimeMilliseconds,
        long actorId,
        long? contactId,
        ShipContactDecisionTieRule tieRule,
        bool randomnessUsed
    );

    [LoggerMessage(
        1401,
        LogLevel.Information,
        "Faction decision {Outcome} at {SimulationTimeMilliseconds} actor {ActorId} selected ship {SelectedShipId} tie {TieRule} random {RandomnessUsed}",
        EventName = "FactionAssignmentDecision"
    )]
    private static partial void FactionDecisionLog(
        ILogger logger,
        FactionAssignmentDecisionOutcome outcome,
        long simulationTimeMilliseconds,
        long actorId,
        long? selectedShipId,
        FactionAssignmentDecisionTieRule tieRule,
        bool randomnessUsed
    );

    [LoggerMessage(
        1402,
        LogLevel.Information,
        "Investigation decision {Outcome} at {SimulationTimeMilliseconds} actor {ActorId} tie {TieRule} random {RandomnessUsed}",
        EventName = "FactionInvestigationDecision"
    )]
    private static partial void InvestigationDecisionLog(
        ILogger logger,
        FactionInvestigationDecisionOutcome outcome,
        long simulationTimeMilliseconds,
        long actorId,
        FactionInvestigationDecisionTieRule tieRule,
        bool randomnessUsed
    );

    [LoggerMessage(
        1403,
        LogLevel.Information,
        "Combat decision {SelectedAction} at {SimulationTimeMilliseconds} actor {ActorId} contact {ContactId} tie {TieRule} application {ApplicationOutcome}",
        EventName = "DefensiveCombatDecision"
    )]
    private static partial void CombatDecisionLog(
        ILogger logger,
        DefensiveCombatDecisionAction selectedAction,
        long simulationTimeMilliseconds,
        long? actorId,
        long contactId,
        DefensiveCombatDecisionTieRule tieRule,
        FireDirectedEnergyOutcome? applicationOutcome
    );

    [LoggerMessage(
        1410,
        LogLevel.Debug,
        "Decision {DecisionKind} candidate {Candidate} ranking {RankingMetric} value {RankingValue} reason {Reason} at {SimulationTimeMilliseconds} actor {ActorId}",
        EventName = "DecisionCandidate"
    )]
    private static partial void CandidateLog(
        ILogger logger,
        long simulationTimeMilliseconds,
        long actorId,
        string decisionKind,
        string candidate,
        string rankingMetric,
        long? rankingValue,
        string reason
    );

    [LoggerMessage(
        1411,
        LogLevel.Debug,
        "Decision {DecisionKind} candidate {Candidate} constraint {Constraint} satisfied {Satisfied} at {SimulationTimeMilliseconds} actor {ActorId}",
        EventName = "DecisionConstraint"
    )]
    private static partial void ConstraintLog(
        ILogger logger,
        long simulationTimeMilliseconds,
        long actorId,
        string decisionKind,
        string candidate,
        string constraint,
        bool satisfied
    );
}
