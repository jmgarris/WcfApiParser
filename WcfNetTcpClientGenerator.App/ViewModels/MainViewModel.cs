using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WcfNetTcpClientGenerator.App.Models;
using WcfNetTcpClientGenerator.App.Services;
using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFolderPickerService _folderPickerService;
    private readonly IGeneratorWorkflowService _workflowService;

    private string? _generatedProjectFilePath;

    public MainViewModel(IFolderPickerService folderPickerService, IGeneratorWorkflowService workflowService)
    {
        _folderPickerService = folderPickerService;
        _workflowService = workflowService;

        AnalyzeServiceMetadataCommand = new AsyncRelayCommand(AnalyzeServiceMetadataAsync);
        GenerateClassLibraryCommand = new AsyncRelayCommand(GenerateClassLibraryAsync);
        PackageClassLibraryCommand = new AsyncRelayCommand(PackageClassLibraryAsync);
        BrowseOutputFolderCommand = new AsyncRelayCommand(BrowseOutputFolderAsync);
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

    [ObservableProperty]
    private string serviceEndpointUrl = string.Empty;

    [ObservableProperty]
    private string metadataEndpointUrl = string.Empty;

    [ObservableProperty]
    private string wsdlFilePath = string.Empty;

    [ObservableProperty]
    private string metadataFolderPath = string.Empty;

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

    private async Task BrowseOutputFolderAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputFolder = folder;
        }
    }

    private async Task AnalyzeServiceMetadataAsync()
    {
        BusyMessage = "Analyzing metadata...";
        ClearState(clearMessages: false);
        AddStatus("Info", "Starting metadata analysis.");

        var result = await _workflowService.AnalyzeAsync(BuildDiscoveryOptions(), CancellationToken.None);
        PopulateFromMetadataResult(result);

        BusyMessage = string.Empty;
    }

    private async Task GenerateClassLibraryAsync()
    {
        BusyMessage = "Generating class library...";
        AddStatus("Info", "Generating client library.");

        var result = await _workflowService.GenerateAsync(BuildGenerationOptions(), CancellationToken.None);
        PopulateFromGenerationResult(result);

        BusyMessage = string.Empty;
    }

    private async Task PackageClassLibraryAsync()
    {
        BusyMessage = "Packaging NuGet package...";

        if (string.IsNullOrWhiteSpace(_generatedProjectFilePath))
        {
            AddStatus("Warning", "Generate the client library before packaging.");
            BusyMessage = string.Empty;
            return;
        }

        var result = await _workflowService.PackageAsync(_generatedProjectFilePath, CancellationToken.None);
        PopulateFromGenerationResult(result);

        BusyMessage = string.Empty;
    }

    private WcfMetadataDiscoveryOptions BuildDiscoveryOptions()
        => new()
        {
            ServiceEndpointUrl = ServiceEndpointUrl,
            MetadataEndpointUrl = MetadataEndpointUrl,
            WsdlFilePath = WsdlFilePath,
            MetadataFolderPath = MetadataFolderPath,
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
        StatusMessages.Add(new StatusMessage
        {
            Severity = severity,
            Message = message
        });
    }

    private void ClearState(bool clearMessages)
    {
        Operations.Clear();

        if (clearMessages)
        {
            StatusMessages.Clear();
        }
    }
}
