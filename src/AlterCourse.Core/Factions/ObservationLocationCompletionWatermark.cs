using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Factions;

/// <summary>Suppresses reports no newer than the latest completed investigation at one location.</summary>
public sealed record ObservationLocationCompletionWatermark
{
    /// <summary>Initializes a sparse location watermark from a completed report observation time.</summary>
    public ObservationLocationCompletionWatermark(LocationId locationId, SimulationTime observedThrough)
    {
        if (string.IsNullOrWhiteSpace(locationId.Value))
        {
            throw new ArgumentException("A completion watermark requires an initialized location.", nameof(locationId));
        }

        LocationId = locationId;
        ObservedThrough = observedThrough;
    }

    /// <summary>Gets the strategic location governed by this watermark.</summary>
    public LocationId LocationId { get; }

    /// <summary>Gets the latest source observation time covered by a completed investigation.</summary>
    public SimulationTime ObservedThrough { get; }
}
