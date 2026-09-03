namespace AlterCourse.Core.Gameplay;

/// <summary>Describes the locked validation and response result of a hail request.</summary>
public enum HailOutcome
{
    /// <summary>The player has no contact with the supplied local identity.</summary>
    ContactNotFound = 1,

    /// <summary>The retained contact is not currently observable.</summary>
    ContactNotCurrent = 2,

    /// <summary>The current contact has not been identified.</summary>
    ContactNotIdentified = 3,

    /// <summary>The target cannot or will not acknowledge this bounded hail.</summary>
    NoResponse = 4,

    /// <summary>The target acknowledged and received the player's transmitted identity.</summary>
    Acknowledged = 5,
}
