using System.Xml.Linq;
using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.Tests;

[TestFixture]
public sealed class RestWrapperProjectFileGeneratorTests
{
    private static readonly XNamespace MsBuild = "http://schemas.microsoft.com/developer/msbuild/2003";

    [Test]
    public void Generate_EmitsBuildableNet481WebApiWrapperProject()
    {
        var metadata = new WcfServiceMetadataModel
        {
            ServiceNamespace = "Contoso.Generated",
            Contracts = [new WcfContractModel { ContractName = "Patient" }]
        };
        var project = new ProjectFileGenerator().Generate(
            "Contoso.Rest",
            new ClientLibraryGenerationOptions
            {
                OutputKind = GeneratedOutputKind.NetFramework48RestApiWrapper,
                EnableSwagger = true
            },
            metadata);
        var document = XDocument.Parse(project);

        Assert.That(document.Root?.Element(MsBuild + "PropertyGroup")?.Element(MsBuild + "TargetFrameworkVersion")?.Value, Is.EqualTo("v4.8.1"));
        AssertConfiguration(document, "Debug|AnyCPU", "bin\\");
        AssertConfiguration(document, "Release|AnyCPU", "bin\\");

        var references = document.Descendants(MsBuild + "Reference").Select(element => (string?)element.Attribute("Include"));
        var packages = document.Descendants(MsBuild + "PackageReference").Select(element => (string?)element.Attribute("Include"));
        var compiledFiles = document.Descendants(MsBuild + "Compile").Select(element => (string?)element.Attribute("Include"));

        Assert.Multiple(() =>
        {
            Assert.That(packages, Does.Contain("Microsoft.AspNet.WebApi.Core"));
            Assert.That(packages, Does.Contain("Microsoft.AspNet.WebApi.WebHost"));
            Assert.That(packages, Does.Contain("Swashbuckle.Core"));
            Assert.That(packages, Does.Contain("WebActivatorEx"));
            Assert.That(references, Does.Contain("System.ServiceModel"));
            Assert.That(compiledFiles, Does.Contain("ServiceReferences\\GeneratedProxy.cs"));
            Assert.That(compiledFiles, Does.Contain("Controllers\\PatientController.cs"));
        });
    }

    private static void AssertConfiguration(XDocument document, string configuration, string expectedOutputPath)
    {
        var group = document.Root!
            .Elements(MsBuild + "PropertyGroup")
            .SingleOrDefault(element => ((string?)element.Attribute("Condition"))?.Contains(configuration, StringComparison.Ordinal) == true);

        Assert.That(group, Is.Not.Null, $"Missing {configuration} configuration.");
        Assert.That(group!.Element(MsBuild + "OutputPath")?.Value, Is.EqualTo(expectedOutputPath));
    }
}
