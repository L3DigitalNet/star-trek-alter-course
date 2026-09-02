namespace AlterCourse.AssetCtl.Providers;

internal static class ProviderRequestIdPolicy
{
    private const int MaximumLength = 128;

    public static string? Sanitize(string? value, string credential)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
        {
            return null;
        }

        if (!IsAsciiLetterOrDigit(value[0]))
        {
            return null;
        }

        if (
            !string.IsNullOrEmpty(credential)
            && (
                value.Contains(credential, StringComparison.Ordinal)
                || credential.Length >= 16 && credential.Contains(value, StringComparison.Ordinal)
            )
        )
        {
            return null;
        }

        if (LooksLikeCredential(value))
        {
            return null;
        }

        return value.All(static character => IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-')
            ? value
            : null;
    }

    private static bool LooksLikeCredential(string value)
    {
        if (
            value.StartsWith("sk-", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("xai-", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        if (value.Length >= 32 && value.All(Uri.IsHexDigit))
        {
            return true;
        }

        string[] segments = value.Split('.');
        return segments is { Length: 3 }
            && segments.All(static segment =>
                segment.Length >= 8 && segment.All(static value => IsAsciiLetterOrDigit(value) || value is '-' or '_')
            );
    }

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
}
