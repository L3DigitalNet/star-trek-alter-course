namespace AlterCourse.Core.Simulation;

/// <summary>Identifies the closed domains that can own scheduled work.</summary>
internal enum ScheduledWorkTargetKind
{
    /// <summary>Targets one ship instance.</summary>
    Ship = 1,

    /// <summary>Targets one faction.</summary>
    Faction = 2,
}
