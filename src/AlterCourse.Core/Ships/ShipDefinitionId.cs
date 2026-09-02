namespace AlterCourse.Core.Ships;

/// <summary>Identifies a stable ship definition independently from runtime instances.</summary>
public readonly record struct ShipDefinitionId
{
    /// <summary>Gets the maximum persisted identity length accepted by content and saves.</summary>
    public const int MaximumLength = 128;

    /// <summary>Initializes a stable ship-definition identity.</summary>
    public ShipDefinitionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"Ship definition identity cannot exceed {MaximumLength} characters.",
                nameof(value)
            );
        }

        Value = value;
    }

    /// <summary>Gets the persisted identity.</summary>
    public string Value { get; }
}
