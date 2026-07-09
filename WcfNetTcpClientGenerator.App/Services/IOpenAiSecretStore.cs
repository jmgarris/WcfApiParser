namespace WcfNetTcpClientGenerator.App.Services;

public interface IOpenAiSecretStore
{
    Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken);

    Task<string?> GetApiKeyAsync(CancellationToken cancellationToken);

    Task ClearApiKeyAsync(CancellationToken cancellationToken);
}
