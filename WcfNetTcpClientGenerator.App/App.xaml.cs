using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using WcfNetTcpClientGenerator.App.Services;
using WcfNetTcpClientGenerator.App.ViewModels;
using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        Services = ConfigureServices();
    }

    public static IServiceProvider Services { get; private set; } = default!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = Services.GetRequiredService<MainWindow>();
        window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddHttpClient<CopilotChatClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        services.AddSingleton<DotNetSvcUtilRunner>();
        services.AddSingleton<WcfMetadataReader>();
        services.AddSingleton<ProxyCodeGenerator>();
        services.AddSingleton<WrapperInterfaceGenerator>();
        services.AddSingleton<NullMethodDocumentationProvider>();
        services.AddSingleton<WrapperImplementationGenerator>();
        services.AddSingleton<DocumentationPromptBuilder>();
        services.AddSingleton<XmlDocumentationSanitizer>();
        services.AddSingleton<MethodDocumentationCache>();
        services.AddSingleton<IGraphAccessTokenProvider, GraphAccessTokenProvider>();
        services.AddSingleton<CopilotConversationManager>();
        services.AddSingleton<CopilotMethodDocumentationProvider>();
        services.AddSingleton<NetTcpBindingFactoryGenerator>();
        services.AddSingleton<ProjectFileGenerator>();
        services.AddSingleton<NuGetPackageBuilder>();
        services.AddSingleton<ClientLibraryGenerator>();

        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<ICopilotAuthenticationService, CopilotAuthenticationService>();
        services.AddSingleton<ICopilotConnectionService, CopilotConnectionService>();
        services.AddSingleton<IGeneratorWorkflowService, GeneratorWorkflowService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
