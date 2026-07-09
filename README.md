# WCF Net.TCP Client Generator

`WcfNetTcpClientGenerator` is a WinUI 3 desktop application and .NET 10 solution for analyzing existing WCF `net.tcp` services and generating reusable client libraries from their metadata.

## Features

- Analyze WCF metadata from a `net.tcp` endpoint, explicit MEX URL, WSDL file, or a folder containing WSDL/XSD documents
- Generate a standalone client library with proxy code, wrapper services, options, binding helpers, dependency injection extensions, and package-ready project metadata
- Package the generated library as a NuGet package from the desktop app
- Preflight-check `dotnet-svcutil` before proxy generation and surface diagnostics in the UI
- Support `dotnet-svcutil` discovery from:
  - a user-specified executable path
  - the global `dotnet-svcutil` command
  - `%USERPROFILE%\.dotnet\tools\dotnet-svcutil.exe`
  - a local tool manifest with `dotnet tool restore` and `dotnet tool run`
- Log the exact proxy-generation command, working directory, exit code, standard output, and standard error
- Add XML documentation comments with:
  - local fallback generation
  - Microsoft 365 Copilot
  - OpenAI
- Test `dotnet-svcutil`, Copilot, and OpenAI connectivity from the app
- Clear the displayed status/progress history without clearing inputs, detected operations, output paths, or disk logs

## Projects

- `WcfNetTcpClientGenerator.App`
  - WinUI 3 desktop application
  - Collects service settings, runs metadata analysis, generates the library, and packages NuGet output
- `WcfNetTcpClientGenerator.Core`
  - Core generation engine
  - Handles metadata discovery, `dotnet-svcutil` execution, wrapper generation, project generation, packaging, and AI-assisted documentation
- `WcfNetTcpClientGenerator.Tests`
  - NUnit test project
  - Covers generator logic, tool detection, command construction, error handling, and view model behavior

## Prerequisites

- Windows with WinUI 3 support
- .NET 10 SDK
- Visual Studio 2022 or later for the full desktop development experience
- `dotnet-svcutil` available in one of the supported locations if you want proxy generation

Global install example:

```powershell
dotnet tool install --global dotnet-svcutil
```

If the global tool is installed but not visible to the WinUI process, you can either add `%USERPROFILE%\.dotnet\tools` to `PATH` or browse to `dotnet-svcutil.exe` in the app.

## Running the App

1. Build the solution:

```powershell
dotnet build WcfNetTcpClientGenerator.sln
```

2. Start `WcfNetTcpClientGenerator.App`.
3. Enter the WCF `net.tcp://...` service endpoint.
4. Optionally provide a metadata URL, WSDL file path, metadata folder, or explicit `dotnet-svcutil` path.
5. Click **Test dotnet-svcutil** to verify which tool location and execution mode will be used.
6. Click **Analyze Service Metadata** to inspect detected operations.
7. Optionally enable AI-assisted XML comments.
8. Click **Generate Class Library** to create the client library.
9. Click **Package Class Library as NuGet** to produce a `.nupkg`.

The **Clear Status** button only clears the displayed status/progress history. It does not cancel work, clear the operation list, remove generated files, or delete logs from disk.

## Metadata Discovery

`net.tcp` services do not automatically expose `?wsdl`, so the app supports several discovery paths:

- Explicit MEX URL such as `http://server:808/MyService/mex`
- Direct WSDL file input
- Folder input containing WSDL and XSD documents
- Common MEX probing patterns derived from the service endpoint

If metadata cannot be resolved, the app reports that the service must expose metadata through MEX, HTTP WSDL, or local WSDL/XSD files.

## dotnet-svcutil Detection

Before proxy generation, the app runs a preflight check and tries `dotnet-svcutil` in this order:

1. User-configured executable path
2. Global command: `dotnet-svcutil --help`
3. Windows global tool path: `%USERPROFILE%\.dotnet\tools\dotnet-svcutil.exe`
4. Local tool manifest containing `dotnet-svcutil`

If a local manifest is found and contains `dotnet-svcutil`, the app restores it with `dotnet tool restore` and then runs:

```powershell
dotnet tool run dotnet-svcutil -- [arguments]
```

Supported proxy-generation command shape includes:

```powershell
dotnet-svcutil net.tcp://server:port/ServiceName/mex -n *,GeneratedNamespace -o GeneratedProxy.cs
```

The generator uses equivalent arguments and also sets the output directory, serializer, target framework, and verbosity.

## AI-Assisted XML Documentation

The app supports three documentation modes:

- `Local fallback`
- `Microsoft 365 Copilot`
- `OpenAI`

OpenAI can read its API key from:

- the `OPENAI_API_KEY` environment variable
- a user-entered key stored locally in Windows PasswordVault

Generated wrapper files include a note when AI-assisted comments are used so reviewers can validate the output before publishing.

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

## Build and Test

Build the solution:

```powershell
dotnet build WcfNetTcpClientGenerator.sln
```

Run the test suite:

```powershell
dotnet test WcfNetTcpClientGenerator.Tests\WcfNetTcpClientGenerator.Tests.csproj
```

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
