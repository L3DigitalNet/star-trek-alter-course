namespace AlterCourse.Core.Player;

/// <summary>Projects one stable Engineering action and its current availability.</summary>
public sealed record EngineeringActionProjection(
    EngineeringAction Action,
    bool IsAvailable,
    EngineeringActionUnavailableReason? UnavailableReason = null
);
