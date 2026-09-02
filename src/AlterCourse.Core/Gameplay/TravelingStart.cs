using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares active strategic travel whose arrival is derived from the map route.</summary>
public sealed record TravelingStart(LocationId Origin, LocationId Destination, SimulationTime Departure)
    : ShipStrategicStart;
