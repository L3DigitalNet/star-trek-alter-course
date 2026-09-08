namespace AlterCourse.Core.Factions;

/// <summary>Controls whether a faction participates in direct observation response.</summary>
public enum ObservationResponsePosture
{
    /// <summary>The faction neither publishes nor responds through the direct report channel.</summary>
    Disabled = 0,

    /// <summary>The faction may publish, receive, and investigate direct observation reports.</summary>
    Enabled = 1,
}
