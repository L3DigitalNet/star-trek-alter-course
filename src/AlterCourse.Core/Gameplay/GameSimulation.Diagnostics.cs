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
            EmitDiagnostic(() =>
            {
                if (trace.FactionDecision is { } faction)
                {
                    FactionDecisionLog(
                        _diagnosticLogger,
                        faction.Outcome,
                        faction.ActorKnownFacts.DecisionTime.Milliseconds,
                        faction.ActorKnownFacts.FactionId.Value,
                        faction.Proposal?.ShipId.Value,
                        faction.TieRule,
                        faction.RandomnessUsed
                    );
                }
                if (trace.FactionInvestigationDecision is { } investigation)
                {
                    InvestigationDecisionLog(
                        _diagnosticLogger,
                        investigation.Outcome,
                        investigation.ActorKnownFacts.DecisionTime.Milliseconds,
                        investigation.ActorKnownFacts.FactionId.Value,
                        investigation.TieRule,
                        investigation.RandomnessUsed
                    );
                }
                if (trace.CombatDecision is { } combat)
                {
                    CombatDecisionLog(
                        _diagnosticLogger,
                        combat.SelectedAction,
                        trace.ResolutionTime.Milliseconds,
                        trace.Target.ShipId?.Value,
                        combat.ContactId.Value,
                        combat.TieRule,
                        combat.ApplicationOutcome
                    );
                }
            });
        }
    }

    private void LogContactDecision(ShipContactDecisionExplanation decision) =>
        EmitDiagnostic(() =>
            ContactDecisionLog(
                _diagnosticLogger!,
                decision.SelectedAction,
                decision.DecisionTime.Milliseconds,
                decision.DecidingShipId.Value,
                decision.PrimaryContactId?.Value,
                decision.TieRule,
                decision.RandomnessUsed
            )
        );

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
}
