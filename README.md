# WCF API Parser

`WCF API Parser` (`WcfNetTcpClientGenerator`) is a Windows desktop application for analyzing existing WCF `net.tcp` services and generating modern integration code from their metadata.

The solution supports two primary output modes:

1. A reusable **.NET 10 WCF client library**.
2. A classic **.NET Framework 4.8.1 ASP.NET Web API 2 REST wrapper** that exposes a WCF `net.tcp` service through JSON HTTP endpoints and Swagger.

Both output paths have been validated end-to-end against a live .NET Framework WCF `net.tcp` service. The .NET 10 path was generated, built, referenced by a .NET 10 console application, and used for live `GetPatients` / `GetPatient` calls. The REST-wrapper path was generated, built, hosted in IIS Express, opened through Swagger, and used for live REST-to-WCF calls.

## Architecture

```text
Existing WCF net.tcp service
        |
        |  MEX / WSDL / XSD metadata
        v
WCF API Parser
        |
        +------------------------------+
        |                              |
        v                              v
.NET 10 client library        .NET Framework 4.8.1
                              ASP.NET Web API 2 wrapper
                                      |
                                      v
                                 Swagger / JSON REST
                                      |
                                      v
                              Generated WCF proxy
                                      |
                                      v
                              Original net.tcp service
```

## Features

- Analyze WCF metadata from:
  - a `net.tcp` service endpoint
  - an explicit MEX endpoint
  - a WSDL file
  - a folder containing WSDL/XSD metadata
- Discover service contracts and operations before generation.
- Use a two-pane WinUI 3 workflow with card-based configuration, scrollable detected operations, scrollable status/progress history, and independent clear actions.
- Generate a standalone .NET 10 WCF client library with:
  - generated WCF proxy code
  - wrapper interfaces and services
  - binding helpers
  - runtime options
  - dependency-injection extensions
  - package-ready project metadata
- Generate a .NET Framework 4.8.1 ASP.NET Web API 2 REST wrapper with:
  - JSON API controllers
  - generated WCF proxy code
  - Swagger / Swashbuckle documentation
  - Web.config-driven WCF runtime settings
  - Web API and Newtonsoft.Json assembly binding redirects
  - safe WCF channel close/abort handling
  - safe REST error payloads
- Support WCF `NetTcpBinding` security modes:
  - `None`
  - `Transport`
  - `Message`
  - `TransportWithMessageCredential`
- Support transport and message credential types including:
  - `None`
  - `Windows`
  - `Certificate`
  - `UserName` where supported
- Support reliable sessions when enabled by the source service configuration.
- Support client certificate configuration from:
  - the Windows certificate store
  - `.pfx` / `.p12` files containing a private key
- Support certificate password sources without embedding the actual password in generated configuration:
  - no password
  - environment variable
  - named application setting
- Format and syntax-check generated C# using Roslyn.
- Format and validate generated XML files such as `Web.config` and `.csproj`.
- Normalize AI-generated XML documentation before inserting it into generated C#.
- Reject malformed generated C# or XML with explicit generation diagnostics.
- Package generated .NET 10 client libraries as NuGet packages.
- Preflight-check `dotnet-svcutil` and display command diagnostics in the UI.
- Support XML documentation generation through:
  - deterministic local fallback comments
  - Microsoft 365 Copilot
  - OpenAI

## Projects

### `WcfNetTcpClientGenerator.App`

WinUI 3 desktop application responsible for:

- collecting service and generation settings
- metadata analysis
- generation workflow
- tool connectivity checks
- AI documentation configuration
- NuGet packaging

### `WcfNetTcpClientGenerator.Core`

Core generation engine responsible for:

- metadata discovery
- `dotnet-svcutil` execution
- proxy generation
- WCF binding generation
- REST controller generation
- .NET Framework Web API project generation
- generated C# formatting and syntax validation
- generated XML formatting and validation
- certificate configuration
- documentation generation

### `WcfNetTcpClientGenerator.Tests`

NUnit test project covering generator behavior, project generation, documentation safety, XML formatting, certificate handling, binding configuration, and regression scenarios.

## Prerequisites

### Parser application

- Windows
- .NET 10 SDK
- Visual Studio 2022 or later for the full WinUI development experience
- `dotnet-svcutil` when generating WCF proxies

Install `dotnet-svcutil` globally with:

```powershell
dotnet tool install --global dotnet-svcutil
```

The application searches for `dotnet-svcutil` in the following order:

1. User-configured executable path.
2. Global `dotnet-svcutil` command.
3. `%USERPROFILE%\.dotnet\tools\dotnet-svcutil.exe`.
4. A local .NET tool manifest.

### Generated .NET Framework 4.8.1 REST wrapper

To build and run the generated REST wrapper you need:

- .NET Framework 4.8.1 Developer Pack / targeting pack
- Visual Studio 2022 MSBuild or equivalent .NET Framework-capable MSBuild
- IIS or IIS Express for local hosting

## Build the Parser

```powershell
dotnet build WcfNetTcpClientGenerator.sln
```

Run the tests:

```powershell
dotnet test WcfNetTcpClientGenerator.Tests\WcfNetTcpClientGenerator.Tests.csproj
```

## Running the Application

1. Start `WcfNetTcpClientGenerator.App`.
2. Enter the WCF service endpoint, for example:

   ```text
   net.tcp://server:9001/PatientProcessing
   ```

3. Enter or discover the metadata endpoint, for example:

   ```text
   net.tcp://server:9001/PatientProcessing/mex
   ```

4. Configure the service namespace and output folder.
5. Select the generated output type:
   - WCF client library (.NET 10)
   - REST API wrapper for WCF net.tcp (.NET Framework 4.8.1)
6. Configure WCF security, credentials, reliable sessions, timeouts, and message-size settings.
7. Click **Test svcutil** if needed.
8. Click **Analyze Metadata** to inspect discovered operations.
9. Optionally enable AI-assisted XML documentation.
10. Generate the selected output.

## Metadata Discovery

WCF `net.tcp` services do not automatically expose `?wsdl`, so metadata must be available through MEX, WSDL, or local metadata files.

Supported discovery methods include:

- explicit MEX URL
- service endpoint with common MEX probing patterns
- direct WSDL file
- folder containing WSDL/XSD documents

For example:

```text
Service endpoint:
net.tcp://localhost:9001/PatientProcessing

MEX endpoint:
net.tcp://localhost:9001/PatientProcessing/mex
```

When a metadata endpoint ends in `/mex` and no explicit runtime service endpoint is supplied, the REST-wrapper generator can derive the runtime endpoint by removing the trailing `/mex` segment.

## Generated .NET 10 Client Library

A generated client library has a structure similar to:

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

The generated client can then be packaged as a NuGet package from the desktop application.

## Generated .NET Framework 4.8.1 REST Wrapper

A generated REST wrapper has a structure similar to:

```text
GeneratedNetTcpClient/
  GeneratedNetTcpClient.csproj
  Web.config
  Global.asax
  Global.asax.cs
  App_Start/
    WebApiConfig.cs
    SwaggerConfig.cs
  Controllers/
    PatientController.cs
  Models/
    RestErrorResponse.cs
  ServiceReferences/
    GeneratedProxy.cs
  Wcf/
    NetTcpWcfClientOptions.cs
    NetTcpBindingFactory.cs
    WcfClientFactory.cs
  README.md
```

The wrapper targets classic ASP.NET Web API 2 and is intended for IIS/IIS Express hosting.

## REST Wrapper Configuration

The generated `Web.config` contains non-secret WCF runtime settings such as:

```xml
<appSettings>
  <add key="Wcf:EndpointUrl" value="net.tcp://server:9001/PatientProcessing" />
  <add key="Wcf:SecurityMode" value="None" />
  <add key="Wcf:TcpTransportClientCredentialType" value="None" />
  <add key="Wcf:MessageClientCredentialType" value="None" />
  <add key="Wcf:ReliableSessionEnabled" value="false" />
  <add key="Wcf:OpenTimeout" value="00:00:30" />
  <add key="Wcf:CloseTimeout" value="00:00:30" />
  <add key="Wcf:SendTimeout" value="00:01:40" />
  <add key="Wcf:ReceiveTimeout" value="00:01:40" />
  <add key="Wcf:MaxReceivedMessageSize" value="65536" />
</appSettings>
```

The generated configuration also includes binding redirects required by the Web API 2 / Swagger dependency set.

## Client Certificate Authentication

Certificate settings are only generated when certificate credentials are selected.

### Windows certificate store

Supported settings include:

```text
Wcf:ClientCertificateSource=Store
Wcf:ClientCertificateStoreLocation=CurrentUser
Wcf:ClientCertificateStoreName=My
Wcf:ClientCertificateFindType=FindByThumbprint
Wcf:ClientCertificateFindValue=<thumbprint>
```

Thumbprints are normalized before lookup. Other find values such as subject or issuer names preserve their embedded spaces.

### PFX / P12 certificate file

Supported file types:

```text
.pfx
.p12
```

The generated runtime code verifies that the loaded certificate contains a private key.

Certificate passwords are not generated directly into `Web.config`. Instead the wrapper can obtain a password from an environment variable or from a named deployment application setting.

## Swagger

When Swagger is enabled, the generated REST wrapper exposes:

```text
Swagger UI:
/swagger
/swagger/ui/index

Swagger JSON:
/swagger/docs/v1
```

Example local IIS Express URL:

```text
http://localhost:8085/swagger
```

## Build a Generated REST Wrapper

Using Visual Studio 2022 Professional MSBuild:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" `
  "C:\Path\To\GeneratedNetTcpClient\GeneratedNetTcpClient.csproj" `
  /t:Restore,Build `
  /p:Configuration=Debug `
  /p:Platform=AnyCPU
```

A clean generated wrapper should build with no errors. The validated test path also builds without assembly-binding warnings.

## Run a Generated REST Wrapper with IIS Express

```powershell
& "C:\Program Files\IIS Express\iisexpress.exe" `
  /path:"C:\Path\To\GeneratedNetTcpClient" `
  /port:8085
```

Then open:

```text
http://localhost:8085/swagger
```

A generated controller route may look like:

```text
POST /api/patient/get-patients
POST /api/patient/get-patient
```

## Generated Error Handling

Generated REST endpoints return sanitized errors when an upstream WCF call fails.

Example:

```json
{
  "Error": "The upstream WCF service could not complete the request.",
  "Code": "wcf-communication",
  "CorrelationId": "..."
}
```

Raw exception stack traces, certificate passwords, and other secret material are not returned to REST callers.

## Generated Source Validation

Generated C# files are parsed with Roslyn before generation is considered successful.

Malformed generated C# produces a generation error such as:

```text
GENERATED_CSHARP_SYNTAX_ERROR
```

Generated XML files are parsed and formatted before being written. Invalid XML produces:

```text
GENERATED_XML_SYNTAX_ERROR
```

This prevents malformed generated source or configuration from being silently emitted as successful output.

## AI-Assisted XML Documentation

The parser supports three documentation modes:

- Local fallback
- Microsoft 365 Copilot
- OpenAI

OpenAI can read its API key from:

- `OPENAI_API_KEY`
- a user-entered key stored locally in Windows PasswordVault

Provider-generated documentation is treated as untrusted input. XML documentation is parsed and normalized before it is emitted into generated C# comments, preventing raw provider output from escaping into source code.

## dotnet-svcutil Diagnostics

Before proxy generation, the app can run a preflight check and report:

- executable used
- command line
- working directory
- process exit code
- standard output
- standard error

The generated .NET Framework REST-wrapper proxy uses a .NET Framework-compatible target (`net48`) while the generated project itself targets .NET Framework 4.8.1.

## Validation Status

The current codebase has been validated with:

- parser solution build with **0 warnings and 0 errors**
- **77 automated tests passing**
- fresh .NET 10 client-library generation and build with **0 warnings and 0 errors**
- live .NET 10 `GetPatients` and `GetPatient` calls against the .NET Framework WCF test service
- fresh .NET Framework 4.8.1 REST-wrapper generation
- generated REST-wrapper build with **0 warnings and 0 errors**
- IIS Express startup
- Swagger UI startup
- live `GetPatients` REST call through the generated WCF proxy
- live `GetPatient` REST call with request data passed through to the WCF service

## Security Notes

- Do not commit WCF usernames/passwords, certificate passwords, API keys, or private certificate files.
- Prefer environment variables or protected deployment configuration for secrets.
- Prefer certificate thumbprints when selecting certificates from the Windows certificate store.
- Review generated REST endpoints before publishing them externally; the generator exposes WCF operations as HTTP endpoints but does not replace application-specific authentication and authorization design.
