using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Player;

/// <summary>
/// Projects one Core-owned Engineering action and its current availability. <see cref="Target"/> is the player
/// ship's local installed identity for <see cref="EngineeringOperation.Prioritize"/> and
/// <see cref="EngineeringOperation.BeginRepair"/>, and null for the ship-level operations.
/// </summary>
public sealed record EngineeringActionProjection(
    EngineeringOperation Operation,
    InstalledSystemId? Target,
    bool IsAvailable,
    EngineeringActionUnavailableReason? UnavailableReason = null
);
