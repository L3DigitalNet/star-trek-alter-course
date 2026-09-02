
namespace AlterCourse.Core.Gameplay;

/// <summary>Describes the validated result of a tactical-course request.</summary>
public enum SetTacticalCourseOutcome
{
    /// <summary>The tactical motion was changed.</summary>
    Accepted = 1,

    /// <summary>Course changes are unavailable during strategic travel.</summary>
    UnavailableWhileTraveling = 2,

    /// <summary>The requested speed exceeds the ship definition maximum.</summary>
    SpeedExceedsMaximum = 3,
}
