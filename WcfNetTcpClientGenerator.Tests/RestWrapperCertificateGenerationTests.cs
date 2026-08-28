using Microsoft.CodeAnalysis.CSharp;
using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.Tests;

[TestFixture]
public sealed class RestWrapperCertificateGenerationTests
{
    [Test]
    public void ValidateOptions_RejectsInvalidCertificateConfiguration()
    {
        var diagnostics = new NetTcpBindingFactoryGenerator().ValidateOptions(new ClientLibraryGenerationOptions
        {
            OutputKind = GeneratedOutputKind.NetFramework48RestApiWrapper,
            TcpTransportClientCredentialType = "Certificate",
            ClientCertificateSource = "Unknown",
            ClientCertificateStoreLocation = "Unknown",
            ClientCertificateStoreName = "Unknown",
            ClientCertificateFindType = "Unknown"
        });

        Assert.That(diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain("UNSUPPORTED_CERTIFICATE_SOURCE"));

        var fileDiagnostics = new NetTcpBindingFactoryGenerator().ValidateOptions(new ClientLibraryGenerationOptions
        {
            OutputKind = GeneratedOutputKind.NetFramework48RestApiWrapper,
            TcpTransportClientCredentialType = "Certificate",
            ClientCertificateSource = "File",
            ClientCertificateFilePath = "certificate.cer",
            ClientCertificateFilePasswordSource = "Unknown"
        });
        Assert.That(fileDiagnostics.Select(diagnostic => diagnostic.Code), Does.Contain("CERTIFICATE_FILE_EXTENSION_INVALID").And.Contain("UNSUPPORTED_CERTIFICATE_PASSWORD_SOURCE"));
    }

    [TestCase("EnvironmentVariable", "", "CERTIFICATE_PASSWORD_ENVIRONMENT_VARIABLE_REQUIRED")]
    [TestCase("AppSettingName", "", "CERTIFICATE_PASSWORD_APPSETTING_REQUIRED")]
    [TestCase("None", "", null)]
    public void ValidateOptions_HandlesCertificatePasswordSources(string passwordSource, string requiredName, string? expectedCode)
    {
        var options = new ClientLibraryGenerationOptions
        {
            OutputKind = GeneratedOutputKind.NetFramework48RestApiWrapper,
            TcpTransportClientCredentialType = "Certificate",
            ClientCertificateSource = "File",
            ClientCertificateFilePath = "certificate.pfx",
            ClientCertificateFilePasswordSource = passwordSource,
            ClientCertificateFilePasswordEnvironmentVariableName = requiredName,
            ClientCertificateFilePasswordAppSettingName = requiredName
        };

        var codes = new NetTcpBindingFactoryGenerator().ValidateOptions(options).Select(diagnostic => diagnostic.Code);
        if (expectedCode is null)
            Assert.That(codes, Does.Not.Contain("CERTIFICATE_PASSWORD_ENVIRONMENT_VARIABLE_REQUIRED").And.Not.Contain("CERTIFICATE_PASSWORD_APPSETTING_REQUIRED"));
        else
            Assert.That(codes, Does.Contain(expectedCode));
    }

    [Test]
    public async Task CertificateWrapper_GeneratesSafeStoreAndFileRuntimeCode()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "WcfNetTcpClientGenerator.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);
        try
        {
            var result = await GenerateAsync(outputRoot, new ClientLibraryGenerationOptions
            {
                OutputKind = GeneratedOutputKind.NetFramework48RestApiWrapper,
                OutputFolder = outputRoot,
                GeneratedLibraryName = "CertificateWrapper",
                ExistingProxyCode = SampleProxyCode,
                TcpTransportClientCredentialType = "Certificate",
                ClientCertificateSource = "Store",
                ClientCertificateStoreLocation = "CurrentUser",
                ClientCertificateStoreName = "My",
                ClientCertificateFindType = "FindBySubjectName",
                ClientCertificateFindValue = "Nevos Client Certificate"
            });

            Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var factory = await File.ReadAllTextAsync(Path.Combine(result.OutputDirectory!, "Wcf", "WcfClientFactory.cs"));
            var config = await File.ReadAllTextAsync(Path.Combine(result.OutputDirectory!, "Web.config"));
            var syntaxErrors = CSharpSyntaxTree.ParseText(factory).GetDiagnostics().Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

            Assert.Multiple(() =>
            {
                Assert.That(factory, Does.Contain("ApplyClientCertificate").And.Contain("SetCertificate(").And.Contain("ParseFindType"));
                Assert.That(factory, Does.Contain("NormalizeFindValue(findType, options.ClientCertificateFindValue)").And.Contain("findType == X509FindType.FindByThumbprint"));
                Assert.That(factory, Does.Contain("(value ?? string.Empty).Trim()"));
                Assert.That(factory, Does.Contain("Replace(\"\\u200e\", string.Empty)"));
                Assert.That(factory, Does.Contain("Client certificate files must be .pfx or .p12.").And.Contain("certificate.HasPrivateKey"));
                Assert.That(factory, Does.Contain("ClientCertificateFilePasswordSource, \"None\""));
                Assert.That(factory, Does.Contain("ClientCertificateFilePasswordSource, \"EnvironmentVariable\""));
                Assert.That(factory, Does.Contain("ClientCertificateFilePasswordSource, \"AppSettingName\""));
                Assert.That(factory, Does.Contain("Unsupported client certificate password source."));
                Assert.That(syntaxErrors, Is.Empty);
                Assert.That(config, Does.Contain("Wcf:ClientCertificateSource").And.Contain("Nevos Client Certificate"));
                Assert.That(config.Contains("password=", StringComparison.OrdinalIgnoreCase), Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Test]
    public async Task NonCertificateWrapper_OmitsCertificateConfiguration()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "WcfNetTcpClientGenerator.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);
        try
        {
            var result = await GenerateAsync(outputRoot, new ClientLibraryGenerationOptions
            {
                OutputKind = GeneratedOutputKind.NetFramework48RestApiWrapper,
                OutputFolder = outputRoot,
                GeneratedLibraryName = "NormalWrapper",
                ExistingProxyCode = SampleProxyCode,
                SecurityMode = "None",
                TcpTransportClientCredentialType = "None",
                MessageClientCredentialType = "None"
            });
            var config = await File.ReadAllTextAsync(Path.Combine(result.OutputDirectory!, "Web.config"));

            Assert.That(result.Success, Is.True);
            Assert.That(config, Does.Not.Contain("Wcf:ClientCertificate"));
        }
        finally
        {
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, recursive: true);
        }
    }

    private static Task<GenerationResult> GenerateAsync(string outputRoot, ClientLibraryGenerationOptions options)
    {
        var proxyGenerator = new ProxyCodeGenerator(new DotNetSvcUtilRunner());
        var generator = new ClientLibraryGenerator(new WcfMetadataReader(proxyGenerator), proxyGenerator, new WrapperInterfaceGenerator(), new WrapperImplementationGenerator(), new NetTcpBindingFactoryGenerator(), new ProjectFileGenerator(), new NullMethodDocumentationProvider());
        return generator.GenerateAsync(options, CancellationToken.None);
    }

    private const string SampleProxyCode = """
namespace Contoso.Generated
{
    [global::System.ServiceModel.ServiceContractAttribute()]
    public interface IPatientService { global::System.Threading.Tasks.Task<PatientResponse> GetPatientAsync(PatientRequest request); }
    public partial class PatientServiceClient : global::System.ServiceModel.ClientBase<IPatientService>, IPatientService
    {
        public PatientServiceClient(global::System.ServiceModel.Channels.Binding binding, global::System.ServiceModel.EndpointAddress address) : base(binding, address) { }
        public global::System.Threading.Tasks.Task<PatientResponse> GetPatientAsync(PatientRequest request) => global::System.Threading.Tasks.Task.FromResult(new PatientResponse());
    }
    public class PatientRequest { }
    public class PatientResponse { }
}
""";
}
