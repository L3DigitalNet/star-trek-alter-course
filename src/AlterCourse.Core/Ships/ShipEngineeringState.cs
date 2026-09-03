using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Ships;

/// <summary>Owns all consequential runtime engineering state for one ship.</summary>
public sealed record ShipEngineeringState
{
    /// <summary>Initializes concrete system conditions, exact allocation, and optional repair.</summary>
    public ShipEngineeringState(
        SystemCondition generationCondition,
        SystemCondition sensorCondition,
        SystemCondition impulseCondition,
        PowerAllocation allocation,
        SystemRepairState? activeRepair = null
    ) =>
        (GenerationCondition, SensorCondition, ImpulseCondition, Allocation, ActiveRepair) = (
            generationCondition,
            sensorCondition,
            impulseCondition,
            allocation,
            activeRepair
        );

    /// <summary>Gets power-generation condition.</summary>
    public SystemCondition GenerationCondition { get; init; }

    /// <summary>Gets sensor condition.</summary>
    public SystemCondition SensorCondition { get; init; }

    /// <summary>Gets impulse-propulsion condition.</summary>
    public SystemCondition ImpulseCondition { get; init; }

    /// <summary>Gets exact consumer allocations.</summary>
    public PowerAllocation Allocation { get; init; }

    /// <summary>Gets the sole active repair, when present.</summary>
    public SystemRepairState? ActiveRepair { get; init; }

    /// <summary>Derives available power using a floor over decimal-safe bounded arithmetic.</summary>
    public PowerUnits AvailablePower(ShipEngineeringDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        decimal available = decimal.Floor(definition.NominalGeneration.Value * (decimal)GenerationCondition.Value);
        return new PowerUnits(decimal.ToInt32(available));
    }

    /// <summary>Gets unallocated available power.</summary>
    public PowerUnits Reserve(ShipEngineeringDefinition definition)
    {
        PowerUnits available = AvailablePower(definition);
        int allocated = checked(Allocation.Sensors.Value + Allocation.ImpulsePropulsion.Value);
        return new PowerUnits(checked(available.Value - allocated));
    }

    /// <summary>Derives effective sensor capability on the inclusive unit interval.</summary>
    public double SensorCapability(ShipEngineeringDefinition definition) =>
        Capability(SensorCondition, Allocation.Sensors, definition.NominalSensorDemand);

    /// <summary>Derives effective impulse-propulsion capability on the inclusive unit interval.</summary>
    public double ImpulseCapability(ShipEngineeringDefinition definition) =>
        Capability(ImpulseCondition, Allocation.ImpulsePropulsion, definition.NominalImpulseDemand);

    /// <summary>Generates one deterministic allocation preset from current available power.</summary>
    public PowerAllocation AllocationFor(ShipEngineeringDefinition definition, PowerAllocationPreset preset)
    {
        ArgumentNullException.ThrowIfNull(definition);
        int available = AvailablePower(definition).Value;
        int sensors;
        int impulse;
        switch (preset)
        {
            case PowerAllocationPreset.Balanced:
                int totalDemand = checked(definition.NominalSensorDemand.Value + definition.NominalImpulseDemand.Value);
                sensors = Math.Min(
                    definition.NominalSensorDemand.Value,
                    checked((int)((long)available * definition.NominalSensorDemand.Value / totalDemand))
                );
                impulse = Math.Min(
                    definition.NominalImpulseDemand.Value,
                    checked((int)((long)available * definition.NominalImpulseDemand.Value / totalDemand))
                );
                // Whole-unit remainders are assigned in semantic system order so equal inputs are replay-stable.
                int remainder = checked(available - sensors - impulse);
                int sensorRemainder = Math.Min(remainder, definition.NominalSensorDemand.Value - sensors);
                sensors = checked(sensors + sensorRemainder);
                remainder -= sensorRemainder;
                impulse = checked(impulse + Math.Min(remainder, definition.NominalImpulseDemand.Value - impulse));
                break;
            case PowerAllocationPreset.PrioritizeSensors:
                sensors = Math.Min(available, definition.NominalSensorDemand.Value);
                impulse = Math.Min(available - sensors, definition.NominalImpulseDemand.Value);
                break;
            case PowerAllocationPreset.PrioritizePropulsion:
                impulse = Math.Min(available, definition.NominalImpulseDemand.Value);
                sensors = Math.Min(available - impulse, definition.NominalSensorDemand.Value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, "Power allocation preset is unknown.");
        }

        return new PowerAllocation(new PowerUnits(sensors), new PowerUnits(impulse));
    }

    internal void Validate(ShipEngineeringDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (
            Allocation.Sensors > definition.NominalSensorDemand
            || Allocation.ImpulsePropulsion > definition.NominalImpulseDemand
        )
        {
            throw new InvalidOperationException("Engineering allocation exceeds authored consumer demand.");
        }

        int allocated = checked(Allocation.Sensors.Value + Allocation.ImpulsePropulsion.Value);
        if (allocated > AvailablePower(definition).Value)
        {
            throw new InvalidOperationException("Engineering allocation exceeds currently available power.");
        }
    }

    internal SystemCondition ConditionFor(ShipSystemId systemId) =>
        systemId == ShipSystemId.Sensors ? SensorCondition
        : systemId == ShipSystemId.ImpulsePropulsion ? ImpulseCondition
        : systemId == ShipSystemId.PowerGeneration ? GenerationCondition
        : throw new ArgumentException("Ship system identity is invalid.", nameof(systemId));

    internal ShipEngineeringState WithCondition(ShipSystemId systemId, SystemCondition condition) =>
        systemId == ShipSystemId.Sensors ? this with { SensorCondition = condition }
        : systemId == ShipSystemId.ImpulsePropulsion ? this with { ImpulseCondition = condition }
        : systemId == ShipSystemId.PowerGeneration ? this with { GenerationCondition = condition }
        : throw new ArgumentException("Ship system identity is invalid.", nameof(systemId));

    private static double Capability(SystemCondition condition, PowerUnits allocated, PowerUnits demand) =>
        Math.Clamp(condition.Value * ((double)allocated.Value / demand.Value), 0, 1);
}
