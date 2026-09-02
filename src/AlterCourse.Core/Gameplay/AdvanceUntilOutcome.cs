namespace AlterCourse.Core.Gameplay;

/// <summary>Describes why advance-until-next-event stopped.</summary>
public enum AdvanceUntilOutcome
{
    /// <summary>At least one player-targeted consequence was resolved at its earliest boundary.</summary>
    ScheduledEventResolved = 1,

    /// <summary>No player-targeted scheduled consequence remains.</summary>
    NoScheduledEvent = 2,
}
