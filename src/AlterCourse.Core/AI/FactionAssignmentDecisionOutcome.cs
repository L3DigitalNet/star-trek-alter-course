namespace AlterCourse.Core.AI;

/// <summary>Describes the complete result of one establish-presence evaluation.</summary>
public enum FactionAssignmentDecisionOutcome
{
    /// <summary>An eligible ship was proposed for ordinary travel assignment.</summary>
    AssignmentProposed = 1,

    /// <summary>A directly controlled nonplayer ship already provides the required presence.</summary>
    ObjectiveAlreadySatisfied = 2,

    /// <summary>No directly controlled ship can currently receive the assignment.</summary>
    NoEligibleCandidate = 3,
}
