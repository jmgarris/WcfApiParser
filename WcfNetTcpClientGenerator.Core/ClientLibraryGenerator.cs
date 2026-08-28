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
    private readonly NullMethodDocumentationProvider _fallbackDocumentationProvider;
    private readonly RestControllerGenerator _restControllerGenerator = new();

    public ClientLibraryGenerator(
        WcfMetadataReader metadataReader,
        ProxyCodeGenerator proxyCodeGenerator,
        WrapperInterfaceGenerator interfaceGenerator,
        WrapperImplementationGenerator implementationGenerator,
        NetTcpBindingFactoryGenerator bindingFactoryGenerator,
        ProjectFileGenerator projectFileGenerator,
        NullMethodDocumentationProvider fallbackDocumentationProvider)
    {
        _metadataReader = metadataReader;
        _proxyCodeGenerator = proxyCodeGenerator;
        _interfaceGenerator = interfaceGenerator;
        _implementationGenerator = implementationGenerator;
        _bindingFactoryGenerator = bindingFactoryGenerator;
        _projectFileGenerator = projectFileGenerator;
        _fallbackDocumentationProvider = fallbackDocumentationProvider;
    }

    public async Task<GenerationResult> GenerateAsync(ClientLibraryGenerationOptions options, CancellationToken cancellationToken)
    {
        var diagnostics = ValidateOptions(options).ToList();
        diagnostics.AddRange(_bindingFactoryGenerator.ValidateOptions(options));
        if (string.Equals(options.SecurityMode, "TransportWithMessageCredential", StringComparison.OrdinalIgnoreCase)
            && options.OutputKind != GeneratedOutputKind.NetFramework48RestApiWrapper)
        {
            diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Error,
                "TransportWithMessageCredential requires the .NET Framework 4.8.1 REST API wrapper output because this WCF net.tcp security mode is not supported by the .NET 10 generated client path.",
                "NET48_REST_WRAPPER_REQUIRED"));
        }

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
                options.DiscoveryOptions.DotNetSvcUtilPath,
                options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper ? "net48" : "net10.0",
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

        diagnostics.AddRange(await (options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper ? WriteRestWrapperAsync(
            libraryDirectory, sanitizedLibraryName, proxyResult.Metadata, options, projectFilePath, cancellationToken) : WriteLibraryAsync(
            libraryDirectory,
            sanitizedLibraryName,
            proxyResult.Metadata,
            options,
            projectFilePath,
            cancellationToken)).ConfigureAwait(false));

        if (diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new GenerationResult
            {
                Success = false,
                OutputDirectory = libraryDirectory,
                ProjectFilePath = projectFilePath,
                Metadata = proxyResult.Metadata,
                Diagnostics = diagnostics
            };
        }

        diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Info, options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper ? $"Generated REST API wrapper at {libraryDirectory}." : $"Generated client library at {libraryDirectory}."));

        return new GenerationResult
        {
            Success = true,
            OutputDirectory = libraryDirectory,
            ProjectFilePath = projectFilePath,
            Metadata = proxyResult.Metadata,
            Diagnostics = diagnostics
        };
    }

    private async Task<IReadOnlyList<GenerationDiagnostic>> WriteRestWrapperAsync(string directory, string ns, WcfServiceMetadataModel metadata, ClientLibraryGenerationOptions options, string projectFile, CancellationToken ct)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        Directory.CreateDirectory(Path.Combine(directory, "App_Start")); Directory.CreateDirectory(Path.Combine(directory, "Controllers")); Directory.CreateDirectory(Path.Combine(directory, "Wcf")); Directory.CreateDirectory(Path.Combine(directory, "Models"));
        diagnostics.AddRange(await WriteFormattedXmlAsync(projectFile, _projectFileGenerator.Generate(ns, options, metadata), ct).ConfigureAwait(false));
        diagnostics.AddRange(await WriteFormattedXmlAsync(Path.Combine(directory, "Web.config"), GenerateWebConfig(options), ct).ConfigureAwait(false));
        await File.WriteAllTextAsync(Path.Combine(directory, "Global.asax"), $"<%@ Application Codebehind=\"Global.asax.cs\" Inherits=\"{ns}.WebApiApplication\" Language=\"C#\" %>", ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "Global.asax.cs"), $"using System.Web.Http; namespace {ns} {{ public class WebApiApplication : System.Web.HttpApplication {{ protected void Application_Start() {{ GlobalConfiguration.Configure(App_Start.WebApiConfig.Register); }} }} }}", ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "App_Start", "WebApiConfig.cs"), GenerateWebApiConfig(ns), ct);
        if (options.EnableSwagger)
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "App_Start", "SwaggerConfig.cs"), GenerateSwaggerConfig(ns), ct);
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "Models", "RestErrorResponse.cs"), $"namespace {ns}.Models {{ public sealed class RestErrorResponse {{ public string Error {{ get; set; }} public string Code {{ get; set; }} public string CorrelationId {{ get; set; }} public static RestErrorResponse FromException(System.Exception ex) => new RestErrorResponse {{ Error = \"The upstream WCF service could not complete the request.\", Code = ex is System.TimeoutException ? \"timeout\" : \"wcf-communication\", CorrelationId = System.Guid.NewGuid().ToString(\"N\") }}; }} }}", ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "Wcf", "NetTcpWcfClientOptions.cs"), GenerateRestOptions(ns, options), ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "Wcf", "NetTcpBindingFactory.cs"), _bindingFactoryGenerator.Generate(ns, options).Replace($"namespace {ns}.Binding;", $"namespace {ns}.Wcf;"), ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "Wcf", "WcfClientFactory.cs"), GenerateWcfFactory(ns, metadata), ct);
        foreach (var contract in metadata.Contracts) { var result = await _restControllerGenerator.GenerateAsync(contract, ns, options, options.MethodDocumentationProvider ?? _fallbackDocumentationProvider, ct); diagnostics.AddRange(result.Diagnostics); await File.WriteAllTextAsync(Path.Combine(directory, "Controllers", $"{CSharpIdentifierSanitizer.SanitizeTypeName(contract.ContractName)}Controller.cs"), result.Source, ct); }
        diagnostics.AddRange(await FormatGeneratedCSharpFilesAsync(directory, ct).ConfigureAwait(false));
        await File.WriteAllTextAsync(Path.Combine(directory, "README.md"), GenerateRestReadme(ns, metadata, options.EnableSwagger), ct);
        return diagnostics;
    }

    private static string GenerateWebConfig(ClientLibraryGenerationOptions options)
    {
        var certificate = string.Equals(options.TcpTransportClientCredentialType, "Certificate", StringComparison.OrdinalIgnoreCase) || string.Equals(options.MessageClientCredentialType, "Certificate", StringComparison.OrdinalIgnoreCase);
        var values = new Dictionary<string, string> { ["Wcf:EndpointUrl"] = ResolveRuntimeEndpoint(options.DiscoveryOptions), ["Wcf:SecurityMode"] = options.SecurityMode, ["Wcf:TcpTransportClientCredentialType"] = options.TcpTransportClientCredentialType, ["Wcf:MessageClientCredentialType"] = options.MessageClientCredentialType, ["Wcf:ReliableSessionEnabled"] = options.ReliableSessionEnabled.ToString().ToLowerInvariant(), ["Wcf:OpenTimeout"] = options.OpenTimeout, ["Wcf:CloseTimeout"] = options.CloseTimeout, ["Wcf:SendTimeout"] = options.SendTimeout, ["Wcf:ReceiveTimeout"] = options.ReceiveTimeout, ["Wcf:MaxReceivedMessageSize"] = options.MaxReceivedMessageSize };
        if (certificate) { values["Wcf:ClientCertificateSource"] = options.ClientCertificateSource; values["Wcf:ClientCertificateStoreLocation"] = options.ClientCertificateStoreLocation; values["Wcf:ClientCertificateStoreName"] = options.ClientCertificateStoreName; values["Wcf:ClientCertificateFindType"] = options.ClientCertificateFindType; values["Wcf:ClientCertificateFindValue"] = options.ClientCertificateFindValue; values["Wcf:ClientCertificateFilePath"] = options.ClientCertificateFilePath; values["Wcf:ClientCertificateFilePasswordSource"] = options.ClientCertificateFilePasswordSource; values["Wcf:ClientCertificateFilePasswordEnvironmentVariableName"] = options.ClientCertificateFilePasswordEnvironmentVariableName; values["Wcf:ClientCertificateFilePasswordAppSettingName"] = options.ClientCertificateFilePasswordAppSettingName; }
        var settings = string.Join(string.Empty, values.Select(pair => $"<add key=\"{System.Security.SecurityElement.Escape(pair.Key)}\" value=\"{System.Security.SecurityElement.Escape(pair.Value)}\"/>"));
        return "<?xml version=\"1.0\"?><configuration><appSettings>" + settings + "</appSettings><system.web><compilation debug=\"true\" targetFramework=\"4.8.1\"/><httpRuntime targetFramework=\"4.8.1\"/></system.web><runtime><assemblyBinding xmlns=\"urn:schemas-microsoft-com:asm.v1\"><dependentAssembly><assemblyIdentity name=\"System.Net.Http.Formatting\" publicKeyToken=\"31bf3856ad364e35\" culture=\"neutral\"/><bindingRedirect oldVersion=\"0.0.0.0-5.2.9.0\" newVersion=\"5.2.9.0\"/></dependentAssembly><dependentAssembly><assemblyIdentity name=\"System.Web.Http\" publicKeyToken=\"31bf3856ad364e35\" culture=\"neutral\"/><bindingRedirect oldVersion=\"0.0.0.0-5.2.9.0\" newVersion=\"5.2.9.0\"/></dependentAssembly><dependentAssembly><assemblyIdentity name=\"Newtonsoft.Json\" publicKeyToken=\"30ad4fe6b2a6aeed\" culture=\"neutral\"/><bindingRedirect oldVersion=\"0.0.0.0-13.0.0.0\" newVersion=\"13.0.0.0\"/></dependentAssembly></assemblyBinding></runtime><system.webServer><modules runAllManagedModulesForAllRequests=\"true\"/><handlers><remove name=\"ExtensionlessUrlHandler-Integrated-4.0\"/><add name=\"ExtensionlessUrlHandler-Integrated-4.0\" path=\"*.\" verb=\"*\" type=\"System.Web.Handlers.TransferRequestHandler\" preCondition=\"integratedMode,runtimeVersionv4.0\"/></handlers></system.webServer></configuration>";
    }

    private static string ResolveRuntimeEndpoint(WcfMetadataDiscoveryOptions discoveryOptions)
    {
        if (!string.IsNullOrWhiteSpace(discoveryOptions.ServiceEndpointUrl))
        {
            return discoveryOptions.ServiceEndpointUrl.Trim();
        }

        var metadataEndpoint = discoveryOptions.MetadataEndpointUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(metadataEndpoint) && Uri.TryCreate(metadataEndpoint, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath;
            if (path.EndsWith("/mex", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new UriBuilder(uri) { Path = path[..^4], Query = string.Empty };
                return builder.Uri.AbsoluteUri.TrimEnd('/');
            }
        }

        return metadataEndpoint ?? "net.tcp://server:808/MyService";
    }

    private static async Task<IReadOnlyList<GenerationDiagnostic>> FormatGeneratedCSharpFilesAsync(string directory, CancellationToken cancellationToken)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        foreach (var filePath in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var formatted = GeneratedCodeFormatter.FormatCSharp(await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false));
            if (formatted.Source is null)
            {
                diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Error, $"Generated C# syntax validation failed for {Path.GetFileName(filePath)}: {string.Join(" ", formatted.Errors)}", "GENERATED_CSHARP_SYNTAX_ERROR"));
                continue;
            }

            await File.WriteAllTextAsync(filePath, formatted.Source, cancellationToken).ConfigureAwait(false);
        }

        return diagnostics;
    }

    internal static async Task<IReadOnlyList<GenerationDiagnostic>> WriteFormattedXmlAsync(string filePath, string xml, CancellationToken cancellationToken)
    {
        var formatted = GeneratedXmlFormatter.Format(xml);
        if (formatted.Xml is null)
        {
            return [new GenerationDiagnostic(DiagnosticSeverity.Error, $"Generated XML syntax validation failed for {Path.GetFileName(filePath)}: {formatted.Error}", "GENERATED_XML_SYNTAX_ERROR")];
        }

        await File.WriteAllTextAsync(filePath, formatted.Xml, cancellationToken).ConfigureAwait(false);
        return [];
    }
    private static string GenerateRestReadme(string ns, WcfServiceMetadataModel metadata, bool enableSwagger)
    {
        var routes = metadata.Contracts.SelectMany(c => c.Operations.Select(o => "- POST /api/" + RestControllerGenerator.ToRoute(c.ContractName.Replace("Service", "")) + "/" + RestControllerGenerator.ToRoute(o.OperationName) + " -> " + c.ClientClassName + "." + o.ProxyMethodName));
        var swagger = enableSwagger ? "\n\n## Swagger\n\n- UI: `/swagger` or `/swagger/ui/index`\n- OpenAPI JSON: `/swagger/docs/v1`" : string.Empty;
        return $"# {ns}\n\n.NET Framework 4.8.1 ASP.NET Web API 2 REST API wrapper for a WCF net.tcp service. Build the web application in Visual Studio and deploy it to IIS.\n\n## Configuration\n\nConfigure these `Web.config` app settings; do not store passwords or other secrets there:\n\n- `Wcf:EndpointUrl`\n- `Wcf:SecurityMode`\n- `Wcf:TcpTransportClientCredentialType`\n- `Wcf:MessageClientCredentialType`\n- `Wcf:ReliableSessionEnabled`\n- `Wcf:OpenTimeout`, `Wcf:CloseTimeout`, `Wcf:SendTimeout`, and `Wcf:ReceiveTimeout`\n- `Wcf:MaxReceivedMessageSize`\n\n## Client certificates\n\nFor Windows-store certificates, configure `Wcf:ClientCertificateSource=Store` plus store location, store name, find type, and find value. Prefer `FindByThumbprint`. For files, use `Wcf:ClientCertificateSource=File` and `Wcf:ClientCertificateFilePath`. Set a password through `Wcf:ClientCertificateFilePasswordEnvironmentVariableName` or a protected deployment app setting; never commit certificate files or passwords.\n\n## Routes\n" + string.Join("\n", routes) + swagger;
    }
    private static string GenerateSwaggerConfig(string ns) => $"using System.Web.Http; using WebActivatorEx; using Swashbuckle.Application; [assembly: PreApplicationStartMethod(typeof({ns}.App_Start.SwaggerConfig), \"Register\")] namespace {ns}.App_Start {{ public static class SwaggerConfig {{ public static void Register() {{ GlobalConfiguration.Configuration.EnableSwagger(c => {{ c.SingleApiVersion(\"v1\", \"{ns} REST API\"); c.DescribeAllEnumsAsStrings(); c.IncludeXmlComments(GetXmlCommentsPath()); }}).EnableSwaggerUi(c => {{ }}); }} private static string GetXmlCommentsPath() {{ return System.String.Format(\"{{0}}\\\\bin\\\\{{1}}.XML\", System.AppDomain.CurrentDomain.BaseDirectory, \"{ns}\"); }} }} }}";
    private static string GenerateWebApiConfig(string ns) => $"using System.Web.Http; namespace {ns}.App_Start {{ public static class WebApiConfig {{ public static void Register(HttpConfiguration config) {{ config.MapHttpAttributeRoutes(); config.Routes.MapHttpRoute(\"DefaultApi\", \"api/{{controller}}/{{action}}/{{id}}\", new {{ id = RouteParameter.Optional }}); config.Formatters.Remove(config.Formatters.XmlFormatter); }} }} }}";
    private static string GenerateRestOptions(string ns, ClientLibraryGenerationOptions options) => $"using System; using System.Configuration; namespace {ns}.Wcf {{ public sealed class NetTcpWcfClientOptions {{ public string EndpointUrl {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:EndpointUrl\"]; public string SecurityMode {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:SecurityMode\"] ?? \"Transport\"; public string TcpTransportClientCredentialType {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:TcpTransportClientCredentialType\"] ?? \"Windows\"; public string MessageClientCredentialType {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:MessageClientCredentialType\"] ?? \"None\"; public bool ReliableSessionEnabled {{ get; set; }} = bool.TryParse(ConfigurationManager.AppSettings[\"Wcf:ReliableSessionEnabled\"], out var reliableSessionEnabled) && reliableSessionEnabled; public TimeSpan OpenTimeout {{ get; set; }} = TimeSpan.Parse(ConfigurationManager.AppSettings[\"Wcf:OpenTimeout\"] ?? \"00:00:30\"); public TimeSpan CloseTimeout {{ get; set; }} = TimeSpan.Parse(ConfigurationManager.AppSettings[\"Wcf:CloseTimeout\"] ?? \"00:00:30\"); public TimeSpan SendTimeout {{ get; set; }} = TimeSpan.Parse(ConfigurationManager.AppSettings[\"Wcf:SendTimeout\"] ?? \"00:01:40\"); public TimeSpan ReceiveTimeout {{ get; set; }} = TimeSpan.Parse(ConfigurationManager.AppSettings[\"Wcf:ReceiveTimeout\"] ?? \"00:01:40\"); public long MaxReceivedMessageSize {{ get; set; }} = long.Parse(ConfigurationManager.AppSettings[\"Wcf:MaxReceivedMessageSize\"] ?? \"65536\"); public string ClientCertificateSource {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:ClientCertificateSource\"] ?? \"Store\"; public string ClientCertificateStoreLocation {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:ClientCertificateStoreLocation\"] ?? \"CurrentUser\"; public string ClientCertificateStoreName {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:ClientCertificateStoreName\"] ?? \"My\"; public string ClientCertificateFindType {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:ClientCertificateFindType\"] ?? \"FindByThumbprint\"; public string ClientCertificateFindValue {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:ClientCertificateFindValue\"] ?? string.Empty; public string ClientCertificateFilePath {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:ClientCertificateFilePath\"] ?? string.Empty; public string ClientCertificateFilePasswordSource {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:ClientCertificateFilePasswordSource\"] ?? \"EnvironmentVariable\"; public string ClientCertificateFilePasswordEnvironmentVariableName {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:ClientCertificateFilePasswordEnvironmentVariableName\"] ?? string.Empty; public string ClientCertificateFilePasswordAppSettingName {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:ClientCertificateFilePasswordAppSettingName\"] ?? string.Empty; public string Username {{ get; set; }} public string Password {{ get; set; }} }} }}";
    private static string GenerateWcfFactory(string ns, WcfServiceMetadataModel metadata)
    {
        var b = new StringBuilder($"using System; using System.Configuration; using System.ServiceModel; using System.Security.Cryptography.X509Certificates; using ServiceReference = {metadata.ServiceNamespace}; namespace {ns}.Wcf {{ public sealed class WcfClientFactory {{ private readonly NetTcpWcfClientOptions _options = new NetTcpWcfClientOptions(); ");
        foreach (var c in metadata.Contracts) b.Append($"public ServiceReference.{c.ClientClassName} Create{CSharpIdentifierSanitizer.SanitizeTypeName(c.ClientClassName)}() {{ var client = new ServiceReference.{c.ClientClassName}(NetTcpBindingFactory.Create(_options), new EndpointAddress(_options.EndpointUrl)); ApplyClientCertificate(client, _options); if (_options.MessageClientCredentialType == \"UserName\" && !string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password)) {{ client.ClientCredentials.UserName.UserName = _options.Username; client.ClientCredentials.UserName.Password = _options.Password; }} return client; }} ");
        return b.Append("public static void ApplyClientCertificate<T>(ClientBase<T> client, NetTcpWcfClientOptions options) where T : class { if (options.TcpTransportClientCredentialType != \"Certificate\" && options.MessageClientCredentialType != \"Certificate\") return; if (options.ClientCertificateSource == \"Store\") client.ClientCredentials.ClientCertificate.SetCertificate(ParseStoreLocation(options.ClientCertificateStoreLocation), ParseStoreName(options.ClientCertificateStoreName), ParseFindType(options.ClientCertificateFindType), NormalizeThumbprint(options.ClientCertificateFindValue)); else client.ClientCredentials.ClientCertificate.Certificate = LoadClientCertificate(options); } public static X509Certificate2 LoadClientCertificate(NetTcpWcfClientOptions options) { var password = options.ClientCertificateFilePasswordSource == \"EnvironmentVariable\" ? Environment.GetEnvironmentVariable(options.ClientCertificateFilePasswordEnvironmentVariableName) : ConfigurationManager.AppSettings[options.ClientCertificateFilePasswordAppSettingName]; return new X509Certificate2(options.ClientCertificateFilePath, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet); } public static StoreLocation ParseStoreLocation(string value) => (StoreLocation)Enum.Parse(typeof(StoreLocation), value, true); public static StoreName ParseStoreName(string value) => (StoreName)Enum.Parse(typeof(StoreName), value, true); public static X509FindType ParseFindType(string value) => (X509FindType)Enum.Parse(typeof(X509FindType), value, true); public static string NormalizeThumbprint(string value) => (value ?? string.Empty).Replace(\" \", string.Empty).Replace(\"\\u200e\", string.Empty).Trim(); public void CloseOrAbort(ICommunicationObject client) { try { if (client.State != CommunicationState.Faulted) client.Close(); else client.Abort(); } catch { client.Abort(); } } } }").ToString();
    }
    /*
    private static string GenerateWebApiConfig(string ns) => $"using System.Web.Http; namespace {ns}.App_Start {{ public static class WebApiConfig {{ public static void Register(HttpConfiguration config) {{ config.MapHttpAttributeRoutes(); config.Routes.MapHttpRoute(\"DefaultApi\", \"api/{{controller}}/{{action}}/{{id}}\", new {{ id = RouteParameter.Optional }}); config.Formatters.Remove(config.Formatters.XmlFormatter); }} }} }}";
    private static string GenerateRestOptions(string ns) => $"using System; using System.Configuration; namespace {ns}.Wcf {{ public sealed class NetTcpWcfClientOptions {{ public string EndpointUrl {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:EndpointUrl\"]; public string SecurityMode {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:SecurityMode\"] ?? \"Transport\"; public string TcpClientCredentialType {{ get; set; }} = ConfigurationManager.AppSettings[\"Wcf:TcpClientCredentialType\"] ?? \"Windows\"; public TimeSpan OpenTimeout {{ get; set; }} = TimeSpan.Parse(ConfigurationManager.AppSettings[\"Wcf:OpenTimeout\"] ?? \"00:00:30\"); public TimeSpan SendTimeout {{ get; set; }} = TimeSpan.Parse(ConfigurationManager.AppSettings[\"Wcf:SendTimeout\"] ?? \"00:01:40\"); public TimeSpan ReceiveTimeout {{ get; set; }} = TimeSpan.Parse(ConfigurationManager.AppSettings[\"Wcf:ReceiveTimeout\"] ?? \"00:01:40\"); public long MaxReceivedMessageSize {{ get; set; }} = long.Parse(ConfigurationManager.AppSettings[\"Wcf:MaxReceivedMessageSize\"] ?? \"65536\"); public string Username {{ get; set; }} public string Password {{ get; set; }} }} }}";
    private static string GenerateWcfFactory(string ns, WcfServiceMetadataModel metadata) { var b = new StringBuilder($"using System; using System.ServiceModel; using ServiceReference = {metadata.ServiceNamespace}; namespace {ns}.Wcf {{ public sealed class WcfClientFactory {{ private readonly NetTcpWcfClientOptions _options = new NetTcpWcfClientOptions(); "); foreach (var c in metadata.Contracts) b.Append($"public ServiceReference.{c.ClientClassName} Create{CSharpIdentifierSanitizer.SanitizeTypeName(c.ClientClassName)}() {{ var client = new ServiceReference.{c.ClientClassName}(NetTcpBindingFactory.Create(_options), new EndpointAddress(_options.EndpointUrl)); if (_options.TcpClientCredentialType == \"UserName\" && !string.IsNullOrWhiteSpace(_options.Username)) {{ client.ClientCredentials.UserName.UserName = _options.Username; client.ClientCredentials.UserName.Password = _options.Password; }} return client; }} "); return b.Append("public void CloseOrAbort(ICommunicationObject client) { try { if (client.State != CommunicationState.Faulted) client.Close(); else client.Abort(); } catch { client.Abort(); } } } }").ToString(); }
    private static string GenerateRestReadme(string ns, WcfServiceMetadataModel metadata) => $"# {ns}\n\n.NET Framework 4.8 ASP.NET Web API 2 wrapper for WCF net.tcp. Configure `Wcf:EndpointUrl` and security settings in Web.config; do not store passwords there. Build in Visual Studio and deploy the IIS application pool as .NET CLR v4.0.\n\n## Routes\n" + string.Join("\n", metadata.Contracts.SelectMany(c => c.Operations.Select(o => $"- POST /api/{RestControllerGenerator.ToRoute(c.ContractName.Replace(\"Service\", \"\"))}/{RestControllerGenerator.ToRoute(o.OperationName)} → {c.ClientClassName}.{o.ProxyMethodName}")));

    */
    private async Task<IReadOnlyList<GenerationDiagnostic>> WriteLibraryAsync(
        string libraryDirectory,
        string libraryNamespace,
        WcfServiceMetadataModel metadata,
        ClientLibraryGenerationOptions options,
        string projectFilePath,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<GenerationDiagnostic>();
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

        diagnostics.AddRange(await WriteFormattedXmlAsync(projectFilePath, _projectFileGenerator.Generate(libraryNamespace, options), cancellationToken).ConfigureAwait(false));
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

            var implementationResult = await _implementationGenerator.GenerateAsync(
                contract,
                libraryNamespace,
                options,
                options.MethodDocumentationProvider ?? _fallbackDocumentationProvider,
                cancellationToken).ConfigureAwait(false);

            diagnostics.AddRange(implementationResult.Diagnostics);

            await File.WriteAllTextAsync(
                Path.Combine(servicesDirectory, $"{contractTypeName}Client.cs"),
                implementationResult.Source,
                cancellationToken).ConfigureAwait(false);
        }

        return diagnostics;
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
