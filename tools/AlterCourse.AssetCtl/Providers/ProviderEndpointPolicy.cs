namespace AlterCourse.AssetCtl.Providers;

internal static class ProviderEndpointPolicy
{
    public static void ValidateHost(string host, string field)
    {
        if (
            string.IsNullOrWhiteSpace(host)
            || !string.Equals(host, host.Trim(), StringComparison.Ordinal)
            || host.Contains('*', StringComparison.Ordinal)
            || Uri.CheckHostName(host) is not UriHostNameType.Dns
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate($"https://{host}/", UriKind.Absolute, out Uri? parsed)
            || !string.Equals(parsed.IdnHost, host, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new AssetCtlException(
                $"{field}: expected an exact DNS hostname without a scheme, port, path, or wildcard.",
                2
            );
        }
    }

    public static void Validate(Uri endpoint, IReadOnlySet<string> allowedHosts, string field)
    {
        if (
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment)
            || !allowedHosts.Contains(endpoint.IdnHost)
        )
        {
            throw new AssetCtlException($"{field}: endpoint is outside the adapter's authorized HTTPS hosts.", 2);
        }
    }
}
