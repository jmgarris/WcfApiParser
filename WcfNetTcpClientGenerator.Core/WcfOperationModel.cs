namespace WcfNetTcpClientGenerator.Core;

public sealed class WcfOperationModel
{
    public string OperationName { get; init; } = string.Empty;

    public string MethodName { get; init; } = string.Empty;

    public string ProxyMethodName { get; init; } = string.Empty;

    public string ResponseTypeName { get; init; } = "void";

    public IReadOnlyList<WcfParameterModel> Parameters { get; init; } = [];
}
