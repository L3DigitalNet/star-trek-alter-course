
namespace AlterCourse.Core.Strategic;

/// <summary>Identifies one stable strategic-map location.</summary>
public readonly record struct LocationId
{
    /// <summary>Initializes a stable location identity.</summary>
    /// <param name="value">The nonblank persisted identity.</param>
    public LocationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the persisted identity.</summary>
    public string Value { get; }
}
