namespace WcfNetTcpClientGenerator.App.Models;

public sealed class OperationRow
{
    public required string ContractName { get; init; }

    public required string OperationName { get; init; }

    public required string Signature { get; init; }
}
