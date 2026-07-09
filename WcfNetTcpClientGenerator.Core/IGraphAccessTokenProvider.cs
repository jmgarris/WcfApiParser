namespace WcfNetTcpClientGenerator.Core;

public interface IGraphAccessTokenProvider
{
    Task<GraphAccessTokenResult> GetAccessTokenAsync(CopilotChatOptions options, CancellationToken cancellationToken);
}
