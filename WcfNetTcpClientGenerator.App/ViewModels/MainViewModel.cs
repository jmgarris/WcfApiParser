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

    public IAsyncRelayCommand AnalyzeServiceMetadataCommand { get; }

    public IAsyncRelayCommand GenerateClassLibraryCommand { get; }

    public IAsyncRelayCommand PackageClassLibraryCommand { get; }

    public IAsyncRelayCommand BrowseOutputFolderCommand { get; }

    public IAsyncRelayCommand BrowseDotNetSvcUtilPathCommand { get; }

    public IAsyncRelayCommand TestDotNetSvcUtilCommand { get; }

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
        BusyMessage = "Generating class library...";
        ProgressPercentage = 50;
        AddStatus("Info", "Generating client library.");

        var result = await _workflowService.GenerateAsync(BuildGenerationOptions(), CancellationToken.None);
        PopulateFromGenerationResult(result);

        ProgressPercentage = 0;
        BusyMessage = string.Empty;
    }

    private async Task PackageClassLibraryAsync()
    {
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
            DiscoveryOptions = BuildDiscoveryOptions(),
            GeneratedLibraryName = GeneratedLibraryName,
            PackageId = PackageId,
            PackageVersion = PackageVersion,
            OutputFolder = OutputFolder,
            SecurityMode = SecurityMode,
            TcpClientCredentialType = TcpClientCredentialType,
            ReliableSessionEnabled = ReliableSessionEnabled,
            OpenTimeout = OpenTimeout,
            SendTimeout = SendTimeout,
            ReceiveTimeout = ReceiveTimeout,
            MaxReceivedMessageSize = MaxReceivedMessageSize,
            Username = Username,
            Password = Password
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
}
