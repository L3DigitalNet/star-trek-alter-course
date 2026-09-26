using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Ships;

/// <summary>Defines an impulse-propulsion component: a power consumer contributing tactical speed.</summary>
public sealed record ImpulsePropulsionSystemDefinition : SystemDefinition
{
    /// <summary>Initializes an impulse-propulsion definition.</summary>
    public ImpulsePropulsionSystemDefinition(
        SystemDefinitionId id,
        string componentLabel,
        int commonOrder,
        bool conditionParticipation,
        SystemRepairCapability? repair,
        SystemPowerDemand power,
        SpeedKilometersPerSecond maximumTacticalSpeed
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
        MaximumTacticalSpeed = maximumTacticalSpeed;
    }

    /// <inheritdoc />
    public override ShipSystemId Kind => ShipSystemId.ImpulsePropulsion;

    /// <summary>Gets the attainable tactical speed at full capability.</summary>
    public SpeedKilometersPerSecond MaximumTacticalSpeed { get; }
}
