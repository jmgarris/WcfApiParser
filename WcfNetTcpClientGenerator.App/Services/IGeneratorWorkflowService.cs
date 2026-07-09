using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.Services;

public interface IGeneratorWorkflowService
{
    Task<MetadataReadResult> AnalyzeAsync(WcfMetadataDiscoveryOptions options, CancellationToken cancellationToken);

    Task<GenerationResult> GenerateAsync(ClientLibraryGenerationOptions options, CancellationToken cancellationToken);

    Task<GenerationResult> PackageAsync(string projectFilePath, CancellationToken cancellationToken);

    Task<DotNetSvcUtilPreflightResult> TestDotNetSvcUtilAsync(WcfMetadataDiscoveryOptions options, CancellationToken cancellationToken);

    Task<CopilotConnectionTestResult> SignInToCopilotAsync(CopilotChatOptions options, CancellationToken cancellationToken);

    Task<CopilotConnectionTestResult> SignOutOfCopilotAsync(CancellationToken cancellationToken);

    Task<CopilotConnectionTestResult> TestCopilotConnectionAsync(CopilotChatOptions options, CancellationToken cancellationToken);

    Task<OpenAiConnectionTestResult> TestOpenAiConnectionAsync(OpenAiDocumentationOptions options, CancellationToken cancellationToken);

    void ClearOpenAiDocumentationCache();
}
