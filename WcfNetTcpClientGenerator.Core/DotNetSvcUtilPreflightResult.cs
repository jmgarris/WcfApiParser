namespace WcfNetTcpClientGenerator.Core;

public sealed class DotNetSvcUtilPreflightResult
{
    public bool ToolFound { get; init; }

    public string? ToolPath { get; init; }

    public DotNetSvcUtilExecutionMode ToolExecutionMode { get; init; } = DotNetSvcUtilExecutionMode.NotFound;

    public string VersionOutput { get; init; } = string.Empty;

    public string DiagnosticMessage { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;
}
