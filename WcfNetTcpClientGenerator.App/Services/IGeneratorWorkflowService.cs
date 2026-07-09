using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.Services;

public interface IGeneratorWorkflowService
{
    Task<MetadataReadResult> AnalyzeAsync(WcfMetadataDiscoveryOptions options, CancellationToken cancellationToken);

    Task<GenerationResult> GenerateAsync(ClientLibraryGenerationOptions options, CancellationToken cancellationToken);

    Task<GenerationResult> PackageAsync(string projectFilePath, CancellationToken cancellationToken);
}
