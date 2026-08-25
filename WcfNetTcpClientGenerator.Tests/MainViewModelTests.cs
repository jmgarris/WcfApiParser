using WcfNetTcpClientGenerator.App.Models;
using WcfNetTcpClientGenerator.App.Services;
using WcfNetTcpClientGenerator.App.ViewModels;
using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.Tests;

[TestFixture]
public sealed class MainViewModelTests
{
    [Test]
    public void ClearStatusCommand_ClearsOnlyDisplayedStatusState()
    {
        var workflow = new FakeGeneratorWorkflowService();
        var viewModel = CreateViewModel(workflow);

        viewModel.ServiceEndpointUrl = "net.tcp://server:808/MyService";
        viewModel.OutputFolder = @"C:\Output\Client";
        viewModel.BusyMessage = "Working...";
        viewModel.ProgressPercentage = 73;
        viewModel.StatusMessages.Add(new StatusMessage { Severity = "Info", Message = "Started" });
        viewModel.StatusMessages.Add(new StatusMessage { Severity = "Warning", Message = "Watch this" });
        viewModel.Operations.Add(new OperationRow
        {
            ContractName = "CustomerService",
            OperationName = "GetCustomer",
            Signature = "CustomerResponse GetCustomer(GetCustomerRequest request)"
        });

        viewModel.ClearStatusCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StatusMessages, Is.Empty);
            Assert.That(viewModel.BusyMessage, Is.Empty);
            Assert.That(viewModel.ProgressPercentage, Is.EqualTo(0));
            Assert.That(viewModel.ServiceEndpointUrl, Is.EqualTo("net.tcp://server:808/MyService"));
            Assert.That(viewModel.OutputFolder, Is.EqualTo(@"C:\Output\Client"));
            Assert.That(viewModel.Operations.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ClearStatusCommand_DoesNotCancelRunningOperation()
    {
        var workflow = new FakeGeneratorWorkflowService();
        workflow.AnalyzeHandler = _ => workflow.AnalyzeCompletion.Task;

        var viewModel = CreateViewModel(workflow);
        viewModel.ServiceEndpointUrl = "net.tcp://server:808/MyService";

        var analyzeTask = viewModel.AnalyzeServiceMetadataCommand.ExecuteAsync(null);
        viewModel.ClearStatusCommand.Execute(null);

        Assert.That(workflow.ObservedAnalyzeCancellationToken.IsCancellationRequested, Is.False);

        workflow.AnalyzeCompletion.SetResult(new MetadataReadResult
        {
            Success = true,
            Metadata = new WcfServiceMetadataModel
            {
                Contracts =
                [
                    new WcfContractModel
                    {
                        ContractName = "CustomerService",
                        Operations =
                        [
                            new WcfOperationModel
                            {
                                OperationName = "GetCustomer",
                                MethodName = "GetCustomer",
                                ResponseTypeName = "CustomerResponse",
                                Parameters =
                                [
                                    new WcfParameterModel
                                    {
                                        Name = "request",
                                        TypeName = "GetCustomerRequest"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        });

        await analyzeTask;

        Assert.That(workflow.ObservedAnalyzeCancellationToken.IsCancellationRequested, Is.False);
    }

    [Test]
    public void SelectingOpenAiProvider_UpdatesProviderState()
    {
        var viewModel = CreateViewModel(new FakeGeneratorWorkflowService());

        viewModel.SelectedDocumentationProvider = DocumentationProviderKind.OpenAI;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.EnableOpenAiComments, Is.True);
            Assert.That(viewModel.IsOpenAiProviderSelected, Is.True);
            Assert.That(viewModel.EnableCopilotComments, Is.False);
        });
    }

    [Test]
    public void OpenAiDefaults_UseGpt56LunaWithNoReasoningEffort()
    {
        var viewModel = CreateViewModel(new FakeGeneratorWorkflowService());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.OpenAiModelName, Is.EqualTo("gpt-5.6-luna"));
            Assert.That(viewModel.OpenAiReasoningEffort, Is.EqualTo("none"));
        });
    }

    private static MainViewModel CreateViewModel(FakeGeneratorWorkflowService workflow)
        => new(new FakeFolderPickerService(), new FakeFilePickerService(), workflow);

    private sealed class FakeFolderPickerService : IFolderPickerService
    {
        public void Initialize(nint windowHandle)
        {
        }

        public Task<string?> PickFolderAsync()
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public void Initialize(nint windowHandle)
        {
        }

        public Task<string?> PickExecutableAsync()
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeGeneratorWorkflowService : IGeneratorWorkflowService
    {
        public TaskCompletionSource<MetadataReadResult> AnalyzeCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Func<WcfMetadataDiscoveryOptions, Task<MetadataReadResult>>? AnalyzeHandler { get; set; }

        public CancellationToken ObservedAnalyzeCancellationToken { get; private set; }

        public Task<MetadataReadResult> AnalyzeAsync(WcfMetadataDiscoveryOptions options, CancellationToken cancellationToken)
        {
            ObservedAnalyzeCancellationToken = cancellationToken;
            return AnalyzeHandler is not null
                ? AnalyzeHandler(options)
                : Task.FromResult(new MetadataReadResult());
        }

        public Task<GenerationResult> GenerateAsync(ClientLibraryGenerationOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new GenerationResult());

        public Task<GenerationResult> PackageAsync(string projectFilePath, CancellationToken cancellationToken)
            => Task.FromResult(new GenerationResult());

        public Task<DotNetSvcUtilPreflightResult> TestDotNetSvcUtilAsync(WcfMetadataDiscoveryOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new DotNetSvcUtilPreflightResult());

        public Task<CopilotConnectionTestResult> SignInToCopilotAsync(CopilotChatOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new CopilotConnectionTestResult());

        public Task<CopilotConnectionTestResult> SignOutOfCopilotAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CopilotConnectionTestResult());

        public Task<CopilotConnectionTestResult> TestCopilotConnectionAsync(CopilotChatOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new CopilotConnectionTestResult());

        public Task<OpenAiConnectionTestResult> TestOpenAiConnectionAsync(OpenAiDocumentationOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new OpenAiConnectionTestResult());

        public void ClearOpenAiDocumentationCache()
        {
        }
    }
}
