using System.Runtime.InteropServices;
using AlterCourse.Core.Identity;

namespace AlterCourse.Core.Simulation;

/// <summary>Identifies one scheduled-work owner in the closed ship-or-faction target domain.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ScheduledWorkTarget
{
    private ScheduledWorkTarget(ScheduledWorkTargetKind kind, ShipInstanceId? shipId, FactionId? factionId)
    {
        Kind = kind;
        ShipId = shipId;
        FactionId = factionId;
    }

    /// <summary>Gets the target domain.</summary>
    public ScheduledWorkTargetKind Kind { get; }

    /// <summary>Gets the ship identity when <see cref="Kind"/> is <see cref="ScheduledWorkTargetKind.Ship"/>.</summary>
    public ShipInstanceId? ShipId { get; }

    /// <summary>Gets the faction identity when <see cref="Kind"/> is <see cref="ScheduledWorkTargetKind.Faction"/>.</summary>
    public FactionId? FactionId { get; }

    /// <summary>Creates an initialized ship target.</summary>
    /// <param name="shipId">The ship instance that owns the scheduled consequence.</param>
    /// <returns>An initialized ship target.</returns>
    /// <exception cref="ArgumentException"><paramref name="shipId"/> is uninitialized.</exception>
    public static ScheduledWorkTarget ForShip(ShipInstanceId shipId)
    {
        if (shipId.Value <= 0)
        {
            throw new ArgumentException("Scheduled work requires an initialized target ship identity.", nameof(shipId));
        }

        return new ScheduledWorkTarget(ScheduledWorkTargetKind.Ship, shipId, null);
    }

    /// <summary>Creates an initialized faction target.</summary>
    /// <param name="factionId">The faction that owns the scheduled consequence.</param>
    /// <returns>An initialized faction target.</returns>
    /// <exception cref="ArgumentException"><paramref name="factionId"/> is uninitialized.</exception>
    public static ScheduledWorkTarget ForFaction(FactionId factionId)
    {
        if (factionId.Value <= 0)
        {
            throw new ArgumentException(
                "Scheduled work requires an initialized target faction identity.",
                nameof(factionId)
            );
        }

        return new ScheduledWorkTarget(ScheduledWorkTargetKind.Faction, null, factionId);
    }

    internal static void Validate(ScheduledWorkTarget target)
    {
        bool valid = target.Kind switch
        {
            ScheduledWorkTargetKind.Ship => target.ShipId is { Value: > 0 } && target.FactionId is null,
            ScheduledWorkTargetKind.Faction => target.FactionId is { Value: > 0 } && target.ShipId is null,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException(
                "Scheduled work target must contain exactly one initialized identity matching its domain.",
                nameof(target)
            );
        }
    }
}
