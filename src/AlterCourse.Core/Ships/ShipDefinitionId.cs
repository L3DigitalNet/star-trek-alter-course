namespace AlterCourse.Core.Ships;

/// <summary>Identifies a stable ship definition independently from runtime instances.</summary>
public readonly record struct ShipDefinitionId
{
    /// <summary>Gets the maximum persisted ASCII identity length accepted by content and saves.</summary>
    public const int MaximumLength = 128;

    /// <summary>Initializes an ASCII ship-definition identity from letters, digits, hyphens, underscores, or periods.</summary>
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

        if (!value.All(IsAllowedIdentityCharacter))
        {
            throw new ArgumentException(
                "Ship definition identity may contain only ASCII letters, digits, hyphens, underscores, and periods.",
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
