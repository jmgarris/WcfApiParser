namespace WcfNetTcpClientGenerator.Core;

public sealed class ProxyCodeGenerator
{
    private readonly DotNetSvcUtilRunner _runner;

    public ProxyCodeGenerator(DotNetSvcUtilRunner runner)
    {
        _runner = runner;
    }

    public async Task<ProxyGenerationResult> GenerateAsync(
        IReadOnlyList<string> metadataSources,
        string outputDirectory,
        string serviceNamespace,
        CancellationToken cancellationToken)
    {
        if (!await _runner.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return new ProxyGenerationResult
            {
                Diagnostics =
                [
                    new GenerationDiagnostic(
                        DiagnosticSeverity.Error,
                        "dotnet-svcutil was not found. Install it or run the solution from the repository root that contains the local tool manifest.",
                        "DOTNET_SVCUTIL_NOT_FOUND")
                ]
            };
        }

        var sanitizedNamespace = CSharpIdentifierSanitizer.SanitizeNamespace(serviceNamespace);
        var proxyPath = Path.Combine(outputDirectory, "GeneratedProxy.cs");

        var result = await _runner.GenerateProxyAsync(
            metadataSources,
            outputDirectory,
            proxyPath,
            sanitizedNamespace,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success || string.IsNullOrWhiteSpace(result.ProxyFilePath))
        {
            return new ProxyGenerationResult
            {
                Diagnostics =
                [
                    new GenerationDiagnostic(
                        DiagnosticSeverity.Error,
                        BuildFailureMessage(metadataSources, result),
                        "PROXY_GENERATION_FAILED")
                ]
            };
        }

        var parseResult = ProxyCodeParser.Parse(await File.ReadAllTextAsync(result.ProxyFilePath, cancellationToken).ConfigureAwait(false), sanitizedNamespace);
        var diagnostics = parseResult.Diagnostics.ToList();

        diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Info, $"Proxy generated at {result.ProxyFilePath}."));

        return new ProxyGenerationResult
        {
            Success = parseResult.Metadata is not null,
            ProxyFilePath = result.ProxyFilePath,
            Metadata = parseResult.Metadata,
            Diagnostics = diagnostics
        };
    }

    private static string BuildFailureMessage(IReadOnlyList<string> metadataSources, DotNetSvcUtilRunner.DotNetSvcUtilResult result)
    {
        var details = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return $"Proxy generation failed for metadata source(s): {string.Join(", ", metadataSources)}. {details}".Trim();
    }

    public sealed class ProxyGenerationResult
    {
        public bool Success { get; init; }

        public string? ProxyFilePath { get; init; }

        public WcfServiceMetadataModel? Metadata { get; init; }

        public IReadOnlyList<GenerationDiagnostic> Diagnostics { get; init; } = [];
    }
}
