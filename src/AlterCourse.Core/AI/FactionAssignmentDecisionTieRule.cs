namespace AlterCourse.Core.AI;

/// <summary>Identifies the stable ordering used to choose among eligible assignment candidates.</summary>
public enum FactionAssignmentDecisionTieRule
{
    /// <summary>Prefer the shortest direct route, then the lowest persistent ship identity.</summary>
    ShortestRouteThenLowestShipId = 1,
}
