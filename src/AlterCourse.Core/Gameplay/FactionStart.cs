using AlterCourse.Core.Factions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares one root faction and its optional initial establish-presence objective.</summary>
public sealed record FactionStart(
    FactionId Id,
    FactionDefinitionId DefinitionId,
    LocationId? PresenceTargetLocationId = null
);
