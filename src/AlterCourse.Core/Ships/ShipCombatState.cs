using System.Collections.Immutable;
using AlterCourse.Core.Simulation;

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

    /// <summary>
    /// Creates the initial continuation for a loadout: every installed weapon ready at time zero, which is what the
    /// pre-substrate single readiness time defaulted to. A loadout without a weapon has no readiness entry.
    /// </summary>
    internal static ShipCombatState InitialFor(InstalledSystemCollection systems)
    {
        ArgumentNullException.ThrowIfNull(systems);
        return new ShipCombatState(
            systems
                .OfKind(ShipSystemKind.DirectedEnergyWeapons)
                .Select(system => new DirectedEnergyReadiness(system.Id, new SimulationTime(0)))
        );
    }

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
