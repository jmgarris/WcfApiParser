namespace WcfNetTcpClientGenerator.Core;

public sealed record MethodDocumentationResult
{
    public bool Success { get; init; }

    public string XmlDocumentationText { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Remarks { get; init; } = string.Empty;

    public IReadOnlyList<DocumentationGenerationDiagnostic> Diagnostics { get; init; } = [];

    public bool WasGeneratedByAi { get; init; }

    public string RawProviderName { get; init; } = string.Empty;
}
