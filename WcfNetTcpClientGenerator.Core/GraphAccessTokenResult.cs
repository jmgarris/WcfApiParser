namespace WcfNetTcpClientGenerator.Core;

public sealed class GraphAccessTokenResult
{
    public bool Success { get; init; }

    public string AccessToken { get; init; } = string.Empty;

    public string AccountName { get; init; } = string.Empty;

    public IReadOnlyList<CopilotChatDiagnostic> Diagnostics { get; init; } = [];
}
