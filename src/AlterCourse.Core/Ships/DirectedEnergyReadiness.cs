using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Readiness of one installed directed-energy weapon: the earliest time it may fire again.</summary>
internal sealed record DirectedEnergyReadiness(InstalledSystemId Weapon, SimulationTime ReadyAt);
