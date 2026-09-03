using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Sensors;

/// <summary>Provides one actor-safe immutable view of observer-local contact knowledge.</summary>
public sealed record SensorContactSnapshot
{
    internal SensorContactSnapshot(
        SensorContactId id,
        TacticalPosition lastObservedPosition,
        SimulationTime lastObservedAt,
        SensorContactStatus status,
        SensorContactIdentification identification,
        string? knownVesselDisplayName,
        string? knownDesignDisplayName
    ) =>
        (
            Id,
            LastObservedPosition,
            LastObservedAt,
            Status,
            Identification,
            KnownVesselDisplayName,
            KnownDesignDisplayName
        ) =
        (
            id,
            lastObservedPosition,
            lastObservedAt,
            status,
            identification,
            knownVesselDisplayName,
            knownDesignDisplayName
        );

    /// <summary>Gets the identity local to the observing ship.</summary>
    public SensorContactId Id { get; }

    /// <summary>Gets the last position observed, which need not be the target's current position.</summary>
    public TacticalPosition LastObservedPosition { get; }

    /// <summary>Gets the simulation time of the last observation.</summary>
    public SimulationTime LastObservedAt { get; }

    /// <summary>Gets the contact's current knowledge-lifecycle status.</summary>
    public SensorContactStatus Status { get; }

    /// <summary>Gets whether identity display facts have been learned.</summary>
    public SensorContactIdentification Identification { get; }

    /// <summary>Gets the learned vessel display name, when identified.</summary>
    public string? KnownVesselDisplayName { get; }

    /// <summary>Gets the learned design display name, when identified.</summary>
    public string? KnownDesignDisplayName { get; }
}
