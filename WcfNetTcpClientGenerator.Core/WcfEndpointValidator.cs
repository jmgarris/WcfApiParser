namespace WcfNetTcpClientGenerator.Core;

public static class WcfEndpointValidator
{
    public static bool IsValidNetTcpEndpoint(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && uri.Scheme.Equals(Uri.UriSchemeNetTcp, StringComparison.OrdinalIgnoreCase);

    public static bool IsValidMetadataUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
               || uri.Scheme.Equals(Uri.UriSchemeNetTcp, StringComparison.OrdinalIgnoreCase));
}
