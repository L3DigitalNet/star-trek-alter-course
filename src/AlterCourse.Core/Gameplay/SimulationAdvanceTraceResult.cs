namespace AlterCourse.Core.Gameplay;

internal readonly record struct SimulationAdvanceTraceResult(
    SimulationState State,
    IReadOnlyList<ScheduledConsequenceTrace> Traces,
    IReadOnlyList<PlayerAdvanceEvent> PlayerEvents
);
