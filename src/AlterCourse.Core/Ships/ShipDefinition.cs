using AlterCourse.Core.Quantities;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Defines the immutable ship values consumed by the first gameplay slice.</summary>
public sealed record ShipDefinition
{
    /// <summary>Initializes a minimal ship definition.</summary>
    public ShipDefinition(
        ShipDefinitionId id,
        string displayName,
        SpeedKilometersPerSecond maximumTacticalSpeed,
        SensorIntegrity initialSensorIntegrity,
        SimulationDuration sensorRepairDuration
    )
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("Ship definition requires an initialized identity.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
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
        DisplayName = displayName;
        MaximumTacticalSpeed = maximumTacticalSpeed;
        InitialSensorIntegrity = initialSensorIntegrity;
        SensorRepairDuration = sensorRepairDuration;
    }

    /// <summary>Gets the stable definition identity.</summary>
    public ShipDefinitionId Id { get; }

    /// <summary>Gets the player-facing ship name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the maximum tactical speed.</summary>
    public SpeedKilometersPerSecond MaximumTacticalSpeed { get; }

    /// <summary>Gets the sensor integrity used by a new ship.</summary>
    public SensorIntegrity InitialSensorIntegrity { get; }

    /// <summary>Gets the duration of a complete active sensor repair.</summary>
    public SimulationDuration SensorRepairDuration { get; }
}
