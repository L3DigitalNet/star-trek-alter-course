namespace AlterCourse.Core.Factions;

/// <summary>Records whether a retained received report remains available for response.</summary>
public enum ObservationReportHandling
{
    /// <summary>The report has not completed an investigation response.</summary>
    Unhandled = 1,

    /// <summary>The report's investigation has completed.</summary>
    Handled = 2,
}
