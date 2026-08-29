using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.UI;
using WcfNetTcpClientGenerator.App.Services;
using WcfNetTcpClientGenerator.App.ViewModels;

namespace WcfNetTcpClientGenerator.App;

public partial class MainWindow : Window
{
    public MainWindow(
        MainViewModel viewModel,
        IFolderPickerService folderPickerService,
        IFilePickerService filePickerService)
    {
        InitializeComponent();
        ConfigureTitleBar();

        ViewModel = viewModel;
        RootLayout.DataContext = viewModel;

        var windowHandle = WindowNative.GetWindowHandle(this);
        folderPickerService.Initialize(windowHandle);
        filePickerService.Initialize(windowHandle);
    }

    public MainViewModel ViewModel { get; }

    private void ConfigureTitleBar()
    {
        AppWindow.Title = "WCF API Parser";

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = AppWindow.TitleBar;
        var background = Color.FromArgb(255, 32, 32, 32);
        var foreground = Color.FromArgb(255, 245, 245, 245);
        var inactiveForeground = Color.FromArgb(255, 160, 160, 160);

        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveBackgroundColor = background;
        titleBar.InactiveForegroundColor = inactiveForeground;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.PasswordBox passwordBox)
        {
            ViewModel.Password = passwordBox.Password;
        }
    }

    private void OpenAiApiKeyPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.PasswordBox passwordBox)
        {
            ViewModel.OpenAiApiKey = passwordBox.Password;
        }
    }
}
