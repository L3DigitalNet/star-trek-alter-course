using AlterCourse.Core.Sensors;

namespace AlterCourse.Core.Player;

/// <summary>Projects sensor integrity and repair state derived at projection time.</summary>
public sealed record SensorProjection
{
    internal SensorProjection(
        double integrity,
        double repairProgress,
        bool isRepairing,
        IReadOnlyList<SensorContactSnapshot> contacts,
        IReadOnlyList<SensorContactActionProjection> contactActions,
        SensorContactId? activeScanContactId
    ) =>
        (Integrity, RepairProgress, IsRepairing, Contacts, ContactActions, ActiveScanContactId) = (
            integrity,
            repairProgress,
            isRepairing,
            contacts,
            contactActions,
            activeScanContactId
        );

    /// <summary>Gets bounded sensor integrity.</summary>
    public double Integrity { get; }

    /// <summary>Gets bounded repair progress, including one after completion.</summary>
    public double RepairProgress { get; }

    /// <summary>Gets whether a repair remains active.</summary>
    public bool IsRepairing { get; }

    /// <summary>Gets retained contacts using identities local to the player ship.</summary>
    public IReadOnlyList<SensorContactSnapshot> Contacts { get; }

    /// <summary>Gets Core-authorized player commands grouped by observer-local contact.</summary>
    public IReadOnlyList<SensorContactActionProjection> ContactActions { get; }

    /// <summary>Gets the local contact currently being scanned, when a scan is active.</summary>
    public SensorContactId? ActiveScanContactId { get; }
}
