using System.Security;
using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class ProjectFileGenerator
{
    public string Generate(string libraryName, ClientLibraryGenerationOptions options)
    {
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
}
