using System.Xml.Linq;
using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.Tests;

[TestFixture]
public sealed class GeneratedXmlFormatterTests
{
    private static readonly XNamespace AssemblyBinding = "urn:schemas-microsoft-com:asm.v1";

    [Test]
    public void Format_IndentsAndPreservesConfigurationContent()
    {
        var result = GeneratedXmlFormatter.Format("<?xml version=\"1.0\"?><configuration><appSettings><add key=\"Wcf:EndpointUrl\" value=\"net.tcp://localhost:9001/PatientProcessing\"/></appSettings><runtime><assemblyBinding xmlns=\"urn:schemas-microsoft-com:asm.v1\"><dependentAssembly><assemblyIdentity name=\"System.Web.Http\" publicKeyToken=\"31bf3856ad364e35\"/><bindingRedirect oldVersion=\"0.0.0.0-5.2.9.0\" newVersion=\"5.2.9.0\"/></dependentAssembly></assemblyBinding></runtime></configuration>");

        Assert.That(result.Xml, Is.Not.Null.And.Contains(Environment.NewLine));
        var document = XDocument.Parse(result.Xml!);
        Assert.Multiple(() =>
        {
            Assert.That(document.Root?.Element("appSettings"), Is.Not.Null);
            Assert.That(document.Descendants().Attributes("value").Any(attribute => attribute.Value == "net.tcp://localhost:9001/PatientProcessing"), Is.True);
            Assert.That(document.Descendants().Attributes("newVersion").Any(attribute => attribute.Value == "5.2.9.0"), Is.True);
        });
    }

    [Test]
    public void Format_InvalidXmlReturnsFailure()
    {
        var result = GeneratedXmlFormatter.Format("<configuration><appSettings></configuration>");

        Assert.That(result.Xml, Is.Null);
        Assert.That(result.Error, Is.Not.Empty);
    }

    [Test]
    public async Task WriteFormattedXmlAsync_InvalidXmlReturnsDiagnosticWithoutWritingFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.config");

        var diagnostics = await ClientLibraryGenerator.WriteFormattedXmlAsync(filePath, "<configuration><appSettings></configuration>", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Has.One.Matches<GenerationDiagnostic>(diagnostic => diagnostic.Code == "GENERATED_XML_SYNTAX_ERROR"));
            Assert.That(diagnostics[0].Message, Does.Contain(Path.GetFileName(filePath)));
            Assert.That(File.Exists(filePath), Is.False);
        });
    }

    [Test]
    public async Task RestWrapperGeneration_FormatsWebConfigAndProjectFileWithoutChangingXmlContent()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "WcfNetTcpClientGenerator.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);

        try
        {
            var result = await CreateGenerator().GenerateAsync(new ClientLibraryGenerationOptions
            {
                OutputKind = GeneratedOutputKind.NetFramework48RestApiWrapper,
                OutputFolder = outputRoot,
                GeneratedLibraryName = "ContosoRest",
                EnableSwagger = true,
                ExistingProxyCode = SampleProxyCode,
                DiscoveryOptions = new WcfMetadataDiscoveryOptions { ServiceEndpointUrl = "net.tcp://localhost:9001/PatientProcessing" }
            }, CancellationToken.None);

            Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var webConfigText = await File.ReadAllTextAsync(Path.Combine(result.OutputDirectory!, "Web.config"));
            var projectText = await File.ReadAllTextAsync(result.ProjectFilePath!);
            var webConfig = XDocument.Parse(webConfigText);
            var project = XDocument.Parse(projectText);
            var msBuild = (XNamespace)"http://schemas.microsoft.com/developer/msbuild/2003";

            Assert.Multiple(() =>
            {
                Assert.That(webConfigText, Does.Contain(Environment.NewLine));
                Assert.That(projectText, Does.Contain(Environment.NewLine));
                Assert.That(webConfigText, Does.Contain(Environment.NewLine + "    <add key=\"Wcf:EndpointUrl\""));
                Assert.That(projectText, Does.Contain(Environment.NewLine + "    <TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion>"));
                Assert.That(webConfig.Root?.Element("appSettings"), Is.Not.Null);
                Assert.That(AppSetting(webConfig, "Wcf:EndpointUrl"), Is.EqualTo("net.tcp://localhost:9001/PatientProcessing"));
                Assert.That(AppSetting(webConfig, "Wcf:SecurityMode"), Is.EqualTo("Transport"));
                Assert.That(AppSetting(webConfig, "Wcf:ReliableSessionEnabled"), Is.EqualTo("false"));
                Assert.That(BindingRedirectVersion(webConfig, "System.Net.Http.Formatting"), Is.EqualTo("5.2.9.0"));
                Assert.That(BindingRedirectVersion(webConfig, "System.Web.Http"), Is.EqualTo("5.2.9.0"));
                Assert.That(BindingRedirectVersion(webConfig, "Newtonsoft.Json"), Is.EqualTo("13.0.0.0"));
                Assert.That(project.Root?.Element(msBuild + "PropertyGroup")?.Element(msBuild + "TargetFrameworkVersion")?.Value, Is.EqualTo("v4.8.1"));
                Assert.That(project.Root?.Elements(msBuild + "PropertyGroup").Any(group => ((string?)group.Attribute("Condition"))?.Contains("Debug|AnyCPU", StringComparison.Ordinal) == true), Is.True);
                Assert.That(project.Root?.Elements(msBuild + "PropertyGroup").Any(group => ((string?)group.Attribute("Condition"))?.Contains("Release|AnyCPU", StringComparison.Ordinal) == true), Is.True);
                Assert.That(project.Descendants(msBuild + "PackageReference").Select(element => (string?)element.Attribute("Include")), Does.Contain("Swashbuckle.Core").And.Contain("WebActivatorEx"));
            });
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static string? AppSetting(XDocument document, string key)
        => document.Root?.Element("appSettings")?.Elements("add").SingleOrDefault(element => (string?)element.Attribute("key") == key)?.Attribute("value")?.Value;

    private static string? BindingRedirectVersion(XDocument document, string assemblyName)
        => document.Descendants(AssemblyBinding + "dependentAssembly")
            .SingleOrDefault(element => (string?)element.Element(AssemblyBinding + "assemblyIdentity")?.Attribute("name") == assemblyName)?
            .Element(AssemblyBinding + "bindingRedirect")?.Attribute("newVersion")?.Value;

    private static ClientLibraryGenerator CreateGenerator()
    {
        var proxyGenerator = new ProxyCodeGenerator(new DotNetSvcUtilRunner());
        return new ClientLibraryGenerator(
            new WcfMetadataReader(proxyGenerator), proxyGenerator, new WrapperInterfaceGenerator(), new WrapperImplementationGenerator(),
            new NetTcpBindingFactoryGenerator(), new ProjectFileGenerator(), new NullMethodDocumentationProvider());
    }

    private const string SampleProxyCode = """
namespace Contoso.Generated
{
    [global::System.ServiceModel.ServiceContractAttribute()]
    public interface IPatientService
    {
        global::System.Threading.Tasks.Task<PatientResponse> GetPatientAsync(PatientRequest request);
    }

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
