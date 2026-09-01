namespace AlterCourse.AssetCtl.Providers;

internal static class ProviderEndpointPolicy
{
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
