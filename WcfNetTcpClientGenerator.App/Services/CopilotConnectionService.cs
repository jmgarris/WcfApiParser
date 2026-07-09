using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.Services;

public sealed class CopilotConnectionService : ICopilotConnectionService
{
    private readonly ICopilotAuthenticationService _authenticationService;
    private readonly CopilotChatClient _chatClient;

    public CopilotConnectionService(ICopilotAuthenticationService authenticationService, CopilotChatClient chatClient)
    {
        _authenticationService = authenticationService;
        _chatClient = chatClient;
    }

    public async Task<CopilotConnectionTestResult> TestConnectionAsync(CopilotChatOptions options, CancellationToken cancellationToken)
    {
        var tokenResult = await _authenticationService.GetAccessTokenAsync(options, cancellationToken).ConfigureAwait(false);
        if (!tokenResult.Success)
        {
            return new CopilotConnectionTestResult
            {
                Success = false,
                IsSignedIn = false,
                StatusText = "Copilot connection test failed during authentication.",
                Diagnostics = tokenResult.Diagnostics
            };
        }

        var conversationResult = await _chatClient.CreateConversationAsync(tokenResult.AccessToken, options, cancellationToken).ConfigureAwait(false);
        if (!conversationResult.Success || string.IsNullOrWhiteSpace(conversationResult.ConversationId))
        {
            return new CopilotConnectionTestResult
            {
                Success = false,
                IsSignedIn = true,
                AccountName = tokenResult.AccountName,
                ApiAvailable = false,
                StatusText = "Copilot conversation creation failed. Tenant consent, licensing, or API availability may be missing.",
                Diagnostics = conversationResult.Diagnostics
            };
        }

        var chatResult = await _chatClient.SendChatAsync(
            conversationResult.ConversationId,
            "Return only valid C# XML documentation comments with a summary that says Connection test succeeded.",
            tokenResult.AccessToken,
            options,
            cancellationToken).ConfigureAwait(false);

        var success = chatResult.Success;
        return new CopilotConnectionTestResult
        {
            Success = success,
            IsSignedIn = true,
            AccountName = tokenResult.AccountName,
            ApiAvailable = success,
            StatusText = success
                ? $"Copilot API is available for {tokenResult.AccountName}."
                : "Copilot conversation succeeded but chat did not return a usable response.",
            Diagnostics = chatResult.Diagnostics
        };
    }
}
