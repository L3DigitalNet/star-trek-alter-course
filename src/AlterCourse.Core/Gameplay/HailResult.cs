namespace AlterCourse.Core.Gameplay;

/// <summary>Returns the typed result of hailing one player-local contact.</summary>
public readonly record struct HailResult(HailOutcome Outcome);
