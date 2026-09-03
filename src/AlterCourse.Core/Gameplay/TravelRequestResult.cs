namespace AlterCourse.Core.Gameplay;

/// <summary>Returns the typed result of requesting travel.</summary>
public readonly record struct TravelRequestResult(
    TravelOutcome Outcome,
    IReadOnlyList<PlayerAdvanceEvent> ResolvedEvents
)
{
    /// <summary>Creates a result with no command-boundary events.</summary>
    public TravelRequestResult(TravelOutcome outcome)
        : this(outcome, Array.Empty<PlayerAdvanceEvent>()) { }
}
