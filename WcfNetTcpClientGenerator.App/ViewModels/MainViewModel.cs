using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using WcfNetTcpClientGenerator.App.Models;
using WcfNetTcpClientGenerator.App.Services;
using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFolderPickerService _folderPickerService;
    private readonly IFilePickerService _filePickerService;
    private readonly IGeneratorWorkflowService _workflowService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly object _statusMessagesGate = new();

    private string? _generatedProjectFilePath;
    private bool _regenerateCommentsRequested;

    public MainViewModel(
        IFolderPickerService folderPickerService,
        IFilePickerService filePickerService,
        IGeneratorWorkflowService workflowService)
    {
        _folderPickerService = folderPickerService;
        _filePickerService = filePickerService;
        _workflowService = workflowService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        AnalyzeServiceMetadataCommand = new AsyncRelayCommand(AnalyzeServiceMetadataAsync);
        GenerateClassLibraryCommand = new AsyncRelayCommand(GenerateClassLibraryAsync);
        PackageClassLibraryCommand = new AsyncRelayCommand(PackageClassLibraryAsync);
        BrowseOutputFolderCommand = new AsyncRelayCommand(BrowseOutputFolderAsync);
        BrowseDotNetSvcUtilPathCommand = new AsyncRelayCommand(BrowseDotNetSvcUtilPathAsync);
        TestDotNetSvcUtilCommand = new AsyncRelayCommand(TestDotNetSvcUtilAsync);
        SignInToCopilotCommand = new AsyncRelayCommand(SignInToCopilotAsync);
        SignOutOfCopilotCommand = new AsyncRelayCommand(SignOutOfCopilotAsync);
        TestCopilotConnectionCommand = new AsyncRelayCommand(TestCopilotConnectionAsync);
        TestOpenAiConnectionCommand = new AsyncRelayCommand(TestOpenAiConnectionAsync);
        ClearOpenAiCachedCommentsCommand = new RelayCommand(ClearOpenAiCachedComments);
        RegenerateCommentsCommand = new RelayCommand(RequestCommentRegeneration);
        ClearStatusCommand = new RelayCommand(ClearStatus);
    }

    public ObservableCollection<OperationRow> Operations { get; } = [];

    public ObservableCollection<StatusMessage> StatusMessages { get; } = [];

    public IReadOnlyList<string> SecurityModes { get; } =
    [
        "None",
        "Transport",
        "Message",
        "TransportWithMessageCredential"
    ];

    public IReadOnlyList<string> TcpCredentialTypes { get; } =
    [
        "None",
        "Windows",
        "Certificate",
        "UserName"
    ];

    public IReadOnlyList<SelectionOption<GeneratedOutputKind>> GeneratedOutputKinds { get; } =
    [
        new SelectionOption<GeneratedOutputKind> { Value = GeneratedOutputKind.NetTcpClientLibrary, Label = "WCF client library (.NET 10)" },
        new SelectionOption<GeneratedOutputKind> { Value = GeneratedOutputKind.NetFramework48RestApiWrapper, Label = "REST API wrapper for WCF net.tcp (.NET Framework 4.8)" }
    ];

    public IReadOnlyList<SelectionOption<DocumentationProviderKind>> DocumentationProviders { get; } =
    [
        new SelectionOption<DocumentationProviderKind> { Value = DocumentationProviderKind.LocalFallback, Label = "Local fallback only" },
        new SelectionOption<DocumentationProviderKind> { Value = DocumentationProviderKind.Microsoft365Copilot, Label = "Microsoft 365 Copilot" },
        new SelectionOption<DocumentationProviderKind> { Value = DocumentationProviderKind.OpenAI, Label = "OpenAI" }
    ];

    public IReadOnlyList<SelectionOption<OpenAiApiKeySource>> OpenAiApiKeySources { get; } =
    [
        new SelectionOption<OpenAiApiKeySource> { Value = OpenAiApiKeySource.EnvironmentVariable, Label = "Environment variable" },
        new SelectionOption<OpenAiApiKeySource> { Value = OpenAiApiKeySource.UserEnteredKey, Label = "User-entered key" }
    ];

    public IReadOnlyList<string> OpenAiReasoningEfforts { get; } =
    [
        "none",
        "low",
        "medium",
        "high",
        "xhigh",
        "max"
    ];

    public IAsyncRelayCommand AnalyzeServiceMetadataCommand { get; }

    public IAsyncRelayCommand GenerateClassLibraryCommand { get; }

    public IAsyncRelayCommand PackageClassLibraryCommand { get; }

    public IAsyncRelayCommand BrowseOutputFolderCommand { get; }

    public IAsyncRelayCommand BrowseDotNetSvcUtilPathCommand { get; }

    public IAsyncRelayCommand TestDotNetSvcUtilCommand { get; }

    public IAsyncRelayCommand SignInToCopilotCommand { get; }

    public IAsyncRelayCommand SignOutOfCopilotCommand { get; }

    public IAsyncRelayCommand TestCopilotConnectionCommand { get; }

    public IAsyncRelayCommand TestOpenAiConnectionCommand { get; }

    public IRelayCommand ClearOpenAiCachedCommentsCommand { get; }

    public IRelayCommand RegenerateCommentsCommand { get; }

    public IRelayCommand ClearStatusCommand { get; }

    [ObservableProperty]
    private string serviceEndpointUrl = string.Empty;

    [ObservableProperty]
    private string metadataEndpointUrl = string.Empty;

    [ObservableProperty]
    private string wsdlFilePath = string.Empty;

    [ObservableProperty]
    private string metadataFolderPath = string.Empty;

    [ObservableProperty]
    private string dotNetSvcUtilPath = string.Empty;

    [ObservableProperty]
    private string serviceNamespace = "Generated.Wcf";

    [ObservableProperty]
    private string generatedLibraryName = "GeneratedNetTcpClient";

    [ObservableProperty]
    private GeneratedOutputKind selectedGeneratedOutputKind = GeneratedOutputKind.NetTcpClientLibrary;

    [ObservableProperty]
    private bool enableSwagger = true;

    public bool IsRestApiWrapperSelected => SelectedGeneratedOutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper;

    public bool IsClientLibrarySelected => !IsRestApiWrapperSelected;

    public string GenerateOutputButtonText => IsRestApiWrapperSelected ? "Generate REST API Wrapper" : "Generate Class Library";

    partial void OnSelectedGeneratedOutputKindChanged(GeneratedOutputKind value)
    {
        OnPropertyChanged(nameof(IsRestApiWrapperSelected));
        OnPropertyChanged(nameof(IsClientLibrarySelected));
        OnPropertyChanged(nameof(GenerateOutputButtonText));
        if (value == GeneratedOutputKind.NetFramework48RestApiWrapper) EnableSwagger = true;
    }

    [ObservableProperty]
    private string packageId = "GeneratedNetTcpClient";

    [ObservableProperty]
    private string packageVersion = "1.0.0";

    [ObservableProperty]
    private string outputFolder = string.Empty;

    [ObservableProperty]
    private string securityMode = "Transport";

    [ObservableProperty]
    private string tcpClientCredentialType = "Windows";

    [ObservableProperty]
    private string tcpTransportClientCredentialType = "Windows";

    [ObservableProperty]
    private string messageClientCredentialType = "None";

    [ObservableProperty]
    private bool reliableSessionEnabled;

    [ObservableProperty]
    private string openTimeout = "00:00:30";

    [ObservableProperty]
    private string sendTimeout = "00:01:40";

    [ObservableProperty]
    private string receiveTimeout = "00:01:40";

    [ObservableProperty]
    private string maxReceivedMessageSize = "65536";

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string busyMessage = string.Empty;

    [ObservableProperty]
    private int progressPercentage;

    [ObservableProperty]
    private DocumentationProviderKind selectedDocumentationProvider = DocumentationProviderKind.LocalFallback;

    [ObservableProperty]
    private string copilotTenantId = string.Empty;

    [ObservableProperty]
    private string copilotClientId = string.Empty;

    [ObservableProperty]
    private string copilotRequiredScopes = "Sites.Read.All, Mail.Read, People.Read.All, OnlineMeetingTranscript.Read.All, Chat.Read, ChannelMessage.Read.All, ExternalItem.Read.All";

    [ObservableProperty]
    private bool copilotUseInteractiveSignIn = true;

    [ObservableProperty]
    private bool copilotDisableWebGrounding;

    [ObservableProperty]
    private bool cacheGeneratedComments = true;

    [ObservableProperty]
    private string copilotMaxCommentLength = "600";

    [ObservableProperty]
    private string copilotTimeoutSeconds = "30";

    [ObservableProperty]
    private string copilotRetryCount = "2";

    [ObservableProperty]
    private string copilotStatusText = "Copilot comments are disabled.";

    [ObservableProperty]
    private string signedInAccount = string.Empty;

    [ObservableProperty]
    private OpenAiApiKeySource selectedOpenAiApiKeySource = OpenAiApiKeySource.EnvironmentVariable;

    [ObservableProperty]
    private string openAiApiKeyEnvironmentVariableName = "OPENAI_API_KEY";

    [ObservableProperty]
    private string openAiApiKey = string.Empty;

    [ObservableProperty]
    private string openAiModelName = "gpt-5.6-luna";

    [ObservableProperty]
    private string openAiMaxOutputTokens = "600";

    [ObservableProperty]
    private string openAiReasoningEffort = "none";

    [ObservableProperty]
    private string openAiTemperature = "0.2";

    [ObservableProperty]
    private string openAiTimeoutSeconds = "30";

    [ObservableProperty]
    private string openAiRetryCount = "2";

    [ObservableProperty]
    private string openAiStatusText = "OpenAI comments are disabled.";

    public bool EnableCopilotComments
    {
        get => SelectedDocumentationProvider == DocumentationProviderKind.Microsoft365Copilot;
        set
        {
            if (value)
            {
                SelectedDocumentationProvider = DocumentationProviderKind.Microsoft365Copilot;
            }
            else if (SelectedDocumentationProvider == DocumentationProviderKind.Microsoft365Copilot)
            {
                SelectedDocumentationProvider = DocumentationProviderKind.LocalFallback;
            }
        }
    }

    public bool EnableOpenAiComments
    {
        get => SelectedDocumentationProvider == DocumentationProviderKind.OpenAI;
        set
        {
            if (value)
            {
                SelectedDocumentationProvider = DocumentationProviderKind.OpenAI;
            }
            else if (SelectedDocumentationProvider == DocumentationProviderKind.OpenAI)
            {
                SelectedDocumentationProvider = DocumentationProviderKind.LocalFallback;
            }
        }
    }

    public bool IsCopilotProviderSelected => SelectedDocumentationProvider == DocumentationProviderKind.Microsoft365Copilot;

    public bool IsOpenAiProviderSelected => SelectedDocumentationProvider == DocumentationProviderKind.OpenAI;

    public bool IsUserEnteredOpenAiKeySelected => SelectedOpenAiApiKeySource == OpenAiApiKeySource.UserEnteredKey;

    private async Task BrowseOutputFolderAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputFolder = folder;
        }
    }

    private async Task BrowseDotNetSvcUtilPathAsync()
    {
        var filePath = await _filePickerService.PickExecutableAsync();
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            DotNetSvcUtilPath = filePath;
        }
    }

    private async Task AnalyzeServiceMetadataAsync()
    {
        BusyMessage = "Analyzing metadata...";
        ProgressPercentage = 10;
        ClearState(clearMessages: false);
        AddStatus("Info", "Starting metadata analysis.");

        var result = await _workflowService.AnalyzeAsync(BuildDiscoveryOptions(), CancellationToken.None);
        PopulateFromMetadataResult(result);

        ProgressPercentage = 0;
        BusyMessage = string.Empty;
    }

    private async Task TestDotNetSvcUtilAsync()
    {
        BusyMessage = "Testing dotnet-svcutil...";
        ProgressPercentage = 20;
        AddStatus("Info", "Running dotnet-svcutil preflight check.");

        var result = await _workflowService.TestDotNetSvcUtilAsync(BuildDiscoveryOptions(), CancellationToken.None);

        AddStatus(result.ToolFound ? "Info" : "Error", $"Mode: {result.ToolExecutionMode}");

        if (!string.IsNullOrWhiteSpace(result.ToolPath))
        {
            AddStatus("Info", $"Tool path: {result.ToolPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.WorkingDirectory))
        {
            AddStatus("Info", $"Working directory: {result.WorkingDirectory}");
        }

        if (!string.IsNullOrWhiteSpace(result.VersionOutput))
        {
            AddStatus("Info", result.VersionOutput.Trim());
        }

        if (!string.IsNullOrWhiteSpace(result.DiagnosticMessage))
        {
            AddStatus(result.ToolFound ? "Info" : "Error", result.DiagnosticMessage);
        }

        ProgressPercentage = 0;
        BusyMessage = string.Empty;
    }

    private async Task GenerateClassLibraryAsync()
    {
        BusyMessage = IsRestApiWrapperSelected ? "Generating .NET Framework 4.8 REST API wrapper..." : "Generating class library...";
        ProgressPercentage = 50;
        AddStatus("Info", IsRestApiWrapperSelected ? "Generating .NET Framework 4.8 REST API wrapper..." : "Generating client library.");
        AnnounceDocumentationProvider();

        var result = await _workflowService.GenerateAsync(BuildGenerationOptions(), CancellationToken.None);
        PopulateFromGenerationResult(result);
        _regenerateCommentsRequested = false;

        ProgressPercentage = 0;
        BusyMessage = string.Empty;
    }

    private async Task PackageClassLibraryAsync()
    {
        if (IsRestApiWrapperSelected)
        {
            AddStatus("Warning", "REST API wrapper projects are built for IIS deployment and are not packaged as NuGet.");
            return;
        }
        BusyMessage = "Packaging NuGet package...";
        ProgressPercentage = 75;

        if (string.IsNullOrWhiteSpace(_generatedProjectFilePath))
        {
            AddStatus("Warning", "Generate the client library before packaging.");
            ProgressPercentage = 0;
            BusyMessage = string.Empty;
            return;
        }

        var result = await _workflowService.PackageAsync(_generatedProjectFilePath, CancellationToken.None);
        PopulateFromGenerationResult(result);

        ProgressPercentage = 0;
        BusyMessage = string.Empty;
    }

    private void ClearStatus()
    {
        BusyMessage = string.Empty;
        ProgressPercentage = 0;
        UpdateStatusMessages(static statusMessages => statusMessages.Clear());
    }

    private async Task SignInToCopilotAsync()
    {
        BusyMessage = "Signing in to Microsoft 365...";
        ProgressPercentage = 15;
        AddStatus("Info", "Starting Microsoft 365 sign-in.");

        var result = await _workflowService.SignInToCopilotAsync(BuildCopilotChatOptions(), CancellationToken.None);
        ApplyCopilotConnectionResult(result);

        ProgressPercentage = 0;
        BusyMessage = string.Empty;
    }

    private async Task SignOutOfCopilotAsync()
    {
        BusyMessage = "Signing out of Microsoft 365...";
        ProgressPercentage = 10;

        var result = await _workflowService.SignOutOfCopilotAsync(CancellationToken.None);
        ApplyCopilotConnectionResult(result);

        ProgressPercentage = 0;
        BusyMessage = string.Empty;
    }

    private async Task TestCopilotConnectionAsync()
    {
        BusyMessage = "Testing Copilot connection...";
        ProgressPercentage = 20;
        AddStatus("Info", "Testing Microsoft 365 Copilot connectivity.");

        var result = await _workflowService.TestCopilotConnectionAsync(BuildCopilotChatOptions(), CancellationToken.None);
        ApplyCopilotConnectionResult(result);

        ProgressPercentage = 0;
        BusyMessage = string.Empty;
    }

    private void RequestCommentRegeneration()
    {
        _regenerateCommentsRequested = true;
        AddStatus("Info", "The next generation run will bypass the documentation cache and regenerate AI documentation comments.");
    }

    private async Task TestOpenAiConnectionAsync()
    {
        BusyMessage = "Testing OpenAI connection...";
        ProgressPercentage = 20;
        AddStatus("Info", "Testing OpenAI connectivity.");

        var result = await _workflowService.TestOpenAiConnectionAsync(BuildOpenAiDocumentationOptions(), CancellationToken.None);
        ApplyOpenAiConnectionResult(result);

        ProgressPercentage = 0;
        BusyMessage = string.Empty;
    }

    private void ClearOpenAiCachedComments()
    {
        _workflowService.ClearOpenAiDocumentationCache();
        AddStatus("Info", "Cleared cached OpenAI documentation comments.");
    }

    private WcfMetadataDiscoveryOptions BuildDiscoveryOptions()
        => new()
        {
            ServiceEndpointUrl = ServiceEndpointUrl,
            MetadataEndpointUrl = MetadataEndpointUrl,
            WsdlFilePath = WsdlFilePath,
            MetadataFolderPath = MetadataFolderPath,
            DotNetSvcUtilPath = DotNetSvcUtilPath,
            ServiceNamespace = ServiceNamespace
        };

    private ClientLibraryGenerationOptions BuildGenerationOptions()
        => new()
        {
            OutputKind = SelectedGeneratedOutputKind,
            EnableSwagger = EnableSwagger,
            DiscoveryOptions = BuildDiscoveryOptions(),
            GeneratedLibraryName = GeneratedLibraryName,
            PackageId = PackageId,
            PackageVersion = PackageVersion,
            OutputFolder = OutputFolder,
            SecurityMode = SecurityMode,
            TcpClientCredentialType = TcpClientCredentialType,
            TcpTransportClientCredentialType = TcpTransportClientCredentialType,
            MessageClientCredentialType = MessageClientCredentialType,
            ReliableSessionEnabled = ReliableSessionEnabled,
            OpenTimeout = OpenTimeout,
            SendTimeout = SendTimeout,
            ReceiveTimeout = ReceiveTimeout,
            MaxReceivedMessageSize = MaxReceivedMessageSize,
            Username = Username,
            Password = Password,
            DocumentationOptions = BuildMethodDocumentationOptions(),
            ProgressReporter = new Progress<GenerationDiagnostic>(diagnostic => AddStatus(diagnostic.Severity.ToString(), diagnostic.Message))
        };

    private MethodDocumentationOptions BuildMethodDocumentationOptions()
        => new()
        {
            ProviderKind = SelectedDocumentationProvider,
            CopilotChat = BuildCopilotChatOptions(),
            OpenAi = BuildOpenAiDocumentationOptions(),
            CacheGeneratedComments = CacheGeneratedComments,
            RegenerateComments = _regenerateCommentsRequested,
            MaxCommentLength = ParsePositiveInt(CopilotMaxCommentLength, 600),
            Timeout = TimeSpan.FromSeconds(SelectedDocumentationProvider == DocumentationProviderKind.OpenAI
                ? ParsePositiveInt(OpenAiTimeoutSeconds, 30)
                : ParsePositiveInt(CopilotTimeoutSeconds, 30)),
            RetryCount = SelectedDocumentationProvider == DocumentationProviderKind.OpenAI
                ? ParseNonNegativeInt(OpenAiRetryCount, 2)
                : ParseNonNegativeInt(CopilotRetryCount, 2)
        };

    private CopilotChatOptions BuildCopilotChatOptions()
        => new()
        {
            TenantId = CopilotTenantId,
            ClientId = CopilotClientId,
            RequiredScopes = CopilotRequiredScopes
                .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            UseInteractiveSignIn = CopilotUseInteractiveSignIn,
            DisableWebGrounding = CopilotDisableWebGrounding
        };

    private OpenAiDocumentationOptions BuildOpenAiDocumentationOptions()
        => new()
        {
            ApiKeySource = SelectedOpenAiApiKeySource,
            ApiKeyEnvironmentVariableName = OpenAiApiKeyEnvironmentVariableName,
            UserEnteredApiKey = OpenAiApiKey,
            ModelName = OpenAiModelName,
            MaxOutputTokens = ParsePositiveInt(OpenAiMaxOutputTokens, 600),
            ReasoningEffort = OpenAiModelCapabilities.NormalizeReasoningEffort(OpenAiReasoningEffort),
            Temperature = ParseDouble(OpenAiTemperature, 0.2d)
        };

    private void PopulateFromMetadataResult(MetadataReadResult result)
    {
        Operations.Clear();

        foreach (var diagnostic in result.Diagnostics)
        {
            AddStatus(diagnostic.Severity.ToString(), diagnostic.Message);
        }

        if (result.Metadata is null)
        {
            return;
        }

        foreach (var contract in result.Metadata.Contracts)
        {
            foreach (var operation in contract.Operations)
            {
                var signature = $"{operation.ResponseTypeName} {operation.MethodName}({string.Join(", ", operation.Parameters.Select(static parameter => $"{parameter.TypeName} {parameter.Name}"))})";
                Operations.Add(new OperationRow
                {
                    ContractName = contract.ContractName,
                    OperationName = operation.OperationName,
                    Signature = signature
                });
            }
        }
    }

    private void PopulateFromGenerationResult(GenerationResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            AddStatus(diagnostic.Severity.ToString(), diagnostic.Message);
        }

        if (!string.IsNullOrWhiteSpace(result.ProjectFilePath))
        {
            _generatedProjectFilePath = result.ProjectFilePath;
        }

        if (!string.IsNullOrWhiteSpace(result.PackagePath))
        {
            AddStatus("Info", $"NuGet package created at {result.PackagePath}");
        }
    }

    private void ApplyCopilotConnectionResult(CopilotConnectionTestResult result)
    {
        CopilotStatusText = result.StatusText;
        SignedInAccount = result.AccountName;

        foreach (var diagnostic in result.Diagnostics)
        {
            AddStatus(diagnostic.Severity, diagnostic.Message);
        }

        if (string.IsNullOrWhiteSpace(result.AccountName) && result.Success && result.StatusText.Contains("Signed out", StringComparison.OrdinalIgnoreCase))
        {
            SignedInAccount = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(result.StatusText))
        {
            AddStatus(result.Success ? "Info" : "Warning", result.StatusText);
        }
    }

    private void ApplyOpenAiConnectionResult(OpenAiConnectionTestResult result)
    {
        OpenAiStatusText = result.StatusText;

        foreach (var diagnostic in result.Diagnostics)
        {
            AddStatus(diagnostic.Severity, diagnostic.Message);
        }

        if (!string.IsNullOrWhiteSpace(result.StatusText))
        {
            AddStatus(result.Success ? "Info" : "Warning", result.StatusText);
        }
    }

    private void AddStatus(string severity, string message)
    {
        UpdateStatusMessages(statusMessages =>
        {
            statusMessages.Add(new StatusMessage
            {
                Severity = severity,
                Message = message
            });
        });
    }

    private void ClearState(bool clearMessages)
    {
        Operations.Clear();

        if (clearMessages)
        {
            UpdateStatusMessages(static statusMessages => statusMessages.Clear());
        }
    }

    private void UpdateStatusMessages(Action<ObservableCollection<StatusMessage>> updateAction)
    {
        if (_dispatcherQueue is not null && !_dispatcherQueue.HasThreadAccess)
        {
            var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _dispatcherQueue.TryEnqueue(() =>
            {
                lock (_statusMessagesGate)
                {
                    updateAction(StatusMessages);
                }

                completionSource.SetResult();
            });

            completionSource.Task.GetAwaiter().GetResult();
            return;
        }

        lock (_statusMessagesGate)
        {
            updateAction(StatusMessages);
        }
    }

    private static int ParsePositiveInt(string value, int fallback)
        => int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;

    private static int ParseNonNegativeInt(string value, int fallback)
        => int.TryParse(value, out var parsed) && parsed >= 0
            ? parsed
            : fallback;

    private static double ParseDouble(string value, double fallback)
        => double.TryParse(value, out var parsed)
            ? parsed
            : fallback;

    private void AnnounceDocumentationProvider()
    {
        switch (SelectedDocumentationProvider)
        {
            case DocumentationProviderKind.Microsoft365Copilot:
                AddStatus("Info", "Copilot comments enabled.");
                break;
            case DocumentationProviderKind.OpenAI:
                AddStatus("Info", "OpenAI comments enabled.");
                if (SelectedOpenAiApiKeySource == OpenAiApiKeySource.EnvironmentVariable)
                {
                    AddStatus("Info", "Reading OpenAI API key from environment variable.");
                }

                break;
        }
    }

    partial void OnSelectedDocumentationProviderChanged(DocumentationProviderKind value)
    {
        OnPropertyChanged(nameof(EnableCopilotComments));
        OnPropertyChanged(nameof(EnableOpenAiComments));
        OnPropertyChanged(nameof(IsCopilotProviderSelected));
        OnPropertyChanged(nameof(IsOpenAiProviderSelected));

        CopilotStatusText = value == DocumentationProviderKind.Microsoft365Copilot
            ? "Copilot comments are enabled."
            : "Copilot comments are disabled.";

        OpenAiStatusText = value == DocumentationProviderKind.OpenAI
            ? "OpenAI comments are enabled."
            : "OpenAI comments are disabled.";
    }

    partial void OnSelectedOpenAiApiKeySourceChanged(OpenAiApiKeySource value)
    {
        OnPropertyChanged(nameof(IsUserEnteredOpenAiKeySelected));
    }
}
