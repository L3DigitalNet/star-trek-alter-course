namespace AlterCourse.AssetCtl.Providers;

internal static class ProviderRequestIdPolicy
{
    private const int MaximumLength = 128;

    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
        {
            return null;
        }

        if (!IsAsciiLetterOrDigit(value[0]))
        {
            return null;
        }

        return value.All(static character => IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-')
            ? value
            : null;
    }

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
}
