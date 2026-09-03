using AlterCourse.Core.Quantities;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Defines the immutable ship values consumed by the first gameplay slice.</summary>
public sealed record ShipDefinition
{
    /// <summary>Initializes a minimal ship definition.</summary>
    public ShipDefinition(
        ShipDefinitionId id,
        string designDisplayName,
        SpeedKilometersPerSecond maximumTacticalSpeed,
        DistanceKilometers passiveSensorRange,
        SimulationDuration activeScanDuration,
        SimulationDuration sensorRepairDuration
    )
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("Ship definition requires an initialized identity.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(designDisplayName);
        if (activeScanDuration.Milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeScanDuration), "Active scan duration must be positive.");
        }

        if (activeScanDuration.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new ArgumentException(
                "Active scan duration must align to the fixed simulation step.",
                nameof(activeScanDuration)
            );
        }

        if (sensorRepairDuration.Milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sensorRepairDuration), "Repair duration must be positive.");
        }

        if (sensorRepairDuration.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new ArgumentException(
                "Repair duration must align to the fixed simulation step.",
                nameof(sensorRepairDuration)
            );
        }

        Id = id;
        DesignDisplayName = designDisplayName;
        MaximumTacticalSpeed = maximumTacticalSpeed;
        PassiveSensorRange = passiveSensorRange;
        ActiveScanDuration = activeScanDuration;
        SensorRepairDuration = sensorRepairDuration;
    }

    /// <summary>Gets the stable definition identity.</summary>
    public ShipDefinitionId Id { get; }

    /// <summary>Gets the reusable player-facing ship design label.</summary>
    public string DesignDisplayName { get; }

    /// <summary>Gets the maximum tactical speed.</summary>
    public SpeedKilometersPerSecond MaximumTacticalSpeed { get; }

    /// <summary>Gets the maximum distance at which passive sensors can maintain a contact.</summary>
    public DistanceKilometers PassiveSensorRange { get; }

    /// <summary>Gets the simulation duration required to complete an active scan.</summary>
    public SimulationDuration ActiveScanDuration { get; }

    /// <summary>Gets the duration of a complete active sensor repair.</summary>
    public SimulationDuration SensorRepairDuration { get; }
}
