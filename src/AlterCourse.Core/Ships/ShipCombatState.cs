using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Retains weapon readiness independently of autonomous contact posture, including for the player.</summary>
internal sealed record ShipCombatState(SimulationTime NextDirectedEnergyReadyAt, CombatStimulus? PendingStimulus = null)
{
    internal static ShipCombatState Empty { get; } = new(new SimulationTime(0));
}
