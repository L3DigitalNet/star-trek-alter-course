namespace AlterCourse.Core.Ships;

/// <summary>Identifies a stable ship definition independently from runtime instances.</summary>
public readonly record struct ShipDefinitionId
{
    /// <summary>Initializes a stable ship-definition identity.</summary>
    public ShipDefinitionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the persisted identity.</summary>
    public string Value { get; }
}
