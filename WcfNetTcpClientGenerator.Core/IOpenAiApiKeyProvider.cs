namespace WcfNetTcpClientGenerator.Core;

public interface IOpenAiApiKeyProvider
{
    Task<OpenAiApiKeyResult> ResolveApiKeyAsync(OpenAiDocumentationOptions options, CancellationToken cancellationToken);
}
