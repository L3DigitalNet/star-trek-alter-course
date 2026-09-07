using AlterCourse.Core.Identity;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Factions;

/// <summary>Preserves only the historical observer-local facts admitted into one faction report.</summary>
public sealed record ObservationReportSnapshot
{
    /// <summary>Initializes an immutable actor-safe historical observation.</summary>
    public ObservationReportSnapshot(
        ObservationReportId reportId,
        ShipInstanceId observerShipId,
        SensorContactId observerContactId,
        LocationId observedAtLocationId,
        TacticalPosition observedPosition,
        SimulationTime observedAt,
        SensorContactIdentification identification,
        string? knownVesselDisplayName = null,
        string? knownDesignDisplayName = null
    )
    {
        if (reportId.Value <= 0)
        {
            throw new ArgumentException("An observation report requires an initialized identity.", nameof(reportId));
        }

        if (observerShipId.Value <= 0)
        {
            throw new ArgumentException(
                "An observation report requires an initialized observer.",
                nameof(observerShipId)
            );
        }

        if (observerContactId.Value <= 0)
        {
            throw new ArgumentException(
                "An observation report requires an initialized observer-local contact.",
                nameof(observerContactId)
            );
        }

        if (string.IsNullOrWhiteSpace(observedAtLocationId.Value))
        {
            throw new ArgumentException(
                "An observation report requires an initialized strategic reference frame.",
                nameof(observedAtLocationId)
            );
        }

        bool validIdentification = identification switch
        {
            SensorContactIdentification.Detected => knownVesselDisplayName is null && knownDesignDisplayName is null,
            SensorContactIdentification.Identified => knownVesselDisplayName
                is { Length: <= ShipState.MaximumVesselDisplayNameLength }
                && knownDesignDisplayName is { Length: <= ShipDefinition.MaximumDesignDisplayNameLength }
                && !string.IsNullOrWhiteSpace(knownVesselDisplayName)
                && !string.IsNullOrWhiteSpace(knownDesignDisplayName),
            _ => false,
        };
        if (!validIdentification)
        {
            throw new ArgumentException(
                "Observation identification must contain exactly the display facts legitimately learned by the observer.",
                nameof(identification)
            );
        }

        ReportId = reportId;
        ObserverShipId = observerShipId;
        ObserverContactId = observerContactId;
        ObservedAtLocationId = observedAtLocationId;
        ObservedPosition = observedPosition;
        ObservedAt = observedAt;
        Identification = identification;
        KnownVesselDisplayName = knownVesselDisplayName;
        KnownDesignDisplayName = knownDesignDisplayName;
    }

    /// <summary>Gets the stable identity shared by delivery, receipt, and investigation state.</summary>
    public ObservationReportId ReportId { get; }

    /// <summary>Gets the reporting observer that legitimately knew the snapshot.</summary>
    public ShipInstanceId ObserverShipId { get; }

    /// <summary>Gets the contact identity local to the reporting observer.</summary>
    public SensorContactId ObserverContactId { get; }

    /// <summary>Gets the strategic reference frame in which the observation occurred.</summary>
    public LocationId ObservedAtLocationId { get; }

    /// <summary>Gets the historical tactical position inside the observation frame.</summary>
    public TacticalPosition ObservedPosition { get; }

    /// <summary>Gets the source observation time.</summary>
    public SimulationTime ObservedAt { get; }

    /// <summary>Gets whether the observer had learned vessel and design display facts.</summary>
    public SensorContactIdentification Identification { get; }

    /// <summary>Gets the legitimately learned vessel display name, when identified.</summary>
    public string? KnownVesselDisplayName { get; }

    /// <summary>Gets the legitimately learned design display name, when identified.</summary>
    public string? KnownDesignDisplayName { get; }
}
