namespace WcfNetTcpClientGenerator.Core;

public sealed class MethodDocumentationRequest
{
    public string ServiceName { get; init; } = string.Empty;

    public string OperationName { get; init; } = string.Empty;

    public string GeneratedWrapperMethodName { get; init; } = string.Empty;

    public string RequestTypeName { get; init; } = string.Empty;

    public string ResponseTypeName { get; init; } = string.Empty;

    public IReadOnlyList<WcfParameterModel> Parameters { get; init; } = [];

    public string ReturnType { get; init; } = string.Empty;

    public IReadOnlyList<WcfFaultContractModel> FaultContracts { get; init; } = [];

    public string WcfBindingType { get; init; } = "NetTcpBinding";

    public bool IsAsync { get; init; }

    public string WsdlDocumentationText { get; init; } = string.Empty;

    public string GeneratedMethodSignature { get; init; } = string.Empty;

    public string SampleRequestTypeName { get; init; } = string.Empty;

    public string SampleResponseTypeName { get; init; } = string.Empty;
}
