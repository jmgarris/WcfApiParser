namespace WcfNetTcpClientGenerator.Core;

public sealed class ClientLibraryGenerationOptions
{
    public GeneratedOutputKind OutputKind { get; init; } = GeneratedOutputKind.NetTcpClientLibrary;

    public bool EnableSwagger { get; init; } = true;
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

    /// <summary>Credential used by the net.tcp transport layer.</summary>
    public string TcpTransportClientCredentialType { get; init; } = "Windows";

    /// <summary>Credential carried in the WCF message when message credentials are enabled.</summary>
    public string MessageClientCredentialType { get; init; } = "None";

    public string ClientCertificateSource { get; init; } = "Store";
    public string ClientCertificateStoreLocation { get; init; } = "CurrentUser";
    public string ClientCertificateStoreName { get; init; } = "My";
    public string ClientCertificateFindType { get; init; } = "FindByThumbprint";
    public string ClientCertificateFindValue { get; init; } = string.Empty;
    public string ClientCertificateFilePath { get; init; } = string.Empty;
    public string ClientCertificateFilePasswordSource { get; init; } = "EnvironmentVariable";
    public string ClientCertificateFilePasswordEnvironmentVariableName { get; init; } = "WCF_CLIENT_CERT_PASSWORD";
    public string ClientCertificateFilePasswordAppSettingName { get; init; } = "Wcf:ClientCertificatePassword";

    public bool ReliableSessionEnabled { get; init; }

    public string OpenTimeout { get; init; } = "00:00:30";

    public string CloseTimeout { get; init; } = "00:00:30";

    public string SendTimeout { get; init; } = "00:01:40";

    public string ReceiveTimeout { get; init; } = "00:01:40";

    public string MaxReceivedMessageSize { get; init; } = "65536";

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? ExistingProxyCode { get; init; }

    public MethodDocumentationOptions DocumentationOptions { get; init; } = new();

    public IMethodDocumentationProvider? MethodDocumentationProvider { get; init; }

    public IProgress<GenerationDiagnostic>? ProgressReporter { get; init; }
}
