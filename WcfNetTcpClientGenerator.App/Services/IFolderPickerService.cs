namespace WcfNetTcpClientGenerator.App.Services;

public interface IFolderPickerService
{
    void Initialize(nint windowHandle);

    Task<string?> PickFolderAsync();
}
