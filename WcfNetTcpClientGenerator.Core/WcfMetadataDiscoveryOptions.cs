namespace WcfNetTcpClientGenerator.Core;

public sealed class WcfMetadataDiscoveryOptions
{
    public string? ServiceEndpointUrl { get; init; }

    public string? MetadataEndpointUrl { get; init; }

    public string? WsdlFilePath { get; init; }

    public string? MetadataFolderPath { get; init; }

    public string? DotNetSvcUtilPath { get; init; }

    public string ServiceNamespace { get; init; } = "Generated.Wcf";
}
