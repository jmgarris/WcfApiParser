using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.Services;

public sealed class GeneratorWorkflowService : IGeneratorWorkflowService
{
    private readonly WcfMetadataReader _metadataReader;
    private readonly ClientLibraryGenerator _clientLibraryGenerator;
    private readonly NuGetPackageBuilder _packageBuilder;

    public GeneratorWorkflowService(
        WcfMetadataReader metadataReader,
        ClientLibraryGenerator clientLibraryGenerator,
        NuGetPackageBuilder packageBuilder)
    {
        _metadataReader = metadataReader;
        _clientLibraryGenerator = clientLibraryGenerator;
        _packageBuilder = packageBuilder;
    }

    public Task<MetadataReadResult> AnalyzeAsync(WcfMetadataDiscoveryOptions options, CancellationToken cancellationToken)
        => _metadataReader.ReadAsync(options, cancellationToken);

    public Task<GenerationResult> GenerateAsync(ClientLibraryGenerationOptions options, CancellationToken cancellationToken)
        => _clientLibraryGenerator.GenerateAsync(options, cancellationToken);

    public Task<GenerationResult> PackageAsync(string projectFilePath, CancellationToken cancellationToken)
        => _packageBuilder.BuildAsync(projectFilePath, cancellationToken);
}
