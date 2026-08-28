using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WcfNetTcpClientGenerator.App.Services;

public sealed class FilePickerService : IFilePickerService
{
    private nint _windowHandle;

    public void Initialize(nint windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public async Task<string?> PickExecutableAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");
        InitializeWithWindow.Initialize(picker, _windowHandle);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}
