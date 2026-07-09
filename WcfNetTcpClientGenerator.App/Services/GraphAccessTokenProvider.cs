using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.Services;

public sealed class GraphAccessTokenProvider : IGraphAccessTokenProvider
{
    private readonly ICopilotAuthenticationService _authenticationService;

    public GraphAccessTokenProvider(ICopilotAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    public Task<GraphAccessTokenResult> GetAccessTokenAsync(CopilotChatOptions options, CancellationToken cancellationToken)
        => _authenticationService.GetAccessTokenAsync(options, cancellationToken);
}
