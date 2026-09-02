using System.Runtime.InteropServices;
using AlterCourse.Core.Identity;

namespace AlterCourse.Core.Simulation;

/// <summary>Describes a persistable scheduled consequence without executable callbacks.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ScheduledWork
{
    /// <summary>Initializes a scheduled-work item.</summary>
    /// <param name="id">The stable scheduled-work identity.</param>
    /// <param name="dueTime">The simulation time at which the work becomes due.</param>
    /// <param name="sequence">The nonnegative persisted same-time ordering sequence.</param>
    /// <param name="targetShipId">The ship instance that owns the scheduled consequence.</param>
    /// <param name="kind">The known consequence kind.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> or <paramref name="targetShipId"/> is an uninitialized identity.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sequence"/> is negative or <paramref name="kind"/> is unknown.
    /// </exception>
    public ScheduledWork(
        ScheduledWorkId id,
        SimulationTime dueTime,
        long sequence,
        ShipInstanceId targetShipId,
        ScheduledWorkKind kind
    )
    {
        if (id.Value <= 0)
        {
            throw new ArgumentException("Scheduled work requires an initialized identity.", nameof(id));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ValidateTarget(targetShipId);
        ValidateKind(kind);

        Id = id;
        DueTime = dueTime;
        Sequence = sequence;
        TargetShipId = targetShipId;
        Kind = kind;
    }

    /// <summary>Gets the stable scheduled-work identity.</summary>
    public ScheduledWorkId Id { get; }

    /// <summary>Gets the simulation time at which the work becomes due.</summary>
    public SimulationTime DueTime { get; }

    /// <summary>Gets the persisted same-time ordering sequence.</summary>
    public long Sequence { get; }

    /// <summary>Gets the ship instance that owns the scheduled consequence.</summary>
    public ShipInstanceId TargetShipId { get; }

    /// <summary>Gets the known consequence kind.</summary>
    public ScheduledWorkKind Kind { get; }

    internal static void ValidateTarget(ShipInstanceId targetShipId)
    {
        if (targetShipId.Value <= 0)
        {
            throw new ArgumentException(
                "Scheduled work requires an initialized target ship identity.",
                nameof(targetShipId)
            );
        }
    }

    internal static void ValidateKind(ScheduledWorkKind kind)
    {
        if (
            kind
            is not ScheduledWorkKind.TravelArrival
                and not ScheduledWorkKind.SensorRepairCompletion
                and not ScheduledWorkKind.OrderWake
        )
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Scheduled work kind is not supported.");
        }
    }
}
