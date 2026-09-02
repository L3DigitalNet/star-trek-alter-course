using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Player;

/// <summary>Provides one fresh read-only player-known simulation view.</summary>
public sealed record PlayerProjection
{
    internal PlayerProjection(
        SimulationTime simulationTime,
        StrategicProjection strategic,
        PlayerShipProjection ship,
        IReadOnlyList<PlayerAction> availableActions
    ) => (SimulationTime, Strategic, Ship, AvailableActions) = (simulationTime, strategic, ship, availableActions);

    /// <summary>Gets authoritative simulation time.</summary>
    public SimulationTime SimulationTime { get; }

    /// <summary>Gets player-known strategic state.</summary>
    public StrategicProjection Strategic { get; }

    /// <summary>Gets the player ship view.</summary>
    public PlayerShipProjection Ship { get; }

    /// <summary>Gets fresh read-only action suggestions.</summary>
    public IReadOnlyList<PlayerAction> AvailableActions { get; }
}
