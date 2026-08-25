namespace WcfNetTcpClientGenerator.Core;

/// <summary>Diagnostic helper for the Windows SDK/Visual Studio full-framework svcutil.exe fallback.</summary>
public sealed class ClassicSvcUtilRunner : IWcfProxyToolRunner
{
    public string ToolName => "svcutil.exe";

    public const string FallbackDiagnostic = "dotnet-svcutil could not generate a net48-compatible proxy. Install or select svcutil.exe from the Windows SDK or Visual Studio Developer Command Prompt, then regenerate the proxy using the classic WCF tool.";
}
