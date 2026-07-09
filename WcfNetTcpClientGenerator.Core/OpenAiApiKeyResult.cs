namespace WcfNetTcpClientGenerator.Core;

public sealed class OpenAiApiKeyResult
{
    public bool Success { get; init; }

    public string ApiKey { get; init; } = string.Empty;

    public string SourceDescription { get; init; } = string.Empty;

    public IReadOnlyList<OpenAiDiagnostic> Diagnostics { get; init; } = [];
}
