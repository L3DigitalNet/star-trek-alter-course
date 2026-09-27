using System.Diagnostics.CodeAnalysis;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Content;

/// <summary>
/// An immutable set of validated system definitions with unique identities, plus the aim-kind vocabulary derived
/// from them.
/// </summary>
/// <remarks>
/// Every exposed order is a function of definition data alone (<see cref="SystemDefinition.CommonOrder"/>, then
/// ordinal identity), never of source-document or dictionary order, so reordering authored input cannot change
/// behavior.
/// </remarks>
public sealed class SystemDefinitionCatalog
{
    private readonly Dictionary<SystemDefinitionId, SystemDefinition> _byId;

    /// <summary>Builds a catalog from definitions whose identities the caller has already proven unique.</summary>
    /// <exception cref="ArgumentException">An identity repeats; loaders report this as a diagnostic first.</exception>
    internal SystemDefinitionCatalog(IEnumerable<SystemDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _byId = [];
        foreach (SystemDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition, nameof(definitions));
            if (!_byId.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException(
                    $"System definition identity '{definition.Id.Value}' appears more than once.",
                    nameof(definitions)
                );
            }
        }

        SystemDefinition[] ordered = _byId
            .Values.OrderBy(definition => definition.CommonOrder)
            .ThenBy(definition => definition.Id.Value, StringComparer.Ordinal)
            .ToArray();
        Definitions = Array.AsReadOnly(ordered);

        // Aim kinds are derived from public content only (never from any ship's installations), ordered by the
        // earliest participating definition of each kind. With production content this reproduces the fixed
        // five-kind combat list: generation, sensors, impulse, shields, directed-energy weapons.
        ShipSystemKind[] damageTargetKinds = ordered
            .Where(definition => definition.ConditionParticipation)
            .GroupBy(definition => definition.Kind)
            .Select(group => (Kind: group.Key, Order: group.Min(definition => definition.CommonOrder)))
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Kind.Value, StringComparer.Ordinal)
            .Select(entry => entry.Kind)
            .ToArray();
        DamageTargetKinds = Array.AsReadOnly(damageTargetKinds);
    }

    /// <summary>Gets every definition ordered by common order, then ordinal identity.</summary>
    public IReadOnlyList<SystemDefinition> Definitions { get; }

    /// <summary>
    /// Gets the distinct kinds of condition-participating definitions, ordered by each kind's lowest common order
    /// and then ordinal kind value. This is the admitted aim vocabulary for remote fire.
    /// </summary>
    public IReadOnlyList<ShipSystemKind> DamageTargetKinds { get; }

    /// <summary>Gets the definition with the required identity.</summary>
    /// <exception cref="KeyNotFoundException">No definition has the identity.</exception>
    public SystemDefinition GetRequired(SystemDefinitionId id) =>
        _byId.TryGetValue(id, out SystemDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"No system definition exists with identity '{id.Value}'.");

    /// <summary>Attempts to find the definition with the identity.</summary>
    public bool TryGet(SystemDefinitionId id, [MaybeNullWhen(false)] out SystemDefinition definition) =>
        _byId.TryGetValue(id, out definition);
}
