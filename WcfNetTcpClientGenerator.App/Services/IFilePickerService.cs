namespace WcfNetTcpClientGenerator.App.Services;

public interface IFilePickerService
{
    void Initialize(nint windowHandle);

    Task<string?> PickExecutableAsync();
}
