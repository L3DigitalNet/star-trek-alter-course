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

    /// <summary>Initializes all five conditions with exact allocation and optional repair.</summary>
    public ShipEngineeringState(
        SystemCondition generationCondition,
        SystemCondition sensorCondition,
        SystemCondition impulseCondition,
        SystemCondition shieldCondition,
        SystemCondition directedEnergyCondition,
        PowerAllocation allocation,
        SystemRepairState? activeRepair = null
    )
        : this(generationCondition, sensorCondition, impulseCondition, allocation, activeRepair)
    {
        ShieldCondition = shieldCondition;
        DirectedEnergyCondition = directedEnergyCondition;
    }

    /// <summary>Gets shield condition.</summary>
    public SystemCondition ShieldCondition { get; init; }

    /// <summary>Gets directed-energy weapon condition.</summary>
    public SystemCondition DirectedEnergyCondition { get; init; }

    /// <summary>Gets shield demand satisfaction; absent legacy capacity remains unpowered.</summary>
    public double ShieldPowerSatisfaction(ShipEngineeringDefinition definition) =>
        PowerSatisfaction(Allocation.Shields, definition.NominalShieldDemand);

    /// <summary>Derives effective shield capability.</summary>
    public double ShieldCapability(ShipEngineeringDefinition definition) =>
        Capability(ShieldCondition, Allocation.Shields, definition.NominalShieldDemand);

    /// <summary>Derives effective directed-energy weapon capability.</summary>
    public double DirectedEnergyCapability(ShipEngineeringDefinition definition) =>
        Capability(DirectedEnergyCondition, Allocation.DirectedEnergyWeapons, definition.NominalDirectedEnergyDemand);

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
        int allocated = Allocation.Total;
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
        int[] demands =
        [
            definition.NominalSensorDemand.Value,
            definition.NominalImpulseDemand.Value,
            definition.NominalShieldDemand.Value,
            definition.NominalDirectedEnergyDemand.Value,
        ];
        int[] shares = new int[4];
        if (preset == PowerAllocationPreset.Balanced)
        {
            int totalDemand = checked(demands.Sum());
            int budget = Math.Min(available, totalDemand);
            for (int i = 0; i < shares.Length; i++)
            {
                shares[i] = checked((int)((long)budget * demands[i] / totalDemand));
            }
            DistributeRemainder(shares, demands, budget - shares.Sum());
        }
        else
        {
            int priority = preset switch
            {
                PowerAllocationPreset.PrioritizeSensors => 0,
                PowerAllocationPreset.PrioritizePropulsion => 1,
                PowerAllocationPreset.PrioritizeShields => 2,
                PowerAllocationPreset.PrioritizeDirectedEnergyWeapons => 3,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(preset),
                    preset,
                    "Power allocation preset is unknown."
                ),
            };
            shares[priority] = Math.Min(available, demands[priority]);
            available -= shares[priority];
            for (int i = 0; i < shares.Length; i++)
            {
                if (i == priority)
                    continue;
                shares[i] = Math.Min(available, demands[i]);
                available -= shares[i];
            }
        }
        return AllocationFrom(shares);
    }

    /// <summary>Reconciles forced generation loss without increasing any previous consumer share.</summary>
    public PowerAllocation ReconcileAvailablePower(ShipEngineeringDefinition definition)
    {
        int available = AvailablePower(definition).Value;
        int total = Allocation.Total;
        if (total <= available)
            return Allocation;
        int[] oldShares =
        [
            Allocation.Sensors.Value,
            Allocation.ImpulsePropulsion.Value,
            Allocation.Shields.Value,
            Allocation.DirectedEnergyWeapons.Value,
        ];
        int[] shares = oldShares.Select(value => checked((int)((long)value * available / total))).ToArray();
        DistributeRemainder(shares, oldShares, available - shares.Sum());
        return AllocationFrom(shares);
    }

    private static PowerAllocation AllocationFrom(int[] shares) =>
        new(new PowerUnits(shares[0]), new PowerUnits(shares[1]), new PowerUnits(shares[2]), new PowerUnits(shares[3]));

    private static void DistributeRemainder(int[] shares, int[] limits, int remainder)
    {
        // Proportional flooring leaves fewer than four units; each eligible consumer receives at most one.
        for (int i = 0; i < shares.Length && remainder > 0; i++)
        {
            if (shares[i] < limits[i])
            {
                shares[i]++;
                remainder--;
            }
        }
    }

    internal void Validate(ShipEngineeringDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (
            Allocation.Sensors > definition.NominalSensorDemand
            || Allocation.ImpulsePropulsion > definition.NominalImpulseDemand
            || Allocation.Shields > definition.NominalShieldDemand
            || Allocation.DirectedEnergyWeapons > definition.NominalDirectedEnergyDemand
        )
        {
            throw new InvalidOperationException("Engineering allocation exceeds authored consumer demand.");
        }

        int allocated = Allocation.Total;
        if (allocated > AvailablePower(definition).Value)
        {
            throw new InvalidOperationException("Engineering allocation exceeds currently available power.");
        }
    }

    internal SystemCondition ConditionFor(ShipSystemId systemId) =>
        systemId == ShipSystemId.Sensors ? SensorCondition
        : systemId == ShipSystemId.ImpulsePropulsion ? ImpulseCondition
        : systemId == ShipSystemId.Shields ? ShieldCondition
        : systemId == ShipSystemId.DirectedEnergyWeapons ? DirectedEnergyCondition
        : systemId == ShipSystemId.PowerGeneration ? GenerationCondition
        : throw new ArgumentException("Ship system identity is invalid.", nameof(systemId));

    internal ShipEngineeringState WithCondition(ShipSystemId systemId, SystemCondition condition) =>
        systemId == ShipSystemId.Sensors ? this with { SensorCondition = condition }
        : systemId == ShipSystemId.ImpulsePropulsion ? this with { ImpulseCondition = condition }
        : systemId == ShipSystemId.Shields ? this with { ShieldCondition = condition }
        : systemId == ShipSystemId.DirectedEnergyWeapons ? this with { DirectedEnergyCondition = condition }
        : systemId == ShipSystemId.PowerGeneration ? this with { GenerationCondition = condition }
        : throw new ArgumentException("Ship system identity is invalid.", nameof(systemId));

    private static double Capability(SystemCondition condition, PowerUnits allocated, PowerUnits demand) =>
        condition.Value * PowerSatisfaction(allocated, demand);

    private static double PowerSatisfaction(PowerUnits allocated, PowerUnits demand) =>
        demand.Value == 0 ? 0 : Math.Min(1, (double)allocated.Value / demand.Value);
}
