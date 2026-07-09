namespace WcfNetTcpClientGenerator.Core;

public sealed class CopilotConnectionTestResult
{
    public bool Success { get; init; }

    public bool IsSignedIn { get; init; }

    public bool ApiAvailable { get; init; }

    public string AccountName { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public IReadOnlyList<CopilotChatDiagnostic> Diagnostics { get; init; } = [];
}
