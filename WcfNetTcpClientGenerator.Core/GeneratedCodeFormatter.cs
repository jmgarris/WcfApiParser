using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WcfNetTcpClientGenerator.Core;

internal static class GeneratedCodeFormatter
{
    public static FormatResult FormatCSharp(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var errors = tree.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        if (errors.Length > 0)
        {
            return new FormatResult(null, errors);
        }

        return new FormatResult(
            tree.GetRoot().NormalizeWhitespace(indentation: "    ", eol: Environment.NewLine).ToFullString(),
            []);
    }

    internal sealed record FormatResult(string? Source, IReadOnlyList<string> Errors);
}
