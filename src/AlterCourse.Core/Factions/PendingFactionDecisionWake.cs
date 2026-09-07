using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Factions;

/// <summary>Correlates a faction decision to exactly one scheduled wake.</summary>
internal sealed record PendingFactionDecisionWake(ScheduledWorkId WorkId, SimulationTime DueTime);
