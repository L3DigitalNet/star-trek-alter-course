namespace AlterCourse.Core.Strategic;

/// <summary>Identifies one stable strategic-map location.</summary>
public readonly record struct LocationId
{
    internal const int MaximumLength = 128;

    /// <summary>Initializes a stable location identity.</summary>
    /// <param name="value">The nonblank persisted identity.</param>
    public LocationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > MaximumLength)
        {
            throw new ArgumentException($"Location identity cannot exceed {MaximumLength} characters.", nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the persisted identity.</summary>
    public string Value { get; }
}
