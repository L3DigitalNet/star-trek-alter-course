using AlterCourse.Core.Quantities;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Defines the immutable ship values consumed by the first gameplay slice.</summary>
public sealed record ShipDefinition
{
    /// <summary>Gets the maximum persisted design display-name length.</summary>
    public const int MaximumDesignDisplayNameLength = 64;

    /// <summary>Initializes a minimal ship definition.</summary>
    public ShipDefinition(
        ShipDefinitionId id,
        string designDisplayName,
        SpeedKilometersPerSecond maximumTacticalSpeed,
        DistanceKilometers passiveSensorRange,
        SimulationDuration activeScanDuration,
        ShipEngineeringDefinition engineering
    )
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("Ship definition requires an initialized identity.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(designDisplayName);
        if (designDisplayName.Length > MaximumDesignDisplayNameLength)
        {
            throw new ArgumentException(
                $"Design display name cannot exceed {MaximumDesignDisplayNameLength} characters.",
                nameof(designDisplayName)
            );
        }

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

        ArgumentNullException.ThrowIfNull(engineering);

        Id = id;
        DesignDisplayName = designDisplayName;
        MaximumTacticalSpeed = maximumTacticalSpeed;
        PassiveSensorRange = passiveSensorRange;
        ActiveScanDuration = activeScanDuration;
        Engineering = engineering;
    }

    internal ShipDefinition(
        ShipDefinitionId id,
        string designDisplayName,
        SpeedKilometersPerSecond maximumTacticalSpeed,
        DistanceKilometers passiveSensorRange,
        SimulationDuration activeScanDuration,
        SimulationDuration sensorRepairDuration
    )
        : this(
            id,
            designDisplayName,
            maximumTacticalSpeed,
            passiveSensorRange,
            activeScanDuration,
            new ShipEngineeringDefinition(
                new PowerUnits(120),
                new PowerUnits(70),
                new PowerUnits(50),
                sensorRepairDuration,
                sensorRepairDuration
            )
        ) { }

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

    /// <summary>Gets immutable engineering demand and repair timing.</summary>
    public ShipEngineeringDefinition Engineering { get; }

    internal SimulationDuration SensorRepairDuration => Engineering.SensorRepairDuration;
}
