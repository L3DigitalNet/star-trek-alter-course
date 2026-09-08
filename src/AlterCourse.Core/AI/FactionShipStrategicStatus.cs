namespace AlterCourse.Core.AI;

/// <summary>Describes the assignment-relevant strategic state visible to a controlling faction.</summary>
public enum FactionShipStrategicStatus
{
    /// <summary>The ship is currently at one strategic location.</summary>
    AtLocation = 1,

    /// <summary>The ship is physically traveling between locations.</summary>
    Traveling = 2,
}
