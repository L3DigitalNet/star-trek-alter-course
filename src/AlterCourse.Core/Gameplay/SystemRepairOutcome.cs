namespace AlterCourse.Core.Gameplay;

/// <summary>Describes the validated result of beginning one analytical system repair.</summary>
public enum SystemRepairOutcome
{
    /// <summary>The repair was scheduled.</summary>
    Accepted = 1,

    /// <summary>The ship already has its one active repair.</summary>
    RepairAlreadyActive = 2,

    /// <summary>The selected system cannot be repaired in this slice.</summary>
    UnsupportedSystem = 3,

    /// <summary>The target does not exceed current condition.</summary>
    TargetDoesNotImproveCondition = 4,
}
