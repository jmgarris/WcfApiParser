namespace WcfNetTcpClientGenerator.Core;

public sealed class CopilotChatOptions
{
    public string TenantId { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public IReadOnlyList<string> RequiredScopes { get; init; } =
    [
        "Sites.Read.All",
        "Mail.Read",
        "People.Read.All",
        "OnlineMeetingTranscript.Read.All",
        "Chat.Read",
        "ChannelMessage.Read.All",
        "ExternalItem.Read.All"
    ];

    public bool UseInteractiveSignIn { get; init; } = true;

    public bool DisableWebGrounding { get; init; }
}
