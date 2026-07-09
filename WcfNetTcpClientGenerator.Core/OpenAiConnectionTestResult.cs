namespace WcfNetTcpClientGenerator.Core;

public sealed class OpenAiConnectionTestResult
{
    public bool Success { get; init; }

    public string StatusText { get; init; } = string.Empty;

    public string ModelName { get; init; } = string.Empty;

    public IReadOnlyList<OpenAiDiagnostic> Diagnostics { get; init; } = [];
}
