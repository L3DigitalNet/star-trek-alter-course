namespace AlterCourse.Core.Gameplay;

/// <summary>Describes why player-event advancement stopped.</summary>
public enum AdvanceUntilOutcome
{
    /// <summary>At least one player-targeted consequence was resolved at its earliest boundary.</summary>
    PlayerEventResolved = 1,

    /// <summary>No player-relevant event remains.</summary>
    NoPlayerEvent = 2,
}
