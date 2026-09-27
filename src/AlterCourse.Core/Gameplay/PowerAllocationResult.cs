using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Gameplay;

/// <summary>Result of a power-allocation command.</summary>
/// <param name="Outcome">The command outcome.</param>
/// <param name="ResolvedEvents">Player-visible events produced by an accepted allocation.</param>
/// <param name="Consumer">The first offending installed consumer, for consumer-specific refusals.</param>
public sealed record PowerAllocationResult(
    PowerAllocationOutcome Outcome,
    IReadOnlyList<PlayerAdvanceEvent> ResolvedEvents,
    InstalledSystemId? Consumer = null
);
