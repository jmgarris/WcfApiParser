# WCF Net.TCP Client Generator

`WcfNetTcpClientGenerator` is a .NET 10 solution for generating reusable .NET client libraries for existing WCF services that expose `net.tcp` endpoints.

## Projects

- `WcfNetTcpClientGenerator.App`
  - WinUI 3 desktop application
  - Collects service information, analyzes metadata, generates a client library, and packages it as NuGet
- `WcfNetTcpClientGenerator.Core`
  - Core generation engine
  - Handles metadata discovery, proxy generation, wrapper generation, project generation, and packaging
- `WcfNetTcpClientGenerator.Tests`
  - NUnit tests for generator behavior
  - Uses mock metadata models and local files, not a live WCF service

## Running the App

1. Open `WcfNetTcpClientGenerator.sln` in Visual Studio or build from the command line with `dotnet build`.
2. Start `WcfNetTcpClientGenerator.App`.
3. Enter the WCF `net.tcp://...` service endpoint.
4. Optionally enter a metadata URL, WSDL file path, or metadata folder path.
5. Click **Analyze Service Metadata** to inspect detected operations.
6. Click **Generate Class Library** to create the client library.
7. Click **Package Class Library as NuGet** to produce a `.nupkg`.

## Why a Separate Metadata Endpoint May Be Required

`net.tcp` services do not automatically expose metadata through `?wsdl`.

The app supports several discovery paths:

- Explicit metadata URL such as `http://server:808/MyService/mex`
- Direct WSDL file input
- Folder input containing WSDL and XSD documents
- Common MEX probing patterns based on the service endpoint

If metadata still cannot be resolved, the app reports that the service must expose metadata through MEX, HTTP WSDL, or local WSDL/XSD files.

## Generated Client Library Structure

Each generated client library is written as a standalone `net10.0` project with a structure similar to this:

```text
GeneratedLibraryName/
  GeneratedLibraryName.csproj
  ServiceReferences/
    GeneratedProxy.cs
  Options/
    NetTcpWcfClientOptions.cs
  Interfaces/
    ICustomerServiceClient.cs
  Services/
    CustomerServiceClient.cs
  Binding/
    NetTcpBindingFactory.cs
  DependencyInjection/
    ServiceCollectionExtensions.cs
  README.md
```

## Packaging as NuGet

The generated project includes package metadata and is ready to pack with:

```powershell
dotnet pack GeneratedLibraryName.csproj -c Release
```

The WinUI app runs the equivalent packaging flow and reports the generated `.nupkg` location after completion.

## Consuming the Generated Package

After adding the generated NuGet package to another .NET application:

```csharp
var options = new NetTcpWcfClientOptions
{
    EndpointUrl = "net.tcp://server:808/MyService",
    SecurityMode = "Transport",
    TcpClientCredentialType = "Windows"
};

var client = new CustomerServiceClient(options);
var response = await client.GetCustomerAsync(request);
```
