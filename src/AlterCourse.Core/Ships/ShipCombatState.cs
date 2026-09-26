using System.Collections.Immutable;

namespace AlterCourse.Core.Ships;

/// <summary>Ship-owned combat continuation: per-installed-weapon readiness and the pending defensive stimulus.</summary>
/// <remarks>
/// Readiness is keyed by installed weapon identity and, in valid state, lists exactly the ship's installed
/// directed-energy weapons in ascending identity (<c>SimulationState</c> enforces the key set). A ship without a
/// weapon has no entries; there is no placeholder readiness for an absent weapon.
/// </remarks>
internal sealed record ShipCombatState
{
    internal ShipCombatState(
        IEnumerable<DirectedEnergyReadiness> weaponReadiness,
        CombatStimulus? pendingStimulus = null
    )
    {
        ArgumentNullException.ThrowIfNull(weaponReadiness);
        DirectedEnergyReadiness[] materialized = weaponReadiness
            .Take(ShipSystemLimits.MaximumInstalledSystemsPerShip + 1)
            .ToArray();
        if (materialized.Length > ShipSystemLimits.MaximumInstalledSystemsPerShip)
        {
            throw new ArgumentException(
                "Weapon readiness exceeds the installed-system limit.",
                nameof(weaponReadiness)
            );
        }

        foreach (DirectedEnergyReadiness entry in materialized)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(weaponReadiness));
        }

        Array.Sort(materialized, static (left, right) => left.Weapon.Value.CompareTo(right.Weapon.Value));
        for (int index = 1; index < materialized.Length; index++)
        {
            if (materialized[index].Weapon == materialized[index - 1].Weapon)
            {
                throw new ArgumentException("Weapon readiness lists an installation twice.", nameof(weaponReadiness));
            }
        }

        WeaponReadiness = [.. materialized];
        PendingStimulus = pendingStimulus;
    }

    internal static ShipCombatState Empty { get; } = new([]);

    internal ImmutableArray<DirectedEnergyReadiness> WeaponReadiness { get; init; }

    internal CombatStimulus? PendingStimulus { get; init; }

    internal DirectedEnergyReadiness? ReadinessOf(InstalledSystemId weapon)
    {
        foreach (DirectedEnergyReadiness entry in WeaponReadiness)
        {
            if (entry.Weapon == weapon)
            {
                return entry;
            }
        }

        return null;
    }

    internal ShipCombatState WithReadiness(DirectedEnergyReadiness readiness) =>
        new(WeaponReadiness.Where(entry => entry.Weapon != readiness.Weapon).Append(readiness), PendingStimulus);

    public bool Equals(ShipCombatState? other) =>
        other is not null
        && WeaponReadiness.AsSpan().SequenceEqual(other.WeaponReadiness.AsSpan())
        && Equals(PendingStimulus, other.PendingStimulus);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (DirectedEnergyReadiness entry in WeaponReadiness)
        {
            hash.Add(entry);
        }

        hash.Add(PendingStimulus);
        return hash.ToHashCode();
    }
}
