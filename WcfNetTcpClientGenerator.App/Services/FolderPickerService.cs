using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WcfNetTcpClientGenerator.App.Services;

public sealed class FolderPickerService : IFolderPickerService
{
    private nint _windowHandle;

    public void Initialize(nint windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, _windowHandle);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
