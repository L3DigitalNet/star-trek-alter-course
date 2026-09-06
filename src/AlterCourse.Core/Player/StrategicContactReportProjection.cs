using AlterCourse.Core.Sensors;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Player;

/// <summary>Projects one retained contact report qualified by the strategic location it was observed in.</summary>
/// <remarks>
/// Every member is a fact the player ship itself recorded, so the report survives later authoritative
/// changes to the target unchanged. The observing ship is not named because the projection carries
/// exactly one observer, and the observed location's display name is not denormalized here because
/// <see cref="StrategicProjection.Locations"/> already owns it — consumers join by
/// <see cref="ObservedAtLocationId"/>.
/// </remarks>
public sealed record StrategicContactReportProjection
{
    internal StrategicContactReportProjection(
        SensorContactId contactId,
        LocationId observedAtLocationId,
        TacticalPosition lastObservedPosition,
        SimulationTime lastObservedAt,
        SensorContactStatus status,
        SensorContactIdentification identification,
        string? knownVesselDisplayName,
        string? knownDesignDisplayName
    ) =>
        (
            ContactId,
            ObservedAtLocationId,
            LastObservedPosition,
            LastObservedAt,
            Status,
            Identification,
            KnownVesselDisplayName,
            KnownDesignDisplayName
        ) = (
            contactId,
            observedAtLocationId,
            lastObservedPosition,
            lastObservedAt,
            status,
            identification,
            knownVesselDisplayName,
            knownDesignDisplayName
        );

    /// <summary>Gets the observer-local contact identity, stable across stale, loss, and reacquisition.</summary>
    public SensorContactId ContactId { get; }

    /// <summary>Gets the strategic location the observation was recorded in.</summary>
    public LocationId ObservedAtLocationId { get; }

    /// <summary>Gets the last observed tactical position, local to the observed location.</summary>
    public TacticalPosition LastObservedPosition { get; }

    /// <summary>Gets the simulation time of the last observation.</summary>
    public SimulationTime LastObservedAt { get; }

    /// <summary>Gets the retained contact status, including lost contacts the tactical surface drops.</summary>
    public SensorContactStatus Status { get; }

    /// <summary>Gets how far the observer has identified the contact.</summary>
    public SensorContactIdentification Identification { get; }

    /// <summary>Gets the learned vessel name, or null until the contact is identified.</summary>
    public string? KnownVesselDisplayName { get; }

    /// <summary>Gets the learned design name, or null until the contact is identified.</summary>
    public string? KnownDesignDisplayName { get; }
}
