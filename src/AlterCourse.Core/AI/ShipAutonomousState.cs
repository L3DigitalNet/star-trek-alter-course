namespace AlterCourse.Core.AI;

/// <summary>Stores optional ship posture and its single pending contact-decision wake.</summary>
internal sealed record ShipAutonomousState(
    ShipContactPosture? ContactPosture = null,
    ShipContactDecisionWake? PendingContactDecisionWake = null
)
{
    internal static ShipAutonomousState Empty { get; } = new();
}
