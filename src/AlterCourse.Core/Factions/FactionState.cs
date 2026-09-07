using AlterCourse.Core.Identity;

namespace AlterCourse.Core.Factions;

/// <summary>Contains the bounded mutable state of one persistent root faction.</summary>
internal sealed record FactionState(
    FactionId Id,
    FactionDefinitionId DefinitionId,
    EstablishPresenceObjectiveState? PresenceObjective = null,
    PendingFactionDecisionWake? PendingDecisionWake = null,
    FactionObservationState? Observation = null
);
