namespace AlterCourse.Core.Strategic;

/// <summary>Identifies one stable strategic-map location.</summary>
public readonly record struct LocationId
{
    internal const int MaximumLength = 128;

    /// <summary>Initializes an ASCII location identity from letters, digits, hyphens, underscores, or periods.</summary>
    /// <param name="value">The nonblank persisted identity.</param>
    public LocationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > MaximumLength)
        {
            throw new ArgumentException($"Location identity cannot exceed {MaximumLength} characters.", nameof(value));
        }

        if (!value.All(IsAllowedIdentityCharacter))
        {
            throw new ArgumentException(
                "Location identity may contain only ASCII letters, digits, hyphens, underscores, and periods.",
                nameof(value)
            );
        }

        Value = value;
    }

    /// <summary>Gets the persisted identity.</summary>
    public string Value { get; }

    private static bool IsAllowedIdentityCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.';
}
