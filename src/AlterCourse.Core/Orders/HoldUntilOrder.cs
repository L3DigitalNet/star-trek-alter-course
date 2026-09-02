using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Orders;

/// <summary>Directs a ship to hold until one exact scheduled wake becomes due.</summary>
public sealed record HoldUntilOrder : ShipOrder
{
    /// <summary>Initializes a hold order with persisted time and wake correlation.</summary>
    /// <param name="id">The stable order identity.</param>
    /// <param name="until">The simulation time at which the hold ends.</param>
    /// <param name="scheduledWakeId">The exact scheduled-work identity authorized to wake the order.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> or <paramref name="scheduledWakeId"/> is uninitialized.
    /// </exception>
    public HoldUntilOrder(ShipOrderId id, SimulationTime until, ScheduledWorkId scheduledWakeId)
        : base(id, ShipOrderKind.HoldUntil)
    {
        if (scheduledWakeId.Value <= 0)
        {
            throw new ArgumentException(
                "A hold order requires an initialized scheduled wake identity.",
                nameof(scheduledWakeId)
            );
        }

        Until = until;
        ScheduledWakeId = scheduledWakeId;
    }

    /// <summary>Gets the simulation time at which the hold ends.</summary>
    public SimulationTime Until { get; }

    /// <summary>Gets the exact scheduled-work identity authorized to wake the order.</summary>
    public ScheduledWorkId ScheduledWakeId { get; }
}
