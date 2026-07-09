using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.Services;

public sealed class OpenAiApiKeyProvider : IOpenAiApiKeyProvider
{
    private readonly IOpenAiSecretStore _secretStore;

    public OpenAiApiKeyProvider(IOpenAiSecretStore secretStore)
    {
        _secretStore = secretStore;
    }

    public async Task<OpenAiApiKeyResult> ResolveApiKeyAsync(OpenAiDocumentationOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ModelName))
        {
            return Failure("An OpenAI model name is required.", "OPENAI_MODEL_REQUIRED");
        }

        if (options.ApiKeySource == OpenAiApiKeySource.EnvironmentVariable)
        {
            var variableName = string.IsNullOrWhiteSpace(options.ApiKeyEnvironmentVariableName)
                ? "OPENAI_API_KEY"
                : options.ApiKeyEnvironmentVariableName.Trim();

            var environmentValue = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                return new OpenAiApiKeyResult
                {
                    Success = true,
                    ApiKey = environmentValue,
                    SourceDescription = "Reading OpenAI API key from environment variable."
                };
            }

            return Failure($"The OpenAI API key environment variable '{variableName}' was not found or was empty.", "OPENAI_API_KEY_MISSING");
        }

        if (!string.IsNullOrWhiteSpace(options.UserEnteredApiKey))
        {
            await _secretStore.SaveApiKeyAsync(options.UserEnteredApiKey, cancellationToken).ConfigureAwait(false);
            return new OpenAiApiKeyResult
            {
                Success = true,
                ApiKey = options.UserEnteredApiKey,
                SourceDescription = "Using the user-entered OpenAI API key from the secure local secret store."
            };
        }

        var storedApiKey = await _secretStore.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(storedApiKey))
        {
            return new OpenAiApiKeyResult
            {
                Success = true,
                ApiKey = storedApiKey,
                SourceDescription = "Using the user-entered OpenAI API key from the secure local secret store."
            };
        }

        return Failure("An OpenAI API key is required. Set OPENAI_API_KEY or enter a key to store securely.", "OPENAI_API_KEY_MISSING");
    }

    private static OpenAiApiKeyResult Failure(string message, string code)
        => new()
        {
            Success = false,
            Diagnostics =
            [
                new OpenAiDiagnostic
                {
                    Severity = "Error",
                    Code = code,
                    Message = message
                }
            ]
        };
}
