namespace WcfNetTcpClientGenerator.Core;

public sealed class GenerationResult
{
    public bool Success { get; init; }

    public string? OutputDirectory { get; init; }

    public string? ProjectFilePath { get; init; }

    public string? PackagePath { get; init; }

    public WcfServiceMetadataModel? Metadata { get; init; }

    public IReadOnlyList<GenerationDiagnostic> Diagnostics { get; init; } = [];
}
