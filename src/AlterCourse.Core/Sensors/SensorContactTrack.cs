using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Sensors;

/// <summary>Stores authoritative observer-local knowledge and its internal target correlation.</summary>
/// <remarks>
/// <para>
/// <see cref="ObservedAtLocationId"/> qualifies the observed tactical facts with the strategic
/// location the observation happened in, so a retained report stays meaningful after either ship
/// travels. It is deliberately a required positional parameter with no default: a future observation
/// path that rebuilds a track with a constructor call instead of a <c>with</c> expression must fail
/// to compile rather than silently record an unqualified observation.
/// </para>
/// <para>
/// Null means an explicitly unqualified legacy observation restored from a save schema that never
/// recorded a frame. It is unreachable from runtime observation paths and is not an "unknown
/// location" or "observed while traveling" concept — neither is representable, because observation
/// requires both ships to be at one shared location.
/// </para>
/// </remarks>
internal sealed record SensorContactTrack(
    SensorContactId Id,
    ShipInstanceId TargetShipId,
    TacticalPosition LastObservedPosition,
    SimulationTime LastObservedAt,
    LocationId? ObservedAtLocationId,
    SensorContactStatus Status,
    SensorContactIdentification Identification,
    string? KnownVesselDisplayName = null,
    string? KnownDesignDisplayName = null,
    ScheduledWorkId? LossWorkId = null,
    SimulationTime? LossDueTime = null
)
{
    internal SensorContactSnapshot ToActorSafeSnapshot() =>
        new(
            Id,
            LastObservedPosition,
            LastObservedAt,
            Status,
            Identification,
            KnownVesselDisplayName,
            KnownDesignDisplayName
        );
}
