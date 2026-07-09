namespace WcfNetTcpClientGenerator.Core;

public sealed class WcfServiceMetadataModel
{
    public string ServiceNamespace { get; init; } = string.Empty;

    public string SourceDescription { get; init; } = string.Empty;

    public IReadOnlyList<WcfContractModel> Contracts { get; init; } = [];
}
