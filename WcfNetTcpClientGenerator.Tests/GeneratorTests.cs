using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.Tests;

[TestFixture]
public sealed class GeneratorTests
{
    [Test]
    public void ValidNetTcpEndpoint_IsAccepted()
    {
        Assert.That(WcfEndpointValidator.IsValidNetTcpEndpoint("net.tcp://server:808/MyService"), Is.True);
        Assert.That(WcfEndpointValidator.IsValidNetTcpEndpoint("https://server/service"), Is.False);
    }

    [Test]
    public void ValidMetadataUrl_IsAccepted()
    {
        Assert.That(WcfEndpointValidator.IsValidMetadataUrl("http://server:808/MyService/mex"), Is.True);
        Assert.That(WcfEndpointValidator.IsValidMetadataUrl("net.tcp://server:808/MyService/mex"), Is.True);
        Assert.That(WcfEndpointValidator.IsValidMetadataUrl("ftp://server/service"), Is.False);
    }

    [Test]
    public void Sanitizer_ProducesDeterministicIdentifiers()
    {
        Assert.That(CSharpIdentifierSanitizer.SanitizeTypeName("123 customer-service"), Is.EqualTo("_123_customer_service"));
        Assert.That(CSharpIdentifierSanitizer.SanitizeMemberName("class"), Is.EqualTo("class_"));
    }

    [Test]
    public void DuplicateOperations_AreRenamed()
    {
        var parseResult = ProxyCodeParser.Parse(SampleProxyCodeWithDuplicateOperations, "Generated.Wcf");

        var contract = parseResult.Metadata!.Contracts.Single();
        Assert.That(contract.Operations.Select(static operation => operation.MethodName), Is.EqualTo(new[] { "GetCustomer", "GetCustomer2" }));
        Assert.That(parseResult.Diagnostics.Any(static diagnostic => diagnostic.Code == "DUPLICATE_OPERATION_NAME"), Is.True);
    }

    [Test]
    public void ProjectFileGenerator_EmitsNuGetMetadata()
    {
        var generator = new ProjectFileGenerator();
        var projectFile = generator.Generate(
            "Contoso.CustomerClient",
            new ClientLibraryGenerationOptions
            {
                PackageId = "Contoso.CustomerClient",
                PackageVersion = "2.5.0",
                Authors = "Contoso",
                Company = "Contoso Ltd",
                Description = "Generated customer client.",
                RepositoryUrl = "https://example.test/repo"
            });

        Assert.Multiple(() =>
        {
            Assert.That(projectFile, Does.Contain("<PackageId>Contoso.CustomerClient</PackageId>"));
            Assert.That(projectFile, Does.Contain("<Version>2.5.0</Version>"));
            Assert.That(projectFile, Does.Contain("<GeneratePackageOnBuild>true</GeneratePackageOnBuild>"));
            Assert.That(projectFile, Does.Contain("System.ServiceModel.NetTcp"));
        });
    }

    [Test]
    public void WrapperInterfaceGenerator_EmitsAsyncMethods()
    {
        var generator = new WrapperInterfaceGenerator();
        var source = generator.Generate(CreateMetadata().Contracts.Single(), "Contoso.CustomerClient");

        Assert.That(source, Does.Contain("Task<global::Contoso.Contracts.CustomerResponse> GetCustomer("));
        Assert.That(source, Does.Contain("CancellationToken cancellationToken = default"));
    }

    [Test]
    public void WrapperImplementationGenerator_ClosesOrAbortsClient()
    {
        var generator = new WrapperImplementationGenerator();
        var source = generator.Generate(CreateMetadata().Contracts.Single(), "Contoso.CustomerClient");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("client.Abort();"));
            Assert.That(source, Does.Contain("CloseClient(client);"));
            Assert.That(source, Does.Contain("NetTcpBindingFactory.Create(_options)"));
        });
    }

    [Test]
    public void BindingFactoryGenerator_MapsSecurityModes()
    {
        var generator = new NetTcpBindingFactoryGenerator();
        var source = generator.Generate(
            "Contoso.CustomerClient",
            new ClientLibraryGenerationOptions
            {
                SecurityMode = "Transport",
                TcpClientCredentialType = "Windows"
            });

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("\"Transport\" => global::System.ServiceModel.SecurityMode.Transport"));
            Assert.That(source, Does.Contain("\"TransportWithMessageCredential\" => global::System.ServiceModel.SecurityMode.TransportWithMessageCredential"));
            Assert.That(source, Does.Contain("\"Windows\" => global::System.ServiceModel.TcpClientCredentialType.Windows"));
        });
    }

    [Test]
    public async Task MissingMetadata_ReturnsClearError()
    {
        var reader = new WcfMetadataReader(new ProxyCodeGenerator(new DotNetSvcUtilRunner()));
        var result = await reader.ReadAsync(new WcfMetadataDiscoveryOptions(), CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "NO_METADATA_SOURCE"), Is.True);
    }

    [Test]
    public async Task InvalidOutputPath_IsRejected()
    {
        var generator = CreateClientLibraryGenerator();
        var result = await generator.GenerateAsync(
            new ClientLibraryGenerationOptions
            {
                OutputFolder = string.Empty,
                ExistingProxyCode = SampleProxyCode,
                DiscoveryOptions = new WcfMetadataDiscoveryOptions()
            },
            CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "OUTPUT_FOLDER_REQUIRED"), Is.True);
    }

    [Test]
    public async Task GeneratedProject_CanBePacked()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "WcfNetTcpClientGenerator.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);

        try
        {
            var generator = CreateClientLibraryGenerator();
            var generationResult = await generator.GenerateAsync(
                new ClientLibraryGenerationOptions
                {
                    OutputFolder = outputRoot,
                    GeneratedLibraryName = "ContosoCustomerClient",
                    PackageId = "Contoso.CustomerClient",
                    PackageVersion = "1.0.0",
                    ExistingProxyCode = SampleProxyCode,
                    DiscoveryOptions = new WcfMetadataDiscoveryOptions
                    {
                        ServiceNamespace = "Contoso.Generated"
                    }
                },
                CancellationToken.None);

            Assert.That(generationResult.Success, Is.True, string.Join(Environment.NewLine, generationResult.Diagnostics.Select(static diagnostic => diagnostic.Message)));
            Assert.That(generationResult.ProjectFilePath, Is.Not.Null);

            var packResult = await new NuGetPackageBuilder().BuildAsync(generationResult.ProjectFilePath!, CancellationToken.None);

            Assert.That(packResult.Success, Is.True, string.Join(Environment.NewLine, packResult.Diagnostics.Select(static diagnostic => diagnostic.Message)));
            Assert.That(packResult.PackagePath, Is.Not.Null.And.EndsWith(".nupkg"));
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static ClientLibraryGenerator CreateClientLibraryGenerator()
    {
        var runner = new DotNetSvcUtilRunner();
        var proxyGenerator = new ProxyCodeGenerator(runner);
        var metadataReader = new WcfMetadataReader(proxyGenerator);
        return new ClientLibraryGenerator(
            metadataReader,
            proxyGenerator,
            new WrapperInterfaceGenerator(),
            new WrapperImplementationGenerator(),
            new NetTcpBindingFactoryGenerator(),
            new ProjectFileGenerator());
    }

    private static WcfServiceMetadataModel CreateMetadata()
        => new()
        {
            ServiceNamespace = "Contoso.Generated",
            Contracts =
            [
                new WcfContractModel
                {
                    ContractName = "CustomerService",
                    ClientClassName = "CustomerServiceClient",
                    ProxyNamespace = "Contoso.Generated",
                    Operations =
                    [
                        new WcfOperationModel
                        {
                            OperationName = "GetCustomer",
                            MethodName = "GetCustomer",
                            ProxyMethodName = "GetCustomerAsync",
                            ResponseTypeName = "global::Contoso.Contracts.CustomerResponse",
                            Parameters =
                            [
                                new WcfParameterModel
                                {
                                    Name = "request",
                                    TypeName = "global::Contoso.Contracts.GetCustomerRequest"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

    private const string SampleProxyCode = """
namespace Contoso.Generated
{
    [global::System.ServiceModel.ServiceContractAttribute()]
    public interface ICustomerService
    {
        global::System.Threading.Tasks.Task<global::Contoso.Contracts.CustomerResponse> GetCustomerAsync(global::Contoso.Contracts.GetCustomerRequest request);
    }

    public partial class CustomerServiceClient : global::System.ServiceModel.ClientBase<ICustomerService>, ICustomerService
    {
        public CustomerServiceClient()
        {
        }

        public CustomerServiceClient(global::System.ServiceModel.Channels.Binding binding, global::System.ServiceModel.EndpointAddress address)
            : base(binding, address)
        {
        }

        public global::System.Threading.Tasks.Task<global::Contoso.Contracts.CustomerResponse> GetCustomerAsync(global::Contoso.Contracts.GetCustomerRequest request)
        {
            return global::System.Threading.Tasks.Task.FromResult(new global::Contoso.Contracts.CustomerResponse());
        }
    }
}

namespace Contoso.Contracts
{
    public class GetCustomerRequest
    {
    }

    public class CustomerResponse
    {
    }
}
""";

    private const string SampleProxyCodeWithDuplicateOperations = """
namespace Contoso.Generated
{
    [global::System.ServiceModel.ServiceContractAttribute()]
    public interface ICustomerService
    {
        global::System.Threading.Tasks.Task<global::Contoso.Contracts.CustomerResponse> GetCustomerAsync(global::Contoso.Contracts.GetCustomerRequest request);
        global::System.Threading.Tasks.Task<global::Contoso.Contracts.CustomerResponse> GetCustomerAsync(global::Contoso.Contracts.GetCustomerRequest request, int version);
    }

    public partial class CustomerServiceClient : global::System.ServiceModel.ClientBase<ICustomerService>, ICustomerService
    {
    }
}
""";
}
