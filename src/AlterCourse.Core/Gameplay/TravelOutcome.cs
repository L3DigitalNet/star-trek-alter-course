
namespace AlterCourse.Core.Gameplay;

/// <summary>Describes the validated result of a travel request.</summary>
public enum TravelOutcome
{
    /// <summary>Travel was scheduled.</summary>
    Accepted = 1,

    /// <summary>The player ship is already traveling.</summary>
    AlreadyTraveling = 2,

    /// <summary>The destination is the current location.</summary>
    SameLocation = 3,

    /// <summary>No direct route connects the current location and destination.</summary>
    RouteUnavailable = 4,
}
