namespace WcfNetTcpClientGenerator.Core;

public sealed record GenerationDiagnostic(
    DiagnosticSeverity Severity,
    string Message,
    string? Code = null,
    string? FilePath = null);
