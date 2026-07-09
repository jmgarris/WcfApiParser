namespace WcfNetTcpClientGenerator.Core;

public sealed class CopilotConversationManager
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CopilotChatClient _chatClient;
    private readonly IGraphAccessTokenProvider _tokenProvider;
    private string _conversationId = string.Empty;
    private string _conversationKey = string.Empty;

    public CopilotConversationManager(CopilotChatClient chatClient, IGraphAccessTokenProvider tokenProvider)
    {
        _chatClient = chatClient;
        _tokenProvider = tokenProvider;
    }

    public async Task<CopilotChatResult> EnsureConversationAsync(CopilotChatOptions options, CancellationToken cancellationToken)
    {
        var key = BuildKey(options);
        if (!string.IsNullOrWhiteSpace(_conversationId) && string.Equals(_conversationKey, key, StringComparison.Ordinal))
        {
            return new CopilotChatResult
            {
                Success = true,
                ConversationId = _conversationId,
                Diagnostics =
                [
                    new CopilotChatDiagnostic
                    {
                        Severity = "Info",
                        Code = "COPILOT_CONVERSATION_REUSED",
                        Message = "Reusing existing Copilot conversation."
                    }
                ]
            };
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(_conversationId) && string.Equals(_conversationKey, key, StringComparison.Ordinal))
            {
                return new CopilotChatResult
                {
                    Success = true,
                    ConversationId = _conversationId
                };
            }

            var tokenResult = await _tokenProvider.GetAccessTokenAsync(options, cancellationToken).ConfigureAwait(false);
            if (!tokenResult.Success)
            {
                return new CopilotChatResult
                {
                    Success = false,
                    Diagnostics = tokenResult.Diagnostics
                };
            }

            var result = await _chatClient.CreateConversationAsync(tokenResult.AccessToken, options, cancellationToken).ConfigureAwait(false);
            if (result.Success && !string.IsNullOrWhiteSpace(result.ConversationId))
            {
                _conversationId = result.ConversationId;
                _conversationKey = key;
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string BuildKey(CopilotChatOptions options)
        => $"{options.TenantId}|{options.ClientId}|{string.Join("|", options.RequiredScopes)}|{options.DisableWebGrounding}";
}
