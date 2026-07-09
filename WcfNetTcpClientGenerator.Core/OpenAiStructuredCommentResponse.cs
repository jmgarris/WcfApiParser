namespace WcfNetTcpClientGenerator.Core;

public sealed class OpenAiStructuredCommentResponse
{
    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<OpenAiStructuredParameterComment> Parameters { get; init; } = [];

    public string Returns { get; init; } = string.Empty;

    public IReadOnlyList<OpenAiStructuredExceptionComment> Exceptions { get; init; } = [];

    public string Remarks { get; init; } = string.Empty;
}

public sealed class OpenAiStructuredParameterComment
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}

public sealed class OpenAiStructuredExceptionComment
{
    public string Type { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
