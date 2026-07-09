using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.Services;

public interface ICopilotAuthenticationService
{
    Task<CopilotConnectionTestResult> SignInAsync(CopilotChatOptions options, CancellationToken cancellationToken);

    Task<CopilotConnectionTestResult> SignOutAsync(CancellationToken cancellationToken);

    Task<GraphAccessTokenResult> GetAccessTokenAsync(CopilotChatOptions options, CancellationToken cancellationToken);
}
