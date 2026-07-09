namespace WcfNetTcpClientGenerator.Core;

public sealed class CopilotChatResult
{
    public bool Success { get; init; }

    public string ConversationId { get; init; } = string.Empty;

    public string ResponseText { get; init; } = string.Empty;

    public int? StatusCode { get; init; }

    public IReadOnlyList<CopilotChatDiagnostic> Diagnostics { get; init; } = [];
}
