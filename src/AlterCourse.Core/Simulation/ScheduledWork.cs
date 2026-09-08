using System.Runtime.InteropServices;
using AlterCourse.Core.Identity;

namespace AlterCourse.Core.Simulation;

/// <summary>Describes a persistable scheduled consequence without executable callbacks.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ScheduledWork
{
    /// <summary>Initializes a scheduled-work item.</summary>
    /// <param name="id">The stable scheduled-work identity.</param>
    /// <param name="dueTime">The simulation time at which the work becomes due.</param>
    /// <param name="sequence">The nonnegative persisted same-time ordering sequence.</param>
    /// <param name="target">The ship or faction that owns the scheduled consequence.</param>
    /// <param name="kind">The known consequence kind.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> or <paramref name="target"/> is uninitialized or malformed, or the target domain does
    /// not support <paramref name="kind"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sequence"/> is negative or <paramref name="kind"/> is unknown.
    /// </exception>
    public ScheduledWork(
        ScheduledWorkId id,
        SimulationTime dueTime,
        long sequence,
        ScheduledWorkTarget target,
        ScheduledWorkKind kind
    )
    {
        if (id.Value <= 0)
        {
            throw new ArgumentException("Scheduled work requires an initialized identity.", nameof(id));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ScheduledWorkTarget.Validate(target);
        ValidateKind(kind);
        ValidateTargetKind(target, kind);

        Id = id;
        DueTime = dueTime;
        Sequence = sequence;
        Target = target;
        Kind = kind;
    }

    /// <summary>Initializes ship-targeted scheduled work for existing callers.</summary>
    /// <param name="id">The stable scheduled-work identity.</param>
    /// <param name="dueTime">The simulation time at which the work becomes due.</param>
    /// <param name="sequence">The nonnegative persisted same-time ordering sequence.</param>
    /// <param name="targetShipId">The ship instance that owns the scheduled consequence.</param>
    /// <param name="kind">The known ship consequence kind.</param>
    public ScheduledWork(
        ScheduledWorkId id,
        SimulationTime dueTime,
        long sequence,
        ShipInstanceId targetShipId,
        ScheduledWorkKind kind
    )
        : this(id, dueTime, sequence, ScheduledWorkTarget.ForShip(targetShipId), kind) { }

    /// <summary>Gets the stable scheduled-work identity.</summary>
    public ScheduledWorkId Id { get; }

    /// <summary>Gets the simulation time at which the work becomes due.</summary>
    public SimulationTime DueTime { get; }

    /// <summary>Gets the persisted same-time ordering sequence.</summary>
    public long Sequence { get; }

    /// <summary>Gets the ship or faction that owns the scheduled consequence.</summary>
    public ScheduledWorkTarget Target { get; }

    /// <summary>Gets the ship owner for ship-targeted scheduled work.</summary>
    /// <exception cref="InvalidOperationException">The scheduled work targets a faction.</exception>
    public ShipInstanceId TargetShipId =>
        Target.ShipId ?? throw new InvalidOperationException("Faction-targeted scheduled work has no target ship.");

    /// <summary>Gets the known consequence kind.</summary>
    public ScheduledWorkKind Kind { get; }

    internal static void ValidateKind(ScheduledWorkKind kind)
    {
        if (
            kind
            is not ScheduledWorkKind.TravelArrival
                and not ScheduledWorkKind.SystemRepairCompletion
                and not ScheduledWorkKind.OrderWake
                and not ScheduledWorkKind.SensorContactLoss
                and not ScheduledWorkKind.ActiveSensorScanCompletion
                and not ScheduledWorkKind.ShipContactDecisionWake
                and not ScheduledWorkKind.FactionDecisionWake
                and not ScheduledWorkKind.ObservationReportDelivery
        )
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Scheduled work kind is not supported.");
        }
    }

    internal static void ValidateTargetKind(ScheduledWorkTarget target, ScheduledWorkKind kind)
    {
        bool valid = kind switch
        {
            ScheduledWorkKind.FactionDecisionWake or ScheduledWorkKind.ObservationReportDelivery => target.Kind
                == ScheduledWorkTargetKind.Faction,
            ScheduledWorkKind.TravelArrival
            or ScheduledWorkKind.SystemRepairCompletion
            or ScheduledWorkKind.OrderWake
            or ScheduledWorkKind.SensorContactLoss
            or ScheduledWorkKind.ActiveSensorScanCompletion
            or ScheduledWorkKind.ShipContactDecisionWake => target.Kind == ScheduledWorkTargetKind.Ship,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException(
                "Scheduled work kind does not support the selected target domain.",
                nameof(target)
            );
        }
    }
}
