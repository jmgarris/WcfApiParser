namespace WcfNetTcpClientGenerator.Core;

public sealed class WcfMetadataReader
{
    private readonly ProxyCodeGenerator _proxyCodeGenerator;

    public WcfMetadataReader(ProxyCodeGenerator proxyCodeGenerator)
    {
        _proxyCodeGenerator = proxyCodeGenerator;
    }

    public async Task<MetadataReadResult> ReadAsync(WcfMetadataDiscoveryOptions options, CancellationToken cancellationToken)
    {
        var diagnostics = Validate(options);
        if (diagnostics.Count > 0)
        {
            return new MetadataReadResult
            {
                Diagnostics = diagnostics,
                Success = false
            };
        }

        var candidateSets = BuildMetadataCandidates(options);
        var attemptDiagnostics = new List<GenerationDiagnostic>();

        foreach (var candidateSet in candidateSets)
        {
            var workingDirectory = Path.Combine(Path.GetTempPath(), "WcfNetTcpClientGenerator", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            var proxyResult = await _proxyCodeGenerator.GenerateAsync(
                candidateSet,
                workingDirectory,
                options.ServiceNamespace,
                cancellationToken).ConfigureAwait(false);

            attemptDiagnostics.AddRange(proxyResult.Diagnostics);

            if (proxyResult.Success && proxyResult.Metadata is not null)
            {
                return new MetadataReadResult
                {
                    Success = true,
                    Metadata = new WcfServiceMetadataModel
                    {
                        ServiceNamespace = proxyResult.Metadata.ServiceNamespace,
                        SourceDescription = string.Join(", ", candidateSet),
                        Contracts = proxyResult.Metadata.Contracts
                    },
                    ProxyFilePath = proxyResult.ProxyFilePath,
                    WorkingDirectory = workingDirectory,
                    MetadataSources = candidateSet,
                    Diagnostics = attemptDiagnostics
                };
            }
        }

        attemptDiagnostics.Add(
            new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                "Metadata could not be discovered. Ensure the service exposes MEX or WSDL metadata, or provide a local WSDL/XSD file or folder.",
                "METADATA_DISCOVERY_FAILED"));

        return new MetadataReadResult
        {
            Success = false,
            Diagnostics = attemptDiagnostics
        };
    }

    public IReadOnlyList<IReadOnlyList<string>> BuildMetadataCandidates(WcfMetadataDiscoveryOptions options)
    {
        var candidates = new List<IReadOnlyList<string>>();

        if (!string.IsNullOrWhiteSpace(options.WsdlFilePath))
        {
            candidates.Add([Path.GetFullPath(options.WsdlFilePath)]);
        }

        if (!string.IsNullOrWhiteSpace(options.MetadataFolderPath))
        {
            var files = Directory
                .EnumerateFiles(options.MetadataFolderPath, "*.*", SearchOption.AllDirectories)
                .Where(static file => file.EndsWith(".wsdl", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length > 0)
            {
                candidates.Add(files);
            }
        }

        if (!string.IsNullOrWhiteSpace(options.MetadataEndpointUrl))
        {
            candidates.Add([options.MetadataEndpointUrl.Trim()]);
        }

        if (!string.IsNullOrWhiteSpace(options.ServiceEndpointUrl))
        {
            foreach (var candidate in BuildMexCandidates(options.ServiceEndpointUrl.Trim()))
            {
                candidates.Add([candidate]);
            }
        }

        return candidates
            .GroupBy(
                static set => string.Join("|", set),
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static List<GenerationDiagnostic> Validate(WcfMetadataDiscoveryOptions options)
    {
        var diagnostics = new List<GenerationDiagnostic>();

        if (string.IsNullOrWhiteSpace(options.ServiceEndpointUrl)
            && string.IsNullOrWhiteSpace(options.MetadataEndpointUrl)
            && string.IsNullOrWhiteSpace(options.WsdlFilePath)
            && string.IsNullOrWhiteSpace(options.MetadataFolderPath))
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                "Provide at least one metadata source: a net.tcp endpoint, metadata URL, WSDL file, or metadata folder.",
                "NO_METADATA_SOURCE"));
        }

        if (!string.IsNullOrWhiteSpace(options.ServiceEndpointUrl) && !WcfEndpointValidator.IsValidNetTcpEndpoint(options.ServiceEndpointUrl))
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                $"Invalid net.tcp endpoint URL: {options.ServiceEndpointUrl}",
                "INVALID_NETTCP_ENDPOINT"));
        }

        if (!string.IsNullOrWhiteSpace(options.MetadataEndpointUrl) && !WcfEndpointValidator.IsValidMetadataUrl(options.MetadataEndpointUrl))
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                $"Invalid metadata endpoint URL: {options.MetadataEndpointUrl}",
                "INVALID_METADATA_URL"));
        }

        if (!string.IsNullOrWhiteSpace(options.WsdlFilePath) && !File.Exists(options.WsdlFilePath))
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                $"The WSDL file was not found: {options.WsdlFilePath}",
                "WSDL_FILE_MISSING"));
        }

        if (!string.IsNullOrWhiteSpace(options.MetadataFolderPath))
        {
            if (!Directory.Exists(options.MetadataFolderPath))
            {
                diagnostics.Add(new GenerationDiagnostic(
                    DiagnosticSeverity.Error,
                    $"The metadata folder was not found: {options.MetadataFolderPath}",
                    "METADATA_FOLDER_MISSING"));
            }
            else if (!Directory.EnumerateFiles(options.MetadataFolderPath, "*.*", SearchOption.AllDirectories)
                         .Any(static file => file.EndsWith(".wsdl", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(new GenerationDiagnostic(
                    DiagnosticSeverity.Error,
                    $"The metadata folder does not contain WSDL or XSD files: {options.MetadataFolderPath}",
                    "METADATA_FOLDER_EMPTY"));
            }
        }

        return diagnostics;
    }

    private static IEnumerable<string> BuildMexCandidates(string serviceEndpointUrl)
    {
        var uri = new Uri(serviceEndpointUrl, UriKind.Absolute);
        var baseUrl = serviceEndpointUrl.TrimEnd('/');
        var candidates = new[]
        {
            $"{baseUrl}/mex",
            $"{baseUrl}/mex?wsdl",
            $"{baseUrl}?wsdl",
            $"{uri.GetLeftPart(UriPartial.Authority).TrimEnd('/')}/mex"
        };

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
