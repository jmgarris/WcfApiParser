namespace WcfNetTcpClientGenerator.Core;

public sealed class WcfOperationModel
{
    public string OperationName { get; init; } = string.Empty;

    public string MethodName { get; init; } = string.Empty;

    public string ProxyMethodName { get; init; } = string.Empty;

    public string ResponseTypeName { get; init; } = "void";

    public IReadOnlyList<WcfParameterModel> Parameters { get; init; } = [];

    public string DocumentationText { get; init; } = string.Empty;

    public IReadOnlyList<WcfFaultContractModel> FaultContracts { get; init; } = [];
}
