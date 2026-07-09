namespace WcfNetTcpClientGenerator.Core;

public sealed class WcfParameterModel
{
    public string Name { get; init; } = string.Empty;

    public string TypeName { get; init; } = string.Empty;

    public bool IsOptional { get; init; }
}
