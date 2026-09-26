namespace AlterCourse.Core.Ships;

/// <summary>
/// Describes one reusable, validated system definition: the common data every installed system of this design
/// shares, with kind-specific tuning carried by the sealed subclasses.
/// </summary>
/// <remarks>
/// <para>
/// The hierarchy is closed (five sealed subclasses, one per kind) and carries data only. Specialized tuning lives
/// in typed subclass members rather than nullable per-kind fields on one record or an untyped payload, so a
/// definition cannot hold another kind's data.
/// </para>
/// <para>
/// There are no support flags: a definition is repairable exactly when <see cref="Repair"/> is present and is an
/// allocatable power consumer exactly when <see cref="Power"/> is present, so no flag can contradict the data.
/// Each subclass constructor fixes which of the two capabilities its kind may carry.
/// </para>
/// </remarks>
public abstract record SystemDefinition
{
    /// <summary>Gets the maximum presentation-label length accepted by content.</summary>
    public const int MaximumComponentLabelLength = 64;

    /// <summary>Gets the maximum explicit ordering value accepted by content.</summary>
    public const int MaximumCommonOrder = 1_000_000;

    /// <summary>Initializes and validates the common definition data.</summary>
    /// <exception cref="ArgumentException">A common value is uninitialized, out of range, or not admitted.</exception>
    private protected SystemDefinition(
        SystemDefinitionId id,
        string componentLabel,
        int commonOrder,
        bool conditionParticipation,
        SystemRepairCapability? repair,
        SystemPowerDemand? power
    )
    {
        if (id.Value is null)
        {
            throw new ArgumentException("System definition requires an initialized identity.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(componentLabel);
        if (componentLabel.Length > MaximumComponentLabelLength)
        {
            throw new ArgumentException(
                $"Component label cannot exceed {MaximumComponentLabelLength} characters.",
                nameof(componentLabel)
            );
        }

        ArgumentOutOfRangeException.ThrowIfNegative(commonOrder);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(commonOrder, MaximumCommonOrder);

        // Every current capability formula multiplies by condition and every current kind is a damage target, so
        // system-definition V1 admits only participating definitions and runtime condition stays non-nullable.
        // A kind without condition would need a new content version and a nullable runtime field.
        if (!conditionParticipation)
        {
            throw new ArgumentException(
                "Every current system kind participates in condition and damage.",
                nameof(conditionParticipation)
            );
        }

        Id = id;
        ComponentLabel = componentLabel;
        CommonOrder = commonOrder;
        ConditionParticipation = conditionParticipation;
        Repair = repair;
        Power = power;
    }

    /// <summary>Gets the stable reusable definition identity.</summary>
    public SystemDefinitionId Id { get; }

    /// <summary>Gets the behavior kind; derived from the subclass, never authored independently of it.</summary>
    public abstract ShipSystemId Kind { get; }

    /// <summary>Gets the presentation-only label. It is excluded from save compatibility.</summary>
    public string ComponentLabel { get; }

    /// <summary>
    /// Gets the explicit authored ordering used wherever systems are processed or presented in common order
    /// (ties broken by installed identity). Lower values come first.
    /// </summary>
    public int CommonOrder { get; }

    /// <summary>Gets whether installations track condition and can be damage targets.</summary>
    public bool ConditionParticipation { get; }

    /// <summary>Gets the repair capability, or <see langword="null"/> when installations are not repairable.</summary>
    public SystemRepairCapability? Repair { get; }

    /// <summary>Gets the allocatable demand, or <see langword="null"/> when installations are not power consumers.</summary>
    public SystemPowerDemand? Power { get; }
}
