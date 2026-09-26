using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Ships;

/// <summary>
/// One live installation on one ship: the sole owner of that installation's common runtime values.
/// </summary>
/// <remarks>
/// Kind is derived from the resolved definition and never stored separately, so an installation cannot claim a
/// kind its definition contradicts. Allocation is present exactly when the definition is an allocatable consumer:
/// a consumer at zero power carries <c>0</c>, a nonconsumer carries <see langword="null"/>, and the difference is
/// structural rather than a flag that could disagree with the definition.
/// </remarks>
public sealed record InstalledSystem
{
    internal InstalledSystem(
        InstalledSystemId id,
        SystemDefinition definition,
        SystemCondition condition,
        PowerUnits? allocation
    )
    {
        if (id.Value == 0)
        {
            throw new ArgumentException("Installed system requires an initialized identity.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(definition);
        if ((definition.Power is null) != (allocation is null))
        {
            throw new ArgumentException(
                $"Installed system {id.Value} must carry an allocation exactly when its definition is a power consumer.",
                nameof(allocation)
            );
        }

        Id = id;
        Definition = definition;
        Condition = condition;
        Allocation = allocation;
    }

    /// <summary>Gets the ship-local installed identity.</summary>
    public InstalledSystemId Id { get; }

    /// <summary>Gets the resolved reusable definition this installation instantiates.</summary>
    public SystemDefinition Definition { get; }

    /// <summary>Gets the kind derived from the definition.</summary>
    public ShipSystemKind Kind => Definition.Kind;

    /// <summary>Gets current condition.</summary>
    public SystemCondition Condition { get; }

    /// <summary>Gets the committed allocation; <see langword="null"/> exactly for nonconsumers.</summary>
    public PowerUnits? Allocation { get; }

    internal InstalledSystem WithCondition(SystemCondition condition) => new(Id, Definition, condition, Allocation);

    internal InstalledSystem WithAllocation(PowerUnits allocation) => new(Id, Definition, Condition, allocation);
}
