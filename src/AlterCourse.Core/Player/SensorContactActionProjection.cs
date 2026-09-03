using AlterCourse.Core.Sensors;

namespace AlterCourse.Core.Player;

/// <summary>Projects Core-authorized player commands for one observer-local contact.</summary>
public sealed record SensorContactActionProjection
{
    internal SensorContactActionProjection(
        SensorContactId contactId,
        IReadOnlyList<SensorContactAction> availableActions
    ) => (ContactId, AvailableActions) = (contactId, availableActions);

    /// <summary>Gets the observer-local contact whose actions were evaluated.</summary>
    public SensorContactId ContactId { get; }

    /// <summary>Gets the commands currently authorized for this contact.</summary>
    public IReadOnlyList<SensorContactAction> AvailableActions { get; }
}
