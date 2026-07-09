namespace WcfNetTcpClientGenerator.App.Models;

public sealed class StatusMessage
{
    public required string Severity { get; init; }

    public required string Message { get; init; }
}
