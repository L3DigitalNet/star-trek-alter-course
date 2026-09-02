namespace AlterCourse.Core.Gameplay;

internal readonly record struct ShipTravelApplicationResult(TravelOutcome Outcome, SimulationState CandidateState);
