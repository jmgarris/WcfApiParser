using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class ProjectFileGenerator
{
    public string Generate(string libraryName, ClientLibraryGenerationOptions options, WcfServiceMetadataModel? metadata = null)
    {
        if (options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper)
        {
            return GenerateNetFramework48RestApiWrapper(libraryName, options.EnableSwagger, metadata);
        }
        var escapedLibraryName = SecurityElement.Escape(libraryName) ?? libraryName;
        var packageId = SecurityElement.Escape(options.PackageId) ?? options.PackageId;
        var packageVersion = SecurityElement.Escape(options.PackageVersion) ?? options.PackageVersion;
        var authors = SecurityElement.Escape(options.Authors) ?? options.Authors;
        var company = SecurityElement.Escape(options.Company) ?? options.Company;
        var description = SecurityElement.Escape(options.Description) ?? options.Description;
        var repositoryUrl = SecurityElement.Escape(options.RepositoryUrl) ?? options.RepositoryUrl;

        var builder = new StringBuilder();
        builder.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        builder.AppendLine();
        builder.AppendLine("  <PropertyGroup>");
        builder.AppendLine("    <TargetFramework>net10.0</TargetFramework>");
        builder.AppendLine("    <Nullable>enable</Nullable>");
        builder.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        builder.AppendLine("    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>");
        builder.AppendLine("    <GenerateDocumentationFile>true</GenerateDocumentationFile>");
        builder.AppendLine($"    <AssemblyName>{escapedLibraryName}</AssemblyName>");
        builder.AppendLine($"    <RootNamespace>{escapedLibraryName}</RootNamespace>");
        builder.AppendLine($"    <PackageId>{packageId}</PackageId>");
        builder.AppendLine($"    <Version>{packageVersion}</Version>");
        builder.AppendLine($"    <Authors>{authors}</Authors>");
        builder.AppendLine($"    <Company>{company}</Company>");
        builder.AppendLine($"    <Description>{description}</Description>");
        builder.AppendLine($"    <RepositoryUrl>{repositoryUrl}</RepositoryUrl>");
        builder.AppendLine("  </PropertyGroup>");
        builder.AppendLine();
        builder.AppendLine("  <ItemGroup>");
        builder.AppendLine("    <PackageReference Include=\"Microsoft.Extensions.DependencyInjection.Abstractions\" Version=\"10.0.0\" />");
        builder.AppendLine("    <PackageReference Include=\"System.ServiceModel.NetTcp\" Version=\"8.1.2\" />");
        builder.AppendLine("    <PackageReference Include=\"System.ServiceModel.Primitives\" Version=\"8.1.2\" />");
        builder.AppendLine("  </ItemGroup>");
        builder.AppendLine();
        builder.AppendLine("</Project>");
        return builder.ToString();
    }

    private static string GenerateNetFramework48RestApiWrapper(string projectName, bool enableSwagger, WcfServiceMetadataModel? metadata)
    {
        var name = SecurityElement.Escape(projectName) ?? projectName;
        var guid = CreateProjectGuid(projectName);
        var b = new StringBuilder();
        b.AppendLine("<Project ToolsVersion=\"15.0\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
        b.AppendLine("  <Import Project=\"$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props\" Condition=\"Exists('$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props')\" />");
        b.AppendLine("  <PropertyGroup>");
        b.AppendLine("    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration><Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>");
        b.AppendLine($"    <ProjectGuid>{guid}</ProjectGuid><ProjectTypeGuids>{{349c5851-65df-11da-9384-00065b846f21}};{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}</ProjectTypeGuids>");
        b.AppendLine($"    <OutputType>Library</OutputType><RootNamespace>{name}</RootNamespace><AssemblyName>{name}</AssemblyName><TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion>");
        b.AppendLine($"    <UseIISExpress>true</UseIISExpress><IISExpressSSLPort /><IISExpressAnonymousAuthentication>enabled</IISExpressAnonymousAuthentication><IISExpressWindowsAuthentication>disabled</IISExpressWindowsAuthentication><IISExpressUseClassicPipelineMode>false</IISExpressUseClassicPipelineMode><UseGlobalApplicationHostFile /><DocumentationFile>bin\\{name}.XML</DocumentationFile><NoWarn>1591</NoWarn><LangVersion>latest</LangVersion>");
        b.AppendLine("  </PropertyGroup>");
        b.AppendLine("  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' \">");
        b.AppendLine("    <DebugSymbols>true</DebugSymbols><DebugType>full</DebugType><Optimize>false</Optimize><OutputPath>bin\\</OutputPath><DefineConstants>DEBUG;TRACE</DefineConstants><ErrorReport>prompt</ErrorReport><WarningLevel>4</WarningLevel>");
        b.AppendLine("  </PropertyGroup>");
        b.AppendLine("  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' \">");
        b.AppendLine("    <DebugType>pdbonly</DebugType><Optimize>true</Optimize><OutputPath>bin\\</OutputPath><DefineConstants>TRACE</DefineConstants><ErrorReport>prompt</ErrorReport><WarningLevel>4</WarningLevel>");
        b.AppendLine("  </PropertyGroup>");
        b.AppendLine("  <ItemGroup><Reference Include=\"System\" /><Reference Include=\"System.Core\" /><Reference Include=\"System.Configuration\" /><Reference Include=\"System.Net.Http\" /><Reference Include=\"System.Runtime.Serialization\" /><Reference Include=\"System.ServiceModel\" /><Reference Include=\"System.Web\" /><Reference Include=\"System.Web.Http\" /><Reference Include=\"System.Web.Routing\" /><Reference Include=\"System.Xml\" /></ItemGroup>");
        b.AppendLine("  <ItemGroup><PackageReference Include=\"Microsoft.AspNet.WebApi.Core\" Version=\"5.2.9\" /><PackageReference Include=\"Microsoft.AspNet.WebApi.WebHost\" Version=\"5.2.9\" /><PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />" + (enableSwagger ? "<PackageReference Include=\"Swashbuckle.Core\" Version=\"5.6.0\" /><PackageReference Include=\"WebActivatorEx\" Version=\"2.2.0\" />" : string.Empty) + "</ItemGroup>");
        b.AppendLine("  <ItemGroup><Content Include=\"Web.config\" /><Content Include=\"Global.asax\" /></ItemGroup>");
        b.AppendLine("  <ItemGroup><Compile Include=\"Global.asax.cs\"><DependentUpon>Global.asax</DependentUpon></Compile><Compile Include=\"App_Start\\WebApiConfig.cs\" />" + (enableSwagger ? "<Compile Include=\"App_Start\\SwaggerConfig.cs\" />" : string.Empty) + "<Compile Include=\"Models\\RestErrorResponse.cs\" /><Compile Include=\"Wcf\\NetTcpWcfClientOptions.cs\" /><Compile Include=\"Wcf\\NetTcpBindingFactory.cs\" /><Compile Include=\"Wcf\\WcfClientFactory.cs\" /><Compile Include=\"ServiceReferences\\GeneratedProxy.cs\" />");
        foreach (var contract in metadata?.Contracts ?? []) b.AppendLine($"    <Compile Include=\"Controllers\\{CSharpIdentifierSanitizer.SanitizeTypeName(contract.ContractName)}Controller.cs\" />");
        b.AppendLine("  </ItemGroup>");
        b.AppendLine("  <Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />");
        b.AppendLine("  <PropertyGroup><VisualStudioVersion Condition=\"'$(VisualStudioVersion)' == ''\">17.0</VisualStudioVersion><VSToolsPath Condition=\"'$(VSToolsPath)' == ''\">$(MSBuildExtensionsPath32)\\Microsoft\\VisualStudio\\v$(VisualStudioVersion)</VSToolsPath></PropertyGroup>");
        b.AppendLine("  <Import Project=\"$(VSToolsPath)\\WebApplications\\Microsoft.WebApplication.targets\" Condition=\"'$(VSToolsPath)' != '' and Exists('$(VSToolsPath)\\WebApplications\\Microsoft.WebApplication.targets')\" />");
        b.AppendLine("</Project>");
        return b.ToString();
    }

    private static string CreateProjectGuid(string projectName)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(projectName));
        return new Guid(hash).ToString("B").ToUpperInvariant();
    }
}
