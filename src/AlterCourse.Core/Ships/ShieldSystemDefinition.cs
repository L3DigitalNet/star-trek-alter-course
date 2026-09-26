namespace AlterCourse.Core.Ships;

/// <summary>Defines a shield component: a power consumer with no specialized tuning in the current simulation.</summary>
public sealed record ShieldSystemDefinition : SystemDefinition
{
    /// <summary>Initializes a shield definition.</summary>
    public ShieldSystemDefinition(
        SystemDefinitionId id,
        string componentLabel,
        int commonOrder,
        bool conditionParticipation,
        SystemRepairCapability? repair,
        SystemPowerDemand power
    )
        : base(
            id,
            componentLabel,
            commonOrder,
            conditionParticipation,
            repair,
            power ?? throw new ArgumentNullException(nameof(power))
        ) { }

    /// <inheritdoc />
    public override ShipSystemId Kind => ShipSystemId.Shields;
}
