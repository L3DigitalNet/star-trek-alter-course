using AlterCourse.Core.Gameplay;

namespace AlterCourse.Core.Tests.Characterization;

/// <summary>
/// FROZEN M6A BASELINE VOCABULARY — not a Core API. A power command's result expressed in the frozen outcome
/// vocabulary, so scenario code written against the M6A shape reads it unchanged.
/// </summary>
internal sealed record PowerAllocationResult(
    PowerAllocationOutcome Outcome,
    IReadOnlyList<PlayerAdvanceEvent> ResolvedEvents
);
