namespace AlterCourse.Core.Factions;

/// <summary>Identifies the durable lifecycle of an establish-presence objective.</summary>
internal enum FactionObjectiveStatus
{
    Pending = 1,
    Assigned = 2,
    Satisfied = 3,
}
