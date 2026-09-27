using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using Microsoft.Extensions.Logging;

namespace AlterCourse.Godot.Gameplay.Logging;

/// <summary>Emits allowlisted application facts through injected logging without affecting gameplay recovery.</summary>
internal sealed partial class GameDiagnostics(ILogger<GameDiagnostics> logger, Action fallback)
{
    private bool _fallbackReported;

    internal void Lifecycle(bool starting, long? simulationTime, long? actorId) =>
        Emit(() => LifecycleLog(logger, starting ? "Started" : "Stopped", simulationTime, actorId));

    internal void Failure(FailureOperation operation, Exception exception, long? simulationTime, long? actorId) =>
        Emit(() => FailureLog(logger, operation, Classify(exception), simulationTime, actorId));

    internal void PersistenceCompleted(bool loading, long? simulationTime, long? actorId) =>
        Emit(() => PersistenceLog(logger, loading ? "Load" : "Save", simulationTime, actorId));

    internal void Consequence(PlayerAdvanceEvent value, long actorId) =>
        Emit(() =>
            ConsequenceLog(logger, value.Kind, value.OccurredAt.Milliseconds, actorId, value.SensorContactId?.Value)
        );

    [LoggerMessage(
        1000,
        LogLevel.Information,
        "Gameplay lifecycle {Phase} at {SimulationTimeMilliseconds} actor {ActorId}",
        EventName = "GameplayLifecycle"
    )]
    private static partial void LifecycleLog(
        ILogger logger,
        string phase,
        long? simulationTimeMilliseconds,
        long? actorId
    );

    [LoggerMessage(
        1100,
        LogLevel.Error,
        "Gameplay operation {Operation} failed with {FailureClassification} at {SimulationTimeMilliseconds} actor {ActorId}",
        EventName = "GameplayOperationFailed"
    )]
    private static partial void FailureLog(
        ILogger logger,
        FailureOperation operation,
        string failureClassification,
        long? simulationTimeMilliseconds,
        long? actorId
    );

    [LoggerMessage(
        1200,
        LogLevel.Information,
        "Persistence {Operation} completed at {SimulationTimeMilliseconds} actor {ActorId}",
        EventName = "PersistenceCompleted"
    )]
    private static partial void PersistenceLog(
        ILogger logger,
        string operation,
        long? simulationTimeMilliseconds,
        long? actorId
    );

    [LoggerMessage(
        1300,
        LogLevel.Information,
        "Player consequence {Consequence} at {SimulationTimeMilliseconds} actor {ActorId} contact {ContactId}",
        EventName = "PlayerConsequence"
    )]
    private static partial void ConsequenceLog(
        ILogger logger,
        PlayerAdvanceEventKind consequence,
        long simulationTimeMilliseconds,
        long actorId,
        long? contactId
    );

    private static string Classify(Exception exception) =>
        exception switch
        {
            GamePersistenceException persistence => persistence.Failure switch
            {
                GamePersistenceFailure.InvalidData => "InvalidExternalInput",
                GamePersistenceFailure.UnsupportedVersion => "UnsupportedVersion",
                GamePersistenceFailure.IncompatibleContent => "IncompatibleContent",
                GamePersistenceFailure.InputOutput => "RecoverableIO",
                _ => "ProgrammingDefect",
            },
            ShipContentValidationException or FactionContentValidationException => "InvalidExternalInput",
            OperationCanceledException => "Cancellation",
            IOException or UnauthorizedAccessException => "RecoverableIO",
            InvalidOperationException => "DomainInvariant",
            _ => "ProgrammingDefect",
        };

    private void Emit(Action emit)
    {
        try
        {
            // Never hand an exception or arbitrary text to a provider: both structured state and rendering can leak it.
            emit();
        }
        catch (Exception)
        {
            if (!_fallbackReported)
            {
                _fallbackReported = true;
                try
                {
                    fallback();
                }
                catch (Exception)
                { /* A diagnostic fallback cannot replace the domain outcome. */
                }
            }
        }
    }

    internal enum FailureOperation
    {
        Content,
        Save,
        Load,
        Simulation,
        Command,
    }
}
