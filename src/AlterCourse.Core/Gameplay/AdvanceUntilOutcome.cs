namespace AlterCourse.Core.Gameplay;

/// <summary>Describes why advance-until-next-event stopped.</summary>
public enum AdvanceUntilOutcome
{
    /// <summary>At least one scheduled consequence was resolved at the earliest boundary.</summary>
    ScheduledEventResolved = 1,

    /// <summary>No scheduled consequence remains.</summary>
    NoScheduledEvent = 2,
}
