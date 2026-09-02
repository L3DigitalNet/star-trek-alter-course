namespace AlterCourse.Core.Gameplay;

/// <summary>Returns the typed result of requesting travel.</summary>
public readonly record struct TravelRequestResult(TravelOutcome Outcome);
