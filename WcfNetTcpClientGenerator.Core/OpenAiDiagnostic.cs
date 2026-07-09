namespace WcfNetTcpClientGenerator.Core;

public sealed class OpenAiDiagnostic
{
    public string Severity { get; init; } = "Info";

    public string Message { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public int? StatusCode { get; init; }
}
