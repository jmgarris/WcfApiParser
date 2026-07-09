namespace WcfNetTcpClientGenerator.Core;

public sealed class MethodDocumentationOptions
{
    public DocumentationProviderKind ProviderKind { get; init; } = DocumentationProviderKind.LocalFallback;

    public int MaxCommentLength { get; init; } = 600;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public int RetryCount { get; init; } = 2;

    public bool CacheGeneratedComments { get; init; } = true;

    public bool RegenerateComments { get; init; }

    public CopilotChatOptions CopilotChat { get; init; } = new();

    public OpenAiDocumentationOptions OpenAi { get; init; } = new();

    public bool UsesAiProvider
        => ProviderKind is DocumentationProviderKind.Microsoft365Copilot or DocumentationProviderKind.OpenAI;
}
