namespace WcfNetTcpClientGenerator.Core;

public sealed class ClientLibraryGenerationOptions
{
    public WcfMetadataDiscoveryOptions DiscoveryOptions { get; init; } = new();

    public string GeneratedLibraryName { get; init; } = "GeneratedNetTcpClient";

    public string PackageId { get; init; } = "GeneratedNetTcpClient";

    public string PackageVersion { get; init; } = "1.0.0";

    public string Authors { get; init; } = "WcfNetTcpClientGenerator";

    public string Company { get; init; } = "WcfNetTcpClientGenerator";

    public string Description { get; init; } = "Generated WCF Net.TCP client library.";

    public string RepositoryUrl { get; init; } = "https://example.com/repository";

    public string OutputFolder { get; init; } = string.Empty;

    public string SecurityMode { get; init; } = "Transport";

    public string TcpClientCredentialType { get; init; } = "Windows";

    public bool ReliableSessionEnabled { get; init; }

    public string OpenTimeout { get; init; } = "00:00:30";

    public string CloseTimeout { get; init; } = "00:00:30";

    public string SendTimeout { get; init; } = "00:01:40";

    public string ReceiveTimeout { get; init; } = "00:01:40";

    public string MaxReceivedMessageSize { get; init; } = "65536";

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? ExistingProxyCode { get; init; }
}
