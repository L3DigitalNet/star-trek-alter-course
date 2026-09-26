using AlterCourse.Core.Quantities;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Defines immutable power demand and repair timing for the concrete engineering systems.</summary>
public sealed record ShipEngineeringDefinition
{
    /// <summary>Initializes authored engineering values.</summary>
    public ShipEngineeringDefinition(
        PowerUnits nominalGeneration,
        PowerUnits nominalSensorDemand,
        PowerUnits nominalImpulseDemand,
        SimulationDuration sensorRepairDuration,
        SimulationDuration impulseRepairDuration
    )
    {
        if (nominalGeneration.Value == 0 || nominalSensorDemand.Value == 0 || nominalImpulseDemand.Value == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nominalGeneration),
                "Authored generation and consumer demands must be positive."
            );
        }

        ValidateDuration(sensorRepairDuration, nameof(sensorRepairDuration));
        ValidateDuration(impulseRepairDuration, nameof(impulseRepairDuration));
        NominalGeneration = nominalGeneration;
        NominalSensorDemand = nominalSensorDemand;
        NominalImpulseDemand = nominalImpulseDemand;
        SensorRepairDuration = sensorRepairDuration;
        ImpulseRepairDuration = impulseRepairDuration;
    }

    /// <summary>Initializes all four authored consumers and their repair timings.</summary>
    public ShipEngineeringDefinition(
        PowerUnits nominalGeneration,
        PowerUnits nominalSensorDemand,
        PowerUnits nominalImpulseDemand,
        PowerUnits nominalShieldDemand,
        PowerUnits nominalDirectedEnergyDemand,
        SimulationDuration sensorRepairDuration,
        SimulationDuration impulseRepairDuration,
        SimulationDuration shieldRepairDuration,
        SimulationDuration directedEnergyRepairDuration
    )
        : this(
            nominalGeneration,
            nominalSensorDemand,
            nominalImpulseDemand,
            sensorRepairDuration,
            impulseRepairDuration
        )
    {
        if (nominalShieldDemand.Value == 0 || nominalDirectedEnergyDemand.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalShieldDemand), "Combat demands must be positive.");
        }
        ValidateDuration(shieldRepairDuration, nameof(shieldRepairDuration));
        ValidateDuration(directedEnergyRepairDuration, nameof(directedEnergyRepairDuration));
        NominalShieldDemand = nominalShieldDemand;
        NominalDirectedEnergyDemand = nominalDirectedEnergyDemand;
        ShieldRepairDuration = shieldRepairDuration;
        DirectedEnergyRepairDuration = directedEnergyRepairDuration;
    }

    /// <summary>Gets nominal shield power demand; zero denotes absent legacy capacity.</summary>
    public PowerUnits NominalShieldDemand { get; }

    /// <summary>Gets nominal directed-energy power demand; zero denotes absent legacy capacity.</summary>
    public PowerUnits NominalDirectedEnergyDemand { get; }

    /// <summary>Gets full shield repair duration.</summary>
    public SimulationDuration ShieldRepairDuration { get; }

    /// <summary>Gets full directed-energy repair duration.</summary>
    public SimulationDuration DirectedEnergyRepairDuration { get; }

    /// <summary>Gets nominal generated power.</summary>
    public PowerUnits NominalGeneration { get; }

    /// <summary>Gets nominal sensor power demand.</summary>
    public PowerUnits NominalSensorDemand { get; }

    /// <summary>Gets nominal impulse-propulsion power demand.</summary>
    public PowerUnits NominalImpulseDemand { get; }

    /// <summary>Gets full sensor repair duration.</summary>
    public SimulationDuration SensorRepairDuration { get; }

    /// <summary>Gets full impulse-propulsion repair duration.</summary>
    public SimulationDuration ImpulseRepairDuration { get; }

    /// <summary>Gets the authored repair duration for one repairable system.</summary>
    public SimulationDuration RepairDurationFor(ShipSystemId systemId) =>
        systemId == ShipSystemId.Sensors ? SensorRepairDuration
        : systemId == ShipSystemId.ImpulsePropulsion ? ImpulseRepairDuration
        : systemId == ShipSystemId.Shields && NominalShieldDemand.Value > 0 ? ShieldRepairDuration
        : systemId == ShipSystemId.DirectedEnergyWeapons && NominalDirectedEnergyDemand.Value > 0
            ? DirectedEnergyRepairDuration
        : throw new ArgumentException("Absent systems and power generation repair is unsupported.", nameof(systemId));

    private static void ValidateDuration(SimulationDuration duration, string parameterName)
    {
        if (duration.Milliseconds <= 0 || duration.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new ArgumentException(
                "Repair duration must be positive and align to the fixed simulation step.",
                parameterName
            );
        }
    }
}
