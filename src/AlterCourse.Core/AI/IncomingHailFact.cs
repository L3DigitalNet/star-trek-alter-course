using AlterCourse.Core.Sensors;

namespace AlterCourse.Core.AI;

/// <summary>Reports vessel and design names transmitted by one observer-local contact.</summary>
public sealed record IncomingHailFact
{
    /// <summary>Initializes an actor-safe incoming-hail fact.</summary>
    public IncomingHailFact(
        SensorContactId sourceContactId,
        string transmittedVesselDisplayName,
        string transmittedDesignDisplayName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transmittedVesselDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(transmittedDesignDisplayName);
        SourceContactId = sourceContactId;
        TransmittedVesselDisplayName = transmittedVesselDisplayName;
        TransmittedDesignDisplayName = transmittedDesignDisplayName;
    }

    /// <summary>Gets the observer-local identity of the transmitting contact.</summary>
    public SensorContactId SourceContactId { get; }

    /// <summary>Gets the vessel display name transmitted by the contact.</summary>
    public string TransmittedVesselDisplayName { get; }

    /// <summary>Gets the design display name transmitted by the contact.</summary>
    public string TransmittedDesignDisplayName { get; }
}
