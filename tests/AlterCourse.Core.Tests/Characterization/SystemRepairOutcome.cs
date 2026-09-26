namespace AlterCourse.Core.Tests.Characterization;

/// <summary>
/// FROZEN M6A BASELINE VOCABULARY — not a Core API. The pre-substrate repair outcomes with their base member names
/// and values; <see cref="M6ABaselineProbe"/> maps Core's <c>NotRepairable</c> to <see cref="UnsupportedSystem"/>
/// (the base generation-repair case). See <see cref="PowerAllocationOutcome"/> for the name-binding rationale.
/// </summary>
internal enum SystemRepairOutcome
{
    Accepted = 1,
    RepairAlreadyActive = 2,
    UnsupportedSystem = 3,
    TargetDoesNotImproveCondition = 4,
}
