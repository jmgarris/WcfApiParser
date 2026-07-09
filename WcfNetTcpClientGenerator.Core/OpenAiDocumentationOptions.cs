namespace WcfNetTcpClientGenerator.Core;

public sealed class OpenAiDocumentationOptions
{
    public OpenAiApiKeySource ApiKeySource { get; init; } = OpenAiApiKeySource.EnvironmentVariable;

    public string ApiKeyEnvironmentVariableName { get; init; } = "OPENAI_API_KEY";

    public string UserEnteredApiKey { get; init; } = string.Empty;

    public string ModelName { get; init; } = "gpt-5.6";

    public int MaxOutputTokens { get; init; } = 600;

    public double Temperature { get; init; } = 0.2d;
}
