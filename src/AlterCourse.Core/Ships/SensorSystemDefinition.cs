using AlterCourse.Core.Quantities;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Defines a sensor component: a power consumer with passive range and active-scan timing.</summary>
public sealed record SensorSystemDefinition : SystemDefinition
{
    /// <summary>Initializes a sensor definition.</summary>
    /// <exception cref="ArgumentException">The scan duration is not positive or not fixed-step aligned.</exception>
    public SensorSystemDefinition(
        SystemDefinitionId id,
        string componentLabel,
        int commonOrder,
        bool conditionParticipation,
        SystemRepairCapability? repair,
        SystemPowerDemand power,
        DistanceKilometers passiveRange,
        SimulationDuration activeScanDuration
    )
        : base(
            id,
            componentLabel,
            commonOrder,
            conditionParticipation,
            repair,
            power ?? throw new ArgumentNullException(nameof(power))
        )
    {
        if (
            activeScanDuration.Milliseconds <= 0
            || activeScanDuration.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0
        )
        {
            throw new ArgumentException(
                "Active scan duration must be positive and fixed-step aligned.",
                nameof(activeScanDuration)
            );
        }

        PassiveRange = passiveRange;
        ActiveScanDuration = activeScanDuration;
    }

    /// <inheritdoc />
    public override ShipSystemKind Kind => ShipSystemKind.Sensors;

    /// <summary>Gets the passive detection range at full capability.</summary>
    public DistanceKilometers PassiveRange { get; }

    /// <summary>Gets the time an active scan takes to complete.</summary>
    public SimulationDuration ActiveScanDuration { get; }
}
