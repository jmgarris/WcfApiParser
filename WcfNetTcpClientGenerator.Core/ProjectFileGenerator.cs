using System.Security;
using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class ProjectFileGenerator
{
    public string Generate(string libraryName, ClientLibraryGenerationOptions options)
    {
        if (options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper)
        {
            return GenerateNetFramework48RestApiWrapper(libraryName, options.EnableSwagger);
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

    private static string GenerateNetFramework48RestApiWrapper(string projectName, bool enableSwagger) => $$"""
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup><TargetFrameworkVersion>v4.8</TargetFrameworkVersion><OutputType>Library</OutputType><RootNamespace>{{SecurityElement.Escape(projectName)}}</RootNamespace><AssemblyName>{{SecurityElement.Escape(projectName)}}</AssemblyName><DocumentationFile>bin\{{SecurityElement.Escape(projectName)}}.XML</DocumentationFile><NoWarn>1591</NoWarn></PropertyGroup>
  <ItemGroup>
    <Reference Include="System" /><Reference Include="System.Core" /><Reference Include="System.Net.Http" /><Reference Include="System.ServiceModel" /><Reference Include="System.Runtime.Serialization" /><Reference Include="System.Web" /><Reference Include="System.Web.Http" />
  </ItemGroup>
  <ItemGroup><PackageReference Include="Microsoft.AspNet.WebApi.Core" Version="5.2.9" /><PackageReference Include="Microsoft.AspNet.WebApi.WebHost" Version="5.2.9" /><PackageReference Include="Newtonsoft.Json" Version="13.0.3" />{{(enableSwagger ? "<PackageReference Include=\"Swashbuckle.Core\" Version=\"5.6.0\" />" : string.Empty)}}</ItemGroup>
  <ItemGroup><Compile Include="**\*.cs" /></ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
""";
}
