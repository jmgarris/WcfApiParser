using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
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
        var windowHandle = WindowNative.GetWindowHandle(this);
        ConfigureTitleBar(windowHandle);

        ViewModel = viewModel;
        RootLayout.DataContext = viewModel;

        folderPickerService.Initialize(windowHandle);
        filePickerService.Initialize(windowHandle);
    }

    public MainViewModel ViewModel { get; }

    private void ConfigureTitleBar(nint windowHandle)
    {
        AppWindow.Title = "WCF API Parser";

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = AppWindow.TitleBar;
            var background = Color.FromArgb(255, 32, 32, 32);
            var foreground = Color.FromArgb(255, 245, 245, 245);
            var inactiveForeground = Color.FromArgb(255, 160, 160, 160);

            titleBar.PreferredTheme = TitleBarTheme.Dark;
            titleBar.BackgroundColor = background;
            titleBar.ForegroundColor = foreground;
            titleBar.InactiveBackgroundColor = background;
            titleBar.InactiveForegroundColor = inactiveForeground;
            titleBar.ButtonBackgroundColor = background;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonInactiveBackgroundColor = background;
            titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        }

        ApplyDwmDarkTitleBar(windowHandle);
    }

    private static void ApplyDwmDarkTitleBar(nint windowHandle)
    {
        if (windowHandle == 0 || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        const int useImmersiveDarkMode = 20;
        const int captionColor = 35;
        const int textColor = 36;
        var darkMode = 1;
        var darkCaption = 0x00202020;
        var lightText = 0x00F5F5F5;
        _ = DwmSetWindowAttribute(windowHandle, useImmersiveDarkMode, ref darkMode, sizeof(int));
        _ = DwmSetWindowAttribute(windowHandle, captionColor, ref darkCaption, sizeof(int));
        _ = DwmSetWindowAttribute(windowHandle, textColor, ref lightText, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

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
