using Windows.Security.Credentials;

namespace WcfNetTcpClientGenerator.App.Services;

public sealed class PasswordVaultOpenAiSecretStore : IOpenAiSecretStore
{
    private const string ResourceName = "WcfNetTcpClientGenerator.OpenAI";
    private const string UserName = "ApiKey";
    private readonly PasswordVault _passwordVault = new();

    public Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TryRemoveExisting();
        _passwordVault.Add(new PasswordCredential(ResourceName, UserName, apiKey));
        return Task.CompletedTask;
    }

    public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var credential = _passwordVault.Retrieve(ResourceName, UserName);
            credential.RetrievePassword();
            return Task.FromResult<string?>(credential.Password);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task ClearApiKeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryRemoveExisting();
        return Task.CompletedTask;
    }

    private void TryRemoveExisting()
    {
        try
        {
            var credential = _passwordVault.Retrieve(ResourceName, UserName);
            _passwordVault.Remove(credential);
        }
        catch
        {
        }
    }
}
