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
        string? configuredToolPath,
        string targetFramework,
        CancellationToken cancellationToken)
    {
        var preflightResult = await _runner.CheckAvailabilityAsync(configuredToolPath, outputDirectory, cancellationToken).ConfigureAwait(false);
        if (!preflightResult.ToolFound)
        {
            return new ProxyGenerationResult
            {
                Diagnostics =
                [
                    new GenerationDiagnostic(
                        DiagnosticSeverity.Error,
                        preflightResult.DiagnosticMessage,
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
            configuredToolPath,
            targetFramework,
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

        diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Info, $"Proxy tool used: dotnet-svcutil ({result.PreflightResult?.ToolExecutionMode})."));
        if (!string.IsNullOrWhiteSpace(result.PreflightResult?.ToolPath))
        {
            diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Info, $"dotnet-svcutil source: {result.PreflightResult.ToolPath}."));
        }
        diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Info, $"Command: {result.ExecutedCommand}"));
        diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Info, $"Working directory: {result.WorkingDirectory}"));
        diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Info, $"Proxy generated at {result.ProxyFilePath}."));

        return new ProxyGenerationResult
        {
            Success = parseResult.Metadata is not null,
            ProxyFilePath = result.ProxyFilePath,
            Metadata = parseResult.Metadata,
            Diagnostics = diagnostics
        };
    }

    public Task<ProxyGenerationResult> GenerateAsync(IReadOnlyList<string> metadataSources, string outputDirectory, string serviceNamespace, string? configuredToolPath, CancellationToken cancellationToken)
        => GenerateAsync(metadataSources, outputDirectory, serviceNamespace, configuredToolPath, "net10.0", cancellationToken);

    private static string BuildFailureMessage(IReadOnlyList<string> metadataSources, DotNetSvcUtilRunner.DotNetSvcUtilResult result)
    {
        if (result.PreflightResult is { ToolFound: false })
        {
            return result.PreflightResult.DiagnosticMessage;
        }

        var output = string.IsNullOrWhiteSpace(result.StandardOutput) ? "(none)" : result.StandardOutput.Trim();
        var error = string.IsNullOrWhiteSpace(result.StandardError) ? "(none)" : result.StandardError.Trim();

        return
            $"Proxy generation failed for metadata source(s): {string.Join(", ", metadataSources)}.{Environment.NewLine}" +
            $"Command: {result.ExecutedCommand}{Environment.NewLine}" +
            $"Working directory: {result.WorkingDirectory}{Environment.NewLine}" +
            $"Exit code: {result.ExitCode}{Environment.NewLine}" +
            $"Standard output:{Environment.NewLine}{output}{Environment.NewLine}" +
            $"Standard error:{Environment.NewLine}{error}";
    }

    public sealed class ProxyGenerationResult
    {
        public bool Success { get; init; }

        public string? ProxyFilePath { get; init; }

        public WcfServiceMetadataModel? Metadata { get; init; }

        public IReadOnlyList<GenerationDiagnostic> Diagnostics { get; init; } = [];
    }
}
