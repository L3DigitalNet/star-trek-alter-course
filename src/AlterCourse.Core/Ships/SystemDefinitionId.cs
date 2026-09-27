namespace AlterCourse.Core.Ships;

/// <summary>
/// Identifies one reusable system definition (a component design such as a specific sensor model), independent
/// of the kind vocabulary and of any installation on a ship.
/// </summary>
/// <remarks>
/// The charset matches <see cref="ShipDefinitionId"/> and the <c>system-definition-v1</c> schema pattern; the
/// 64-character bound matches that schema's <c>maxLength</c>. Saves persist this value as a compatibility key, so
/// the three boundaries must change together.
/// </remarks>
public readonly record struct SystemDefinitionId
{
    /// <summary>Gets the maximum persisted ASCII identity length accepted by content and saves.</summary>
    public const int MaximumLength = 64;

    /// <summary>Initializes an ASCII definition identity from letters, digits, hyphens, underscores, or periods.</summary>
    /// <exception cref="ArgumentException">The value is empty, too long, or contains another character.</exception>
    public SystemDefinitionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"System definition identity cannot exceed {MaximumLength} characters.",
                nameof(value)
            );
        }

        if (!value.All(IsAllowedIdentityCharacter))
        {
            throw new ArgumentException(
                "System definition identity may contain only ASCII letters, digits, hyphens, underscores, and periods.",
                nameof(value)
            );
        }

        Value = value;
    }

    /// <summary>Gets the persisted identity.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    private static bool IsAllowedIdentityCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.';
}
