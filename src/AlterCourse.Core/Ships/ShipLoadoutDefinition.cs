namespace AlterCourse.Core.Ships;

/// <summary>
/// A ship design's default initial loadout: explicit installations plus the explicit allocator continuation a
/// ship bootstrapped from this loadout starts with.
/// </summary>
/// <remarks>
/// <para>
/// This is only the design default. A live ship owns its actual installations; loading a save never reads, overlays,
/// or replenishes from this value.
/// </para>
/// <para>
/// Identities are authored, never derived from array position, so the constructor canonicalizes to ascending
/// identity order: two loadouts listing the same installations in different orders are equal. Gaps are legal.
/// References to definitions are not resolved here; content readers resolve them against a catalog before
/// constructing a loadout.
/// </para>
/// </remarks>
public sealed record ShipLoadoutDefinition
{
    private readonly InitialInstalledSystem[] _systems;

    /// <summary>Initializes and canonicalizes a loadout.</summary>
    /// <exception cref="ArgumentException">
    /// The loadout exceeds <see cref="ShipSystemLimits.MaximumInstalledSystemsPerShip"/>, repeats an installed
    /// identity, or its continuation does not exceed every listed identity.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nextInstalledSystemId"/> is not positive.</exception>
    public ShipLoadoutDefinition(long nextInstalledSystemId, IEnumerable<InitialInstalledSystem> systems)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextInstalledSystemId);
        ArgumentNullException.ThrowIfNull(systems);

        // Bound the enumeration before materializing so an unbounded or hostile sequence cannot allocate past
        // the limit.
        InitialInstalledSystem[] materialized = systems
            .Take(ShipSystemLimits.MaximumInstalledSystemsPerShip + 1)
            .ToArray();
        if (materialized.Length > ShipSystemLimits.MaximumInstalledSystemsPerShip)
        {
            throw new ArgumentException(
                $"A loadout supports at most {ShipSystemLimits.MaximumInstalledSystemsPerShip} installed systems.",
                nameof(systems)
            );
        }

        foreach (InitialInstalledSystem system in materialized)
        {
            ArgumentNullException.ThrowIfNull(system, nameof(systems));
        }

        Array.Sort(materialized, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        for (int index = 1; index < materialized.Length; index++)
        {
            if (materialized[index].Id == materialized[index - 1].Id)
            {
                throw new ArgumentException(
                    $"Installed system identity {materialized[index].Id.Value} appears more than once.",
                    nameof(systems)
                );
            }
        }

        // The continuation must exceed every retained identity so a later allocation cannot reissue one. It is
        // validated, never recomputed: max + 1 would erase the history of removed installations.
        if (materialized.Length > 0 && nextInstalledSystemId <= materialized[^1].Id.Value)
        {
            throw new ArgumentException(
                $"Next installed system identity {nextInstalledSystemId} must exceed the largest listed identity "
                    + $"{materialized[^1].Id.Value}.",
                nameof(nextInstalledSystemId)
            );
        }

        NextInstalledSystemId = nextInstalledSystemId;
        _systems = materialized;
        Systems = Array.AsReadOnly(materialized);
    }

    /// <summary>Gets the allocator continuation a bootstrapped ship starts with.</summary>
    public long NextInstalledSystemId { get; }

    /// <summary>Gets the installations in ascending installed-identity order.</summary>
    public IReadOnlyList<InitialInstalledSystem> Systems { get; }

    /// <inheritdoc />
    public bool Equals(ShipLoadoutDefinition? other) =>
        other is not null
        && NextInstalledSystemId == other.NextInstalledSystemId
        && _systems.AsSpan().SequenceEqual(other._systems);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(NextInstalledSystemId);
        foreach (InitialInstalledSystem system in _systems)
        {
            hash.Add(system);
        }

        return hash.ToHashCode();
    }
}
