using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.Services;

public interface ICopilotConnectionService
{
    Task<CopilotConnectionTestResult> TestConnectionAsync(CopilotChatOptions options, CancellationToken cancellationToken);
}
