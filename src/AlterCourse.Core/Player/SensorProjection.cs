using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Player;

/// <summary>Projects sensor integrity and repair state derived at projection time.</summary>
/// <remarks>
/// <see cref="SensorInstallation"/> is null when the player ship has no sensor installation. <see cref="Integrity"/>
/// is then zero only because the ship cannot sense; presentation must state the capability as unavailable rather
/// than show that zero as a damaged-sensor reading.
/// </remarks>
public sealed record SensorProjection
{
    internal SensorProjection(
        double integrity,
        double repairProgress,
        bool isRepairing,
        IReadOnlyList<SensorContactSnapshot> contacts,
        IReadOnlyList<SensorContactActionProjection> contactActions,
        SensorContactId? activeScanContactId,
        double? activeScanProgress,
        InstalledSystemId? sensorInstallation
    ) =>
        (
            Integrity,
            RepairProgress,
            IsRepairing,
            Contacts,
            ContactActions,
            ActiveScanContactId,
            ActiveScanProgress,
            SensorInstallation
        ) = (
            integrity,
            repairProgress,
            isRepairing,
            contacts,
            contactActions,
            activeScanContactId,
            activeScanProgress,
            sensorInstallation
        );

    /// <summary>Gets the player's own sensor installation, or null when none is installed.</summary>
    public InstalledSystemId? SensorInstallation { get; }

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

    /// <summary>Gets bounded active-scan progress derived at the projection time.</summary>
    public double? ActiveScanProgress { get; }
}
