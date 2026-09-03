using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.AI;

/// <summary>Correlates one autonomous contact decision to exact scheduled work.</summary>
internal sealed record ShipContactDecisionWake(ScheduledWorkId ScheduledWorkId, SimulationTime DueTime);
