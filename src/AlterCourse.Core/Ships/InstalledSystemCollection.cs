using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace AlterCourse.Core.Ships;

/// <summary>
/// The one owner of common per-installation state for a ship: an immutable, bounded set of installations.
/// </summary>
/// <remarks>
/// <para>
/// This is the common storage boundary, so several installations of one kind are representable here (ADR 0014).
/// Per-kind cardinality is a separate typed admission rule (<see cref="ShipSystemAdmission"/>) enforced by
/// bootstrap, content loading, and simulation validation — never by this type — so the common layer can hold,
/// compare, and snapshot shapes the current simulation refuses. Demand and supply are likewise checked by
/// <see cref="ShipEngineeringState"/>, not here.
/// </para>
/// <para>
/// Views are backed by <see cref="ImmutableArray{T}"/>; casting one to a mutable collection interface and
/// mutating it throws. Mutation happens only through the internal <c>With*</c> transitions, which return a new
/// collection. There is deliberately no kind-keyed dictionary and no first-match lookup: <see cref="OfKind"/>
/// returns every installation of a kind so a caller cannot silently collapse duplicates.
/// </para>
/// </remarks>
public sealed class InstalledSystemCollection
    : IReadOnlyCollection<InstalledSystem>,
        IEquatable<InstalledSystemCollection>
{
    /// <summary>Maximum installations one ship can store.</summary>
    public const int MaximumCount = ShipSystemLimits.MaximumInstalledSystemsPerShip;

    private readonly ImmutableArray<InstalledSystem> _byIdentity;
    private readonly ImmutableArray<InstalledSystem> _inCommonOrder;
    private readonly ImmutableArray<InstalledSystem> _consumers;

    private InstalledSystemCollection(ImmutableArray<InstalledSystem> byIdentity)
    {
        _byIdentity = byIdentity;
        _inCommonOrder =
        [
            .. byIdentity.OrderBy(system => system.Definition.CommonOrder).ThenBy(system => system.Id.Value),
        ];
        _consumers = [.. _inCommonOrder.Where(system => system.Definition.Power is not null)];
    }

    /// <summary>Gets a collection with no installations.</summary>
    public static InstalledSystemCollection Empty { get; } = new([]);

    /// <summary>Gets installations in ascending installed identity (snapshots and persistence order).</summary>
    public IReadOnlyList<InstalledSystem> ByIdentity => _byIdentity;

    /// <summary>
    /// Gets installations in canonical mechanics order: authored common order, then installed identity.
    /// </summary>
    public IReadOnlyList<InstalledSystem> InCommonOrder => _inCommonOrder;

    /// <summary>Gets allocatable consumers in canonical mechanics order.</summary>
    public IReadOnlyList<InstalledSystem> Consumers => _consumers;

    /// <summary>Gets the installation count.</summary>
    public int Count => _byIdentity.Length;

    /// <summary>Resolves one installation on this ship only.</summary>
    public bool TryGet(InstalledSystemId id, [MaybeNullWhen(false)] out InstalledSystem system)
    {
        foreach (InstalledSystem candidate in _byIdentity)
        {
            if (candidate.Id == id)
            {
                system = candidate;
                return true;
            }
        }

        system = null;
        return false;
    }

    /// <summary>Resolves one installation that must exist.</summary>
    public InstalledSystem GetRequired(InstalledSystemId id) =>
        TryGet(id, out InstalledSystem? system)
            ? system
            : throw new KeyNotFoundException($"No installed system exists with identity {id.Value}.");

    /// <summary>Gets every installation of one kind in canonical order; zero, one, or several.</summary>
    public IReadOnlyList<InstalledSystem> OfKind(ShipSystemKind kind) =>
        _inCommonOrder.Where(system => system.Kind == kind).ToImmutableArray();

    /// <summary>Enumerates installations in ascending installed identity.</summary>
    public IEnumerator<InstalledSystem> GetEnumerator() => ((IEnumerable<InstalledSystem>)_byIdentity).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public bool Equals(InstalledSystemCollection? other) =>
        other is not null && _byIdentity.AsSpan().SequenceEqual(other._byIdentity.AsSpan());

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as InstalledSystemCollection);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (InstalledSystem system in _byIdentity)
        {
            hash.Add(system);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Validates storage shape and creates a collection; the only way to build one from arbitrary input.
    /// </summary>
    internal static InstalledSystemCollection Create(IEnumerable<InstalledSystem> systems)
    {
        ArgumentNullException.ThrowIfNull(systems);

        // Bound before materializing so a hostile or unbounded sequence cannot allocate past the limit.
        InstalledSystem[] materialized = systems.Take(MaximumCount + 1).ToArray();
        if (materialized.Length > MaximumCount)
        {
            throw new ArgumentException($"A ship supports at most {MaximumCount} installed systems.", nameof(systems));
        }

        foreach (InstalledSystem system in materialized)
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

        return new InstalledSystemCollection([.. materialized]);
    }

    internal InstalledSystemCollection WithCondition(InstalledSystemId id, SystemCondition condition) =>
        Replace(GetRequired(id).WithCondition(condition));

    /// <summary>Replaces every consumer allocation; the value's key set must equal this ship's consumers.</summary>
    internal InstalledSystemCollection WithAllocation(PowerAllocation exact)
    {
        ArgumentNullException.ThrowIfNull(exact);
        if (exact.Entries.Count != _consumers.Length)
        {
            throw new ArgumentException("Allocation must list every installed consumer exactly once.", nameof(exact));
        }

        InstalledSystem[] replaced = _byIdentity.ToArray();
        for (int index = 0; index < replaced.Length; index++)
        {
            InstalledSystem system = replaced[index];
            if (system.Definition.Power is null)
            {
                continue;
            }

            if (!exact.TryGet(system.Id, out Quantities.PowerUnits allocation))
            {
                throw new ArgumentException($"Allocation omits installed consumer {system.Id.Value}.", nameof(exact));
            }

            replaced[index] = system.WithAllocation(allocation);
        }

        return new InstalledSystemCollection([.. replaced]);
    }

    private InstalledSystemCollection Replace(InstalledSystem replacement)
    {
        ImmutableArray<InstalledSystem>.Builder builder = ImmutableArray.CreateBuilder<InstalledSystem>(
            _byIdentity.Length
        );
        foreach (InstalledSystem system in _byIdentity)
        {
            builder.Add(system.Id == replacement.Id ? replacement : system);
        }

        return new InstalledSystemCollection(builder.MoveToImmutable());
    }
}
