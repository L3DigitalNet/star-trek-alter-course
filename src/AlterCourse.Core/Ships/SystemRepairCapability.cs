using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>
/// Declares that installations of a definition are repairable: how long a full repair takes and where the
/// definition's repair action sits in presentation order.
/// </summary>
public sealed record SystemRepairCapability
{
    /// <summary>Gets the maximum explicit repair-action ordering value accepted by content.</summary>
    public const int MaximumActionOrder = 1_000_000;

    /// <summary>Initializes a positive, fixed-step-aligned full repair duration and its action order.</summary>
    /// <exception cref="ArgumentException">The duration is not positive or not fixed-step aligned.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The action order is outside zero through the maximum.</exception>
    public SystemRepairCapability(SimulationDuration fullRepairDuration, int actionOrder)
    {
        if (
            fullRepairDuration.Milliseconds <= 0
            || fullRepairDuration.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0
        )
        {
            throw new ArgumentException(
                "Full repair duration must be positive and fixed-step aligned.",
                nameof(fullRepairDuration)
            );
        }

        ArgumentOutOfRangeException.ThrowIfNegative(actionOrder);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(actionOrder, MaximumActionOrder);
        FullRepairDuration = fullRepairDuration;
        ActionOrder = actionOrder;
    }

    /// <summary>Gets the time a repair from zero to full condition takes.</summary>
    public SimulationDuration FullRepairDuration { get; }

    /// <summary>
    /// Gets the presentation order of this definition's repair action (ties broken by installed identity). It is
    /// deliberately separate from <see cref="SystemDefinition.CommonOrder"/>: the established Engineering layout
    /// lists repair actions in a different order than simulation processing. It affects no simulation outcome and
    /// is therefore excluded from the save-compatibility descriptor.
    /// </summary>
    public int ActionOrder { get; }
}
