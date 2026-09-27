using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Ships;

/// <summary>
/// Owns a ship's live installed systems, their identity continuation, and its single active repair.
/// </summary>
/// <remarks>
/// <para>
/// ONE SOURCE OF TRUTH: condition and allocation are stored only on each <see cref="InstalledSystem"/>.
/// <see cref="Allocation"/> is a derived read-only value; nothing writes through it. There is no named per-kind
/// field and no parallel keyed map to keep synchronized.
/// </para>
/// <para>
/// Power algorithms walk consumers in canonical order (authored common order, then installed identity), never
/// array or dictionary order, so renumbering installations with the same common order yields identical vectors.
/// Power arithmetic uses checked 64-bit intermediates: a consumer demand and the available supply are each at most
/// 1,000,000 and there are at most 16 consumers, so every product stays below 10^13.
/// </para>
/// </remarks>
public sealed record ShipEngineeringState
{
    internal ShipEngineeringState(
        InstalledSystemCollection systems,
        InstalledSystemIdAllocator installationIds,
        SystemRepairState? activeRepair = null
    )
    {
        ArgumentNullException.ThrowIfNull(systems);
        ArgumentNullException.ThrowIfNull(installationIds);
        Systems = systems;
        InstallationIds = installationIds;
        ActiveRepair = activeRepair;
    }

    /// <summary>Gets the live installations.</summary>
    public InstalledSystemCollection Systems { get; init; }

    /// <summary>Gets the ship-local installed identity continuation; restored, never recomputed.</summary>
    public InstalledSystemIdAllocator InstallationIds { get; init; }

    /// <summary>Gets the sole active repair, when present.</summary>
    public SystemRepairState? ActiveRepair { get; init; }

    /// <summary>Gets the current exact allocation derived from consumer installations.</summary>
    public PowerAllocation Allocation =>
        new(Systems.Consumers.Select(consumer => new PowerAllocationEntry(consumer.Id, consumer.Allocation!.Value)));

    /// <summary>Gets the nominal output of the sole generator, or zero when none is installed.</summary>
    public PowerUnits NominalGeneration =>
        ShipSystemAdmission.SupportedSingle(Systems, ShipSystemKind.PowerGeneration) is { } generator
            ? ((PowerGenerationSystemDefinition)generator.Definition).NominalOutput
            : default;

    /// <summary>
    /// Gets available power: the floor of nominal output times generator condition, or zero without a generator.
    /// </summary>
    public PowerUnits AvailablePower
    {
        get
        {
            InstalledSystem? generator = ShipSystemAdmission.SupportedSingle(Systems, ShipSystemKind.PowerGeneration);
            if (generator is null)
            {
                return default;
            }

            // Decimal keeps the historical expression exact: floor(120 × 0.625) must be 75, not 74.99….
            var definition = (PowerGenerationSystemDefinition)generator.Definition;
            decimal available = decimal.Floor(definition.NominalOutput.Value * (decimal)generator.Condition.Value);
            return new PowerUnits(decimal.ToInt32(available));
        }
    }

    /// <summary>Gets unallocated available power.</summary>
    public PowerUnits Reserve => new(checked((int)(AvailablePower.Value - Allocation.Total)));

    /// <summary>Gets a consumer's demand satisfaction on the unit interval; nonconsumers are zero.</summary>
    public static PowerSatisfactionRatio PowerSatisfaction(InstalledSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (system.Definition.Power is not { } power || system.Allocation is not { } allocation)
        {
            return default;
        }

        return new PowerSatisfactionRatio(Math.Min(1, (double)allocation.Value / power.NominalDemand.Value));
    }

    /// <summary>Gets effective capability: condition times demand satisfaction.</summary>
    public static SystemCapability Capability(InstalledSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        return new SystemCapability(system.Condition.Value * PowerSatisfaction(system).Value);
    }

    /// <summary>Generates the Balanced allocation over installed consumer demands.</summary>
    public PowerAllocation BalancedAllocation()
    {
        IReadOnlyList<InstalledSystem> consumers = Systems.Consumers;
        if (consumers.Count == 0)
        {
            return PowerAllocation.Empty;
        }

        long[] demands = [.. consumers.Select(consumer => (long)consumer.Definition.Power!.NominalDemand.Value)];
        long totalDemand = 0;
        foreach (long demand in demands)
        {
            totalDemand = checked(totalDemand + demand);
        }

        long budget = Math.Min(AvailablePower.Value, totalDemand);
        long[] shares = new long[demands.Length];
        long assigned = 0;
        for (int index = 0; index < shares.Length; index++)
        {
            shares[index] = checked(budget * demands[index]) / totalDemand;
            assigned = checked(assigned + shares[index]);
        }

        DistributeRemainder(shares, demands, checked(budget - assigned));
        return AllocationFrom(consumers, shares);
    }

    /// <summary>
    /// Generates a priority allocation: the named consumer first, then the rest in canonical order.
    /// </summary>
    public PowerAllocation PriorityAllocation(InstalledSystemId priority)
    {
        IReadOnlyList<InstalledSystem> consumers = Systems.Consumers;
        int priorityIndex = -1;
        for (int index = 0; index < consumers.Count; index++)
        {
            if (consumers[index].Id == priority)
            {
                priorityIndex = index;
            }
        }

        if (priorityIndex < 0)
        {
            throw new ArgumentException(
                $"Installed system {priority.Value} is not a consumer on this ship.",
                nameof(priority)
            );
        }

        long remaining = AvailablePower.Value;
        long[] shares = new long[consumers.Count];
        shares[priorityIndex] = Math.Min(remaining, consumers[priorityIndex].Definition.Power!.NominalDemand.Value);
        remaining -= shares[priorityIndex];
        for (int index = 0; index < consumers.Count; index++)
        {
            if (index == priorityIndex)
            {
                continue;
            }

            shares[index] = Math.Min(remaining, consumers[index].Definition.Power!.NominalDemand.Value);
            remaining -= shares[index];
        }

        return AllocationFrom(consumers, shares);
    }

    /// <summary>
    /// Forced brownout: scales previously committed allocations down to available power.
    /// </summary>
    /// <remarks>
    /// Distinct from Balanced by design: it scales prior shares, not demands, and never raises any consumer above
    /// its prior share, so a brownout cannot silently redistribute power the player chose not to assign.
    /// </remarks>
    public PowerAllocation ReconcileAvailablePower()
    {
        IReadOnlyList<InstalledSystem> consumers = Systems.Consumers;
        long available = AvailablePower.Value;
        long[] previous = [.. consumers.Select(consumer => (long)consumer.Allocation!.Value.Value)];
        long total = 0;
        foreach (long share in previous)
        {
            total = checked(total + share);
        }

        if (total <= available)
        {
            return Allocation;
        }

        long[] shares = new long[previous.Length];
        long assigned = 0;
        for (int index = 0; index < shares.Length; index++)
        {
            shares[index] = checked(previous[index] * available) / total;
            assigned = checked(assigned + shares[index]);
        }

        DistributeRemainder(shares, previous, checked(available - assigned));
        return AllocationFrom(consumers, shares);
    }

    internal ShipEngineeringState WithCondition(InstalledSystemId id, SystemCondition condition) =>
        this with
        {
            Systems = Systems.WithCondition(id, condition),
        };

    internal ShipEngineeringState WithAllocation(PowerAllocation exact) =>
        this with
        {
            Systems = Systems.WithAllocation(exact),
        };

    internal ShipEngineeringState WithRepair(SystemRepairState? repair) => this with { ActiveRepair = repair };

    /// <summary>
    /// Invariant form of the exact-allocation rules plus identity continuation, used by bootstrap, restore, and
    /// every simulation validation.
    /// </summary>
    internal void Validate()
    {
        long largest = Systems.Count == 0 ? 0 : Systems.ByIdentity[^1].Id.Value;
        if (InstallationIds.NextId <= largest)
        {
            throw new InvalidOperationException(
                $"Installed system continuation {InstallationIds.NextId} must exceed retained identity {largest}."
            );
        }

        foreach (InstalledSystem consumer in Systems.Consumers)
        {
            if (consumer.Allocation!.Value > consumer.Definition.Power!.NominalDemand)
            {
                throw new InvalidOperationException(
                    $"Engineering allocation exceeds authored demand of installed system {consumer.Id.Value}."
                );
            }
        }

        if (Allocation.Total > AvailablePower.Value)
        {
            throw new InvalidOperationException("Engineering allocation exceeds currently available power.");
        }
    }

    private static PowerAllocation AllocationFrom(IReadOnlyList<InstalledSystem> consumers, long[] shares) =>
        new(
            consumers.Select(
                (consumer, index) => new PowerAllocationEntry(consumer.Id, new PowerUnits((int)shares[index]))
            )
        );

    /// <summary>
    /// Adds one unit to each still-eligible consumer in canonical order until the remainder is spent.
    /// </summary>
    /// <remarks>
    /// One pass suffices for both callers: each exact proportional quotient is below its limit whenever the budget is
    /// short, so every consumer that lost a fraction is eligible, and the remainder (the sum of those fractions) is
    /// smaller than their count. Zero-limit consumers are skipped. A leftover remainder therefore means a broken
    /// invariant, and silently dropping it would leak power out of the conservation rule.
    /// </remarks>
    private static void DistributeRemainder(long[] shares, long[] limits, long remainder)
    {
        for (int index = 0; index < shares.Length && remainder > 0; index++)
        {
            if (shares[index] < limits[index])
            {
                shares[index]++;
                remainder--;
            }
        }

        if (remainder != 0)
        {
            throw new InvalidOperationException("Proportional allocation left an undistributable remainder.");
        }
    }
}
