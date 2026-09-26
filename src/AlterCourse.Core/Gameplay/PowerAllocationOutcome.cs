namespace AlterCourse.Core.Gameplay;

/// <summary>Outcome of an exact, Balanced, or priority power-allocation command.</summary>
/// <remarks>
/// Numeric values 2, 3, 6, and 7 belonged to retired per-kind demand members and are deliberately not reused, so a
/// stale integer can never be read as a different generic outcome.
/// </remarks>
public enum PowerAllocationOutcome
{
    /// <summary>The allocation was applied.</summary>
    Accepted = 1,

    /// <summary>The allocation total exceeds available power.</summary>
    AvailablePowerExceeded = 4,

    /// <summary>The ship's current speed exceeds the maximum the allocation would leave.</summary>
    CurrentSpeedExceedsResultingMaximum = 5,

    /// <summary>An installed consumer's allocation exceeds its authored demand.</summary>
    ConsumerDemandExceeded = 8,

    /// <summary>The allocation omits an installed consumer.</summary>
    IncompleteAllocation = 9,

    /// <summary>The allocation names an identity that is not an installed consumer on this ship.</summary>
    UnknownConsumer = 10,
}
