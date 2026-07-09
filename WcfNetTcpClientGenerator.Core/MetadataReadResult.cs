namespace WcfNetTcpClientGenerator.Core;

public sealed class MetadataReadResult
{
    public bool Success { get; init; }

    public WcfServiceMetadataModel? Metadata { get; init; }

    public string? ProxyFilePath { get; init; }

    public string? WorkingDirectory { get; init; }

    public IReadOnlyList<string> MetadataSources { get; init; } = [];

    public IReadOnlyList<GenerationDiagnostic> Diagnostics { get; init; } = [];
}
