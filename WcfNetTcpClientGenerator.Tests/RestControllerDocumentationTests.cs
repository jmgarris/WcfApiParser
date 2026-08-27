using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.Tests;

[TestFixture]
public sealed class RestControllerDocumentationTests
{
    [TestCase("<summary>Gets a patient.</summary>")]
    [TestCase("<summary>Gets a patient.\nWith complete details.</summary>")]
    [TestCase("/// <summary>\n/// Gets a patient.\n/// </summary>")]
    [TestCase("```xml\n<summary>Gets a patient.</summary>\n```")]
    public async Task GenerateAsync_NormalizesProviderDocumentation(string documentation)
    {
        var source = await GenerateSourceAsync(documentation);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("    /// <summary>"));
            Assert.That(source, Does.Contain("Gets a patient."));
            Assert.That(source, Does.Not.Contain("```"));
            Assert.That(source, Does.Not.Contain("\n<summary>"));
            Assert.That(source, Does.Not.Contain("\n/// ///"));
        });
    }

    [Test]
    public async Task GenerateAsync_UsesProviderParamAndReturnsWithoutDuplicates()
    {
        var source = await GenerateSourceAsync("""
            <summary>Gets a patient.</summary>
            <param name="patientId">The patient identifier.</param>
            <returns>The matching patient.</returns>
            """);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("/// <param name=\"patientId\">The patient identifier.</param>"));
            Assert.That(source, Does.Contain("/// <returns>The matching patient.</returns>"));
            Assert.That(CountOccurrences(source, "<param name=\"patientId\">"), Is.EqualTo(1));
            Assert.That(CountOccurrences(source, "<returns>"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GenerateAsync_UsesFallbackForEmptyOrMalformedProviderDocumentation()
    {
        var emptySource = await GenerateSourceAsync("   ");
        var malformedSource = await GenerateSourceAsync("<summary>Incomplete");

        Assert.Multiple(() =>
        {
            Assert.That(emptySource, Does.Contain("Calls the GetPatient WCF operation"));
            Assert.That(malformedSource, Does.Contain("Calls the GetPatient WCF operation"));
            Assert.That(malformedSource, Does.Not.Contain("<summary>Incomplete"));
        });
    }

    [Test]
    public async Task GenerateAsync_DoesNotAllowProviderTextToInjectCSharp()
    {
        var source = await GenerateSourceAsync("<summary>Gets a patient.</summary>\npublic int Injected => 1;");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("Calls the GetPatient WCF operation"));
            Assert.That(source, Does.Not.Contain("public int Injected"));
        });
    }

    private static async Task<string> GenerateSourceAsync(string documentation)
    {
        var result = await new RestControllerGenerator().GenerateAsync(
            new WcfContractModel
            {
                ContractName = "Patient",
                ClientClassName = "PatientClient",
                Operations =
                [
                    new WcfOperationModel
                    {
                        OperationName = "GetPatient",
                        MethodName = "GetPatient",
                        ProxyMethodName = "GetPatientAsync",
                        ResponseTypeName = "global::Generated.Wcf.PatientDto",
                        Parameters = [new WcfParameterModel { Name = "patientId", TypeName = "global::System.Guid" }]
                    }
                ]
            },
            "Generated.Rest",
            new ClientLibraryGenerationOptions(),
            new StaticDocumentationProvider(documentation),
            CancellationToken.None);

        return result.Source;
    }

    private static int CountOccurrences(string value, string match)
        => value.Split(match, StringSplitOptions.None).Length - 1;

    private sealed class StaticDocumentationProvider(string documentation) : IMethodDocumentationProvider
    {
        public Task<MethodDocumentationResult> GenerateDocumentationAsync(
            MethodDocumentationRequest request,
            MethodDocumentationOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(new MethodDocumentationResult { Success = true, XmlDocumentationText = documentation });
    }
}
