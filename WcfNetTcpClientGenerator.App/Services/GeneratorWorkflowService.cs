using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.Services;

public sealed class GeneratorWorkflowService : IGeneratorWorkflowService
{
    private readonly WcfMetadataReader _metadataReader;
    private readonly ClientLibraryGenerator _clientLibraryGenerator;
    private readonly NuGetPackageBuilder _packageBuilder;
    private readonly DotNetSvcUtilRunner _dotNetSvcUtilRunner;
    private readonly NullMethodDocumentationProvider _nullDocumentationProvider;
    private readonly CopilotMethodDocumentationProvider _copilotMethodDocumentationProvider;
    private readonly OpenAiMethodDocumentationProvider _openAiMethodDocumentationProvider;
    private readonly ICopilotAuthenticationService _copilotAuthenticationService;
    private readonly ICopilotConnectionService _copilotConnectionService;
    private readonly OpenAiConnectionTester _openAiConnectionTester;
    private readonly MethodDocumentationCache _methodDocumentationCache;

    public GeneratorWorkflowService(
        WcfMetadataReader metadataReader,
        ClientLibraryGenerator clientLibraryGenerator,
        NuGetPackageBuilder packageBuilder,
        DotNetSvcUtilRunner dotNetSvcUtilRunner,
        NullMethodDocumentationProvider nullDocumentationProvider,
        CopilotMethodDocumentationProvider copilotMethodDocumentationProvider,
        OpenAiMethodDocumentationProvider openAiMethodDocumentationProvider,
        ICopilotAuthenticationService copilotAuthenticationService,
        ICopilotConnectionService copilotConnectionService,
        OpenAiConnectionTester openAiConnectionTester,
        MethodDocumentationCache methodDocumentationCache)
    {
        _metadataReader = metadataReader;
        _clientLibraryGenerator = clientLibraryGenerator;
        _packageBuilder = packageBuilder;
        _dotNetSvcUtilRunner = dotNetSvcUtilRunner;
        _nullDocumentationProvider = nullDocumentationProvider;
        _copilotMethodDocumentationProvider = copilotMethodDocumentationProvider;
        _openAiMethodDocumentationProvider = openAiMethodDocumentationProvider;
        _copilotAuthenticationService = copilotAuthenticationService;
        _copilotConnectionService = copilotConnectionService;
        _openAiConnectionTester = openAiConnectionTester;
        _methodDocumentationCache = methodDocumentationCache;
    }

    public Task<MetadataReadResult> AnalyzeAsync(WcfMetadataDiscoveryOptions options, CancellationToken cancellationToken)
        => _metadataReader.ReadAsync(options, cancellationToken);

    public Task<GenerationResult> GenerateAsync(ClientLibraryGenerationOptions options, CancellationToken cancellationToken)
    {
        IMethodDocumentationProvider selectedDocumentationProvider = options.DocumentationOptions.ProviderKind switch
        {
            DocumentationProviderKind.Microsoft365Copilot => _copilotMethodDocumentationProvider,
            DocumentationProviderKind.OpenAI => _openAiMethodDocumentationProvider,
            _ => _nullDocumentationProvider
        };

        var effectiveOptions = new ClientLibraryGenerationOptions
        {
            OutputKind = options.OutputKind,
            EnableSwagger = options.EnableSwagger,
            DiscoveryOptions = options.DiscoveryOptions,
            GeneratedLibraryName = options.GeneratedLibraryName,
            PackageId = options.PackageId,
            PackageVersion = options.PackageVersion,
            Authors = options.Authors,
            Company = options.Company,
            Description = options.Description,
            RepositoryUrl = options.RepositoryUrl,
            OutputFolder = options.OutputFolder,
            SecurityMode = options.SecurityMode,
            TcpClientCredentialType = options.TcpClientCredentialType,
            TcpTransportClientCredentialType = options.TcpTransportClientCredentialType,
            MessageClientCredentialType = options.MessageClientCredentialType,
            ClientCertificateSource = options.ClientCertificateSource,
            ClientCertificateStoreLocation = options.ClientCertificateStoreLocation,
            ClientCertificateStoreName = options.ClientCertificateStoreName,
            ClientCertificateFindType = options.ClientCertificateFindType,
            ClientCertificateFindValue = options.ClientCertificateFindValue,
            ClientCertificateFilePath = options.ClientCertificateFilePath,
            ClientCertificateFilePasswordSource = options.ClientCertificateFilePasswordSource,
            ClientCertificateFilePasswordEnvironmentVariableName = options.ClientCertificateFilePasswordEnvironmentVariableName,
            ClientCertificateFilePasswordAppSettingName = options.ClientCertificateFilePasswordAppSettingName,
            ReliableSessionEnabled = options.ReliableSessionEnabled,
            OpenTimeout = options.OpenTimeout,
            CloseTimeout = options.CloseTimeout,
            SendTimeout = options.SendTimeout,
            ReceiveTimeout = options.ReceiveTimeout,
            MaxReceivedMessageSize = options.MaxReceivedMessageSize,
            Username = options.Username,
            Password = options.Password,
            ExistingProxyCode = options.ExistingProxyCode,
            DocumentationOptions = options.DocumentationOptions,
            MethodDocumentationProvider = selectedDocumentationProvider,
            ProgressReporter = options.ProgressReporter
        };

        return _clientLibraryGenerator.GenerateAsync(effectiveOptions, cancellationToken);
    }

    public Task<GenerationResult> PackageAsync(string projectFilePath, CancellationToken cancellationToken)
        => _packageBuilder.BuildAsync(projectFilePath, cancellationToken);

    public Task<DotNetSvcUtilPreflightResult> TestDotNetSvcUtilAsync(WcfMetadataDiscoveryOptions options, CancellationToken cancellationToken)
        => _dotNetSvcUtilRunner.CheckAvailabilityAsync(options.DotNetSvcUtilPath, cancellationToken);

    public Task<CopilotConnectionTestResult> SignInToCopilotAsync(CopilotChatOptions options, CancellationToken cancellationToken)
        => _copilotAuthenticationService.SignInAsync(options, cancellationToken);

    public Task<CopilotConnectionTestResult> SignOutOfCopilotAsync(CancellationToken cancellationToken)
        => _copilotAuthenticationService.SignOutAsync(cancellationToken);

    public Task<CopilotConnectionTestResult> TestCopilotConnectionAsync(CopilotChatOptions options, CancellationToken cancellationToken)
        => _copilotConnectionService.TestConnectionAsync(options, cancellationToken);

    public Task<OpenAiConnectionTestResult> TestOpenAiConnectionAsync(OpenAiDocumentationOptions options, CancellationToken cancellationToken)
        => _openAiConnectionTester.TestConnectionAsync(options, cancellationToken);

    public void ClearOpenAiDocumentationCache()
        => _methodDocumentationCache.ClearByPrefix("OpenAI|");
}
