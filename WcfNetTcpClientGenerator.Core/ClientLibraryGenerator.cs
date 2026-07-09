using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class ClientLibraryGenerator
{
    private readonly WcfMetadataReader _metadataReader;
    private readonly ProxyCodeGenerator _proxyCodeGenerator;
    private readonly WrapperInterfaceGenerator _interfaceGenerator;
    private readonly WrapperImplementationGenerator _implementationGenerator;
    private readonly NetTcpBindingFactoryGenerator _bindingFactoryGenerator;
    private readonly ProjectFileGenerator _projectFileGenerator;

    public ClientLibraryGenerator(
        WcfMetadataReader metadataReader,
        ProxyCodeGenerator proxyCodeGenerator,
        WrapperInterfaceGenerator interfaceGenerator,
        WrapperImplementationGenerator implementationGenerator,
        NetTcpBindingFactoryGenerator bindingFactoryGenerator,
        ProjectFileGenerator projectFileGenerator)
    {
        _metadataReader = metadataReader;
        _proxyCodeGenerator = proxyCodeGenerator;
        _interfaceGenerator = interfaceGenerator;
        _implementationGenerator = implementationGenerator;
        _bindingFactoryGenerator = bindingFactoryGenerator;
        _projectFileGenerator = projectFileGenerator;
    }

    public async Task<GenerationResult> GenerateAsync(ClientLibraryGenerationOptions options, CancellationToken cancellationToken)
    {
        var diagnostics = ValidateOptions(options).ToList();
        diagnostics.AddRange(_bindingFactoryGenerator.ValidateOptions(options));

        if (diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new GenerationResult
            {
                Success = false,
                Diagnostics = diagnostics
            };
        }

        var sanitizedLibraryName = CSharpIdentifierSanitizer.SanitizeTypeName(options.GeneratedLibraryName, "GeneratedNetTcpClient");
        var libraryDirectory = Path.Combine(Path.GetFullPath(options.OutputFolder), sanitizedLibraryName);

        if (Directory.Exists(libraryDirectory) && Directory.EnumerateFileSystemEntries(libraryDirectory).Any())
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                $"The output folder already exists and is not empty: {libraryDirectory}",
                "OUTPUT_FOLDER_EXISTS"));

            return new GenerationResult
            {
                Success = false,
                Diagnostics = diagnostics
            };
        }

        Directory.CreateDirectory(libraryDirectory);

        ProxyCodeGenerator.ProxyGenerationResult proxyResult;
        if (!string.IsNullOrWhiteSpace(options.ExistingProxyCode))
        {
            var proxyDirectory = Path.Combine(libraryDirectory, "ServiceReferences");
            Directory.CreateDirectory(proxyDirectory);

            var proxyFilePath = Path.Combine(proxyDirectory, "GeneratedProxy.cs");
            await File.WriteAllTextAsync(proxyFilePath, options.ExistingProxyCode, cancellationToken).ConfigureAwait(false);

            var parsed = ProxyCodeParser.Parse(options.ExistingProxyCode, CSharpIdentifierSanitizer.SanitizeNamespace(options.DiscoveryOptions.ServiceNamespace));
            proxyResult = new ProxyCodeGenerator.ProxyGenerationResult
            {
                Success = parsed.Metadata is not null,
                ProxyFilePath = proxyFilePath,
                Metadata = parsed.Metadata,
                Diagnostics = parsed.Diagnostics
            };
        }
        else
        {
            var metadataResult = await _metadataReader.ReadAsync(options.DiscoveryOptions, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(metadataResult.Diagnostics);

            if (!metadataResult.Success || metadataResult.Metadata is null || metadataResult.MetadataSources.Count == 0)
            {
                return new GenerationResult
                {
                    Success = false,
                    Diagnostics = diagnostics
                };
            }

            proxyResult = await _proxyCodeGenerator.GenerateAsync(
                metadataResult.MetadataSources,
                Path.Combine(libraryDirectory, "ServiceReferences"),
                options.DiscoveryOptions.ServiceNamespace,
                cancellationToken).ConfigureAwait(false);
        }

        diagnostics.AddRange(proxyResult.Diagnostics);

        if (!proxyResult.Success || proxyResult.Metadata is null || string.IsNullOrWhiteSpace(proxyResult.ProxyFilePath))
        {
            return new GenerationResult
            {
                Success = false,
                OutputDirectory = libraryDirectory,
                Diagnostics = diagnostics
            };
        }

        var projectFilePath = Path.Combine(libraryDirectory, $"{sanitizedLibraryName}.csproj");

        await WriteLibraryAsync(
            libraryDirectory,
            sanitizedLibraryName,
            proxyResult.Metadata,
            options,
            projectFilePath,
            cancellationToken).ConfigureAwait(false);

        diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Info, $"Generated client library at {libraryDirectory}."));

        return new GenerationResult
        {
            Success = true,
            OutputDirectory = libraryDirectory,
            ProjectFilePath = projectFilePath,
            Metadata = proxyResult.Metadata,
            Diagnostics = diagnostics
        };
    }

    private async Task WriteLibraryAsync(
        string libraryDirectory,
        string libraryNamespace,
        WcfServiceMetadataModel metadata,
        ClientLibraryGenerationOptions options,
        string projectFilePath,
        CancellationToken cancellationToken)
    {
        var optionsDirectory = Path.Combine(libraryDirectory, "Options");
        var interfacesDirectory = Path.Combine(libraryDirectory, "Interfaces");
        var servicesDirectory = Path.Combine(libraryDirectory, "Services");
        var bindingDirectory = Path.Combine(libraryDirectory, "Binding");
        var diDirectory = Path.Combine(libraryDirectory, "DependencyInjection");

        Directory.CreateDirectory(optionsDirectory);
        Directory.CreateDirectory(interfacesDirectory);
        Directory.CreateDirectory(servicesDirectory);
        Directory.CreateDirectory(bindingDirectory);
        Directory.CreateDirectory(diDirectory);

        await File.WriteAllTextAsync(projectFilePath, _projectFileGenerator.Generate(libraryNamespace, options), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(optionsDirectory, "NetTcpWcfClientOptions.cs"), GenerateOptionsClass(libraryNamespace, options), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(bindingDirectory, "NetTcpBindingFactory.cs"), _bindingFactoryGenerator.Generate(libraryNamespace, options), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(diDirectory, "ServiceCollectionExtensions.cs"), GenerateServiceCollectionExtensions(libraryNamespace, metadata), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(libraryDirectory, "README.md"), GenerateReadme(libraryNamespace, metadata), cancellationToken).ConfigureAwait(false);

        foreach (var contract in metadata.Contracts)
        {
            var contractTypeName = CSharpIdentifierSanitizer.SanitizeTypeName(contract.ContractName);
            await File.WriteAllTextAsync(
                Path.Combine(interfacesDirectory, $"I{contractTypeName}Client.cs"),
                _interfaceGenerator.Generate(contract, libraryNamespace),
                cancellationToken).ConfigureAwait(false);

            await File.WriteAllTextAsync(
                Path.Combine(servicesDirectory, $"{contractTypeName}Client.cs"),
                _implementationGenerator.Generate(contract, libraryNamespace),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<GenerationDiagnostic> ValidateOptions(ClientLibraryGenerationOptions options)
    {
        var diagnostics = new List<GenerationDiagnostic>();

        if (string.IsNullOrWhiteSpace(options.OutputFolder))
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                "An output folder is required.",
                "OUTPUT_FOLDER_REQUIRED"));
        }
        else if (Path.IsPathRooted(options.OutputFolder) && options.OutputFolder.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                $"Invalid output folder path: {options.OutputFolder}",
                "INVALID_OUTPUT_PATH"));
        }

        if (!long.TryParse(options.MaxReceivedMessageSize, out _))
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                $"Invalid max received message size: {options.MaxReceivedMessageSize}",
                "INVALID_MESSAGE_SIZE"));
        }

        foreach (var value in new[] { options.OpenTimeout, options.CloseTimeout, options.SendTimeout, options.ReceiveTimeout })
        {
            if (!TimeSpan.TryParse(value, out _))
            {
                diagnostics.Add(new GenerationDiagnostic(
                    DiagnosticSeverity.Error,
                    $"Invalid timeout value: {value}",
                    "INVALID_TIMEOUT"));
            }
        }

        return diagnostics;
    }

    private static string GenerateOptionsClass(string libraryNamespace, ClientLibraryGenerationOptions options)
        => $$"""
namespace {{libraryNamespace}}.Options;

public sealed class NetTcpWcfClientOptions
{
    public string EndpointUrl { get; set; } = string.Empty;

    public string SecurityMode { get; set; } = "{{options.SecurityMode}}";

    public string TcpClientCredentialType { get; set; } = "{{options.TcpClientCredentialType}}";

    public bool ReliableSessionEnabled { get; set; } = {{options.ReliableSessionEnabled.ToString().ToLowerInvariant()}};

    public global::System.TimeSpan OpenTimeout { get; set; } = global::System.TimeSpan.Parse("{{options.OpenTimeout}}");

    public global::System.TimeSpan CloseTimeout { get; set; } = global::System.TimeSpan.Parse("{{options.CloseTimeout}}");

    public global::System.TimeSpan SendTimeout { get; set; } = global::System.TimeSpan.Parse("{{options.SendTimeout}}");

    public global::System.TimeSpan ReceiveTimeout { get; set; } = global::System.TimeSpan.Parse("{{options.ReceiveTimeout}}");

    public long MaxReceivedMessageSize { get; set; } = {{options.MaxReceivedMessageSize}};

    public string? Username { get; set; }

    public string? Password { get; set; }
}
""";

    private static string GenerateServiceCollectionExtensions(string libraryNamespace, WcfServiceMetadataModel metadata)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine($"using {libraryNamespace}.Interfaces;");
        builder.AppendLine($"using {libraryNamespace}.Options;");
        builder.AppendLine($"using {libraryNamespace}.Services;");
        builder.AppendLine();
        builder.AppendLine($"namespace {libraryNamespace}.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine("public static class ServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedNetTcpClients(");
        builder.AppendLine("        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services,");
        builder.AppendLine("        global::System.Action<NetTcpWcfClientOptions> configure)");
        builder.AppendLine("    {");
        builder.AppendLine("        var options = new NetTcpWcfClientOptions();");
        builder.AppendLine("        configure(options);");
        builder.AppendLine("        services.AddSingleton(options);");

        foreach (var contract in metadata.Contracts)
        {
            var contractTypeName = CSharpIdentifierSanitizer.SanitizeTypeName(contract.ContractName);
            builder.AppendLine($"        services.AddSingleton<I{contractTypeName}Client, {contractTypeName}Client>();");
        }

        builder.AppendLine("        return services;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string GenerateReadme(string libraryNamespace, WcfServiceMetadataModel metadata)
    {
        var firstContract = metadata.Contracts.FirstOrDefault();
        var contractTypeName = firstContract is null
            ? "GeneratedService"
            : CSharpIdentifierSanitizer.SanitizeTypeName(firstContract.ContractName);

        var exampleMethod = firstContract?.Operations.FirstOrDefault()?.MethodName ?? "CallAsync";

        return $$"""
# {{libraryNamespace}}

This library was generated for a WCF `net.tcp` service and is ready to package as NuGet.

## Example

```csharp
var options = new NetTcpWcfClientOptions
{
    EndpointUrl = "net.tcp://server:808/MyService",
    SecurityMode = "Transport",
    TcpClientCredentialType = "Windows"
};

var client = new {{contractTypeName}}Client(options);
await client.{{exampleMethod}}(request);
```
""";
    }
}
