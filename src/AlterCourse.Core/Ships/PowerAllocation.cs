using System.Collections.Immutable;
using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Ships;

/// <summary>
/// A complete exact allocation value keyed by installed consumer identity.
/// </summary>
/// <remarks>
/// <para>
/// This is a value, not storage: committed allocations live on each <see cref="InstalledSystem"/>, and a ship's
/// current allocation is derived from them. Commands submit a whole value that replaces every consumer allocation
/// atomically; it is never a sparse patch.
/// </para>
/// <para>
/// The constructor enforces only shape (bounded count, no duplicate identity, canonical ascending order). Whether
/// the key set matches a particular ship's consumers, and whether values fit demand and supply, depends on that
/// ship and is decided by the engineering command or invariant validation.
/// </para>
/// </remarks>
public sealed class PowerAllocation : IEquatable<PowerAllocation>
{
    private readonly ImmutableArray<PowerAllocationEntry> _entries;

    /// <summary>Initializes a complete allocation from entries in any order.</summary>
    public PowerAllocation(IEnumerable<PowerAllocationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        PowerAllocationEntry[] materialized = entries
            .Take(ShipSystemLimits.MaximumInstalledSystemsPerShip + 1)
            .ToArray();
        if (materialized.Length > ShipSystemLimits.MaximumInstalledSystemsPerShip)
        {
            throw new ArgumentException(
                $"An allocation supports at most {ShipSystemLimits.MaximumInstalledSystemsPerShip} consumers.",
                nameof(entries)
            );
        }

        foreach (PowerAllocationEntry entry in materialized)
        {
            if (entry.Consumer.Value == 0)
            {
                throw new ArgumentException(
                    "Allocation entries require initialized consumer identities.",
                    nameof(entries)
                );
            }
        }

        Array.Sort(materialized, static (left, right) => left.Consumer.Value.CompareTo(right.Consumer.Value));
        for (int index = 1; index < materialized.Length; index++)
        {
            if (materialized[index].Consumer == materialized[index - 1].Consumer)
            {
                throw new ArgumentException(
                    $"Consumer {materialized[index].Consumer.Value} appears more than once in an allocation.",
                    nameof(entries)
                );
            }
        }

        _entries = [.. materialized];
    }

    /// <summary>Gets the allocation with no consumers.</summary>
    public static PowerAllocation Empty { get; } = new([]);

    /// <summary>Gets entries in ascending consumer identity.</summary>
    public IReadOnlyList<PowerAllocationEntry> Entries => _entries;

    /// <summary>Gets the exact total; at most 16 × 1,000,000, so a checked 64-bit sum cannot overflow.</summary>
    public long Total
    {
        get
        {
            long total = 0;
            foreach (PowerAllocationEntry entry in _entries)
            {
                total = checked(total + entry.Allocation.Value);
            }

            return total;
        }
    }

    /// <summary>Gets one consumer's allocation when the value lists it.</summary>
    public bool TryGet(InstalledSystemId consumer, out PowerUnits allocation)
    {
        foreach (PowerAllocationEntry entry in _entries)
        {
            if (entry.Consumer == consumer)
            {
                allocation = entry.Allocation;
                return true;
            }
        }

        allocation = default;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(PowerAllocation? other) =>
        other is not null && _entries.AsSpan().SequenceEqual(other._entries.AsSpan());

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PowerAllocation);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (PowerAllocationEntry entry in _entries)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }

    /// <summary>Compares allocation values.</summary>
    public static bool operator ==(PowerAllocation? left, PowerAllocation? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Compares allocation values.</summary>
    public static bool operator !=(PowerAllocation? left, PowerAllocation? right) => !(left == right);
}
