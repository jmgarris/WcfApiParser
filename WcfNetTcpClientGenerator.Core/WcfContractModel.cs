namespace WcfNetTcpClientGenerator.Core;

public sealed class WcfContractModel
{
    public string ContractName { get; init; } = string.Empty;

    public string ClientClassName { get; init; } = string.Empty;

    public string ProxyNamespace { get; init; } = string.Empty;

    public IReadOnlyList<WcfOperationModel> Operations { get; init; } = [];
}
