using System.Collections.Immutable;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Gameplay;

/// <summary>
/// A ship start's installed-system declaration: either state for the design's default loadout, or an explicit
/// loadout that bootstrap uses verbatim.
/// </summary>
/// <remarks>
/// <para>
/// Omitted versus explicit is a typed choice, not an empty-list convention, so an explicitly empty loadout
/// (<c>Explicit(1, [])</c>) can never be mistaken for "use the defaults". A design-default start must state the
/// condition and allocation of exactly the default installations; nothing is implied.
/// </para>
/// <para>
/// Only shape is checked here (bounds, duplicate identities). Definition resolution, allocation presence,
/// allocator continuation, cardinality, demand, and supply are validated by bootstrap against the catalog.
/// </para>
/// </remarks>
public sealed record ShipSystemsStart
{
    private ShipSystemsStart(
        ShipLoadoutSource source,
        long nextInstalledSystemId,
        ImmutableArray<InstalledSystemStateStart> defaultStates,
        ImmutableArray<InstalledSystemStart> installations
    )
    {
        Source = source;
        NextInstalledSystemId = nextInstalledSystemId;
        DefaultStates = defaultStates;
        Installations = installations;
    }

    /// <summary>Gets whether installations come from the design default or from this start.</summary>
    public ShipLoadoutSource Source { get; }

    /// <summary>Gets the explicit continuation; zero for a design-default start.</summary>
    public long NextInstalledSystemId { get; }

    /// <summary>Gets per-installation state for a design-default start, ascending by identity.</summary>
    public IReadOnlyList<InstalledSystemStateStart> DefaultStates { get; }

    /// <summary>Gets explicit installations, ascending by identity.</summary>
    public IReadOnlyList<InstalledSystemStart> Installations { get; }

    /// <summary>Uses the design's default loadout with exactly these per-installation states.</summary>
    public static ShipSystemsStart FromDesignDefaults(IEnumerable<InstalledSystemStateStart> states)
    {
        InstalledSystemStateStart[] materialized = Bounded(states, nameof(states));
        Array.Sort(materialized, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        EnsureDistinct(materialized.Select(state => state.Id), nameof(states));
        return new ShipSystemsStart(ShipLoadoutSource.DesignDefault, 0, [.. materialized], []);
    }

    /// <summary>Declares the ship's actual installations and continuation; the design default is ignored.</summary>
    public static ShipSystemsStart Explicit(long nextInstalledSystemId, IEnumerable<InstalledSystemStart> installations)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextInstalledSystemId);
        InstalledSystemStart[] materialized = Bounded(installations, nameof(installations));
        foreach (InstalledSystemStart installation in materialized)
        {
            ArgumentNullException.ThrowIfNull(installation, nameof(installations));
        }

        Array.Sort(materialized, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        EnsureDistinct(materialized.Select(installation => installation.Id), nameof(installations));
        return new ShipSystemsStart(ShipLoadoutSource.Explicit, nextInstalledSystemId, [], [.. materialized]);
    }

    private static T[] Bounded<T>(IEnumerable<T> items, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(items, parameterName);
        T[] materialized = items.Take(ShipSystemLimits.MaximumInstalledSystemsPerShip + 1).ToArray();
        if (materialized.Length > ShipSystemLimits.MaximumInstalledSystemsPerShip)
        {
            throw new ArgumentException(
                $"A ship start supports at most {ShipSystemLimits.MaximumInstalledSystemsPerShip} installed systems.",
                parameterName
            );
        }

        return materialized;
    }

    private static void EnsureDistinct(IEnumerable<InstalledSystemId> ids, string parameterName)
    {
        InstalledSystemId? previous = null;
        foreach (InstalledSystemId id in ids)
        {
            if (id.Value == 0)
            {
                throw new ArgumentException("Installed system starts require initialized identities.", parameterName);
            }

            if (previous == id)
            {
                throw new ArgumentException($"Installed system {id.Value} is declared more than once.", parameterName);
            }

            previous = id;
        }
    }

    /// <inheritdoc />
    public bool Equals(ShipSystemsStart? other) =>
        other is not null
        && Source == other.Source
        && NextInstalledSystemId == other.NextInstalledSystemId
        && DefaultStates.SequenceEqual(other.DefaultStates)
        && Installations.SequenceEqual(other.Installations);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(Source, NextInstalledSystemId, DefaultStates.Count, Installations.Count);
}
