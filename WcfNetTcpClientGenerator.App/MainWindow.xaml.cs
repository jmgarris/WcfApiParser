using Microsoft.UI.Xaml;
using WinRT.Interop;
using WcfNetTcpClientGenerator.App.Services;
using WcfNetTcpClientGenerator.App.ViewModels;

namespace WcfNetTcpClientGenerator.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel, IFolderPickerService folderPickerService)
    {
        InitializeComponent();

        ViewModel = viewModel;
        RootLayout.DataContext = viewModel;

        folderPickerService.Initialize(WindowNative.GetWindowHandle(this));
    }

    public MainViewModel ViewModel { get; }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.PasswordBox passwordBox)
        {
            ViewModel.Password = passwordBox.Password;
        }
    }
}
