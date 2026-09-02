using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares an order that remains active until one future simulation time.</summary>
public sealed record HoldUntilOrderStart(SimulationTime Until) : ShipOrderStart;
