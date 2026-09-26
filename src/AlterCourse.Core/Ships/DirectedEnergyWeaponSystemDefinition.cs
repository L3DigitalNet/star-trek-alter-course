namespace AlterCourse.Core.Ships;

/// <summary>Defines a directed-energy weapon component: a power consumer with weapon tuning.</summary>
public sealed record DirectedEnergyWeaponSystemDefinition : SystemDefinition
{
    /// <summary>Initializes a directed-energy weapon definition.</summary>
    public DirectedEnergyWeaponSystemDefinition(
        SystemDefinitionId id,
        string componentLabel,
        int commonOrder,
        bool conditionParticipation,
        SystemRepairCapability? repair,
        SystemPowerDemand power,
        DirectedEnergyWeaponDefinition weapon
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
        ArgumentNullException.ThrowIfNull(weapon);
        Weapon = weapon;
    }

    /// <inheritdoc />
    public override ShipSystemId Kind => ShipSystemId.DirectedEnergyWeapons;

    /// <summary>Gets the range, base damage, and cooldown tuning.</summary>
    public DirectedEnergyWeaponDefinition Weapon { get; }
}
