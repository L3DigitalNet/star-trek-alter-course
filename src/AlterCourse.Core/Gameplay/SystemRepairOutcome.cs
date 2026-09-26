namespace AlterCourse.Core.Gameplay;

/// <summary>Outcome of a system-repair command.</summary>
public enum SystemRepairOutcome
{
    /// <summary>The repair was scheduled.</summary>
    Accepted = 1,

    /// <summary>The ship already has an active repair.</summary>
    RepairAlreadyActive = 2,

    /// <summary>The target installation's definition has no repair capability.</summary>
    NotRepairable = 3,

    /// <summary>The requested condition does not improve the target.</summary>
    TargetDoesNotImproveCondition = 4,

    /// <summary>The target identity is not installed on this ship.</summary>
    UnknownSystem = 5,
}
