using System.Security;
using System.Xml.Linq;

namespace WcfNetTcpClientGenerator.Core;

public sealed class XmlDocumentationSanitizer
{
    public SanitizationResult Sanitize(string rawDocumentation, MethodDocumentationRequest request, int maxCommentLength)
    {
        if (string.IsNullOrWhiteSpace(rawDocumentation))
        {
            return Failure("The Copilot response was empty.", "EMPTY_RESPONSE");
        }

        var normalized = rawDocumentation
            .Replace("```xml", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```csharp", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        var lines = normalized
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            return Failure("The Copilot response did not contain documentation lines.", "EMPTY_LINES");
        }

        var xmlLines = new List<string>();
        foreach (var line in lines)
        {
            if (line.StartsWith("///", StringComparison.Ordinal))
            {
                xmlLines.Add(line[3..].TrimStart());
                continue;
            }

            if (line.StartsWith("<", StringComparison.Ordinal) || line.StartsWith("&lt;", StringComparison.Ordinal))
            {
                xmlLines.Add(line);
                continue;
            }
        }

        if (xmlLines.Count == 0)
        {
            return Failure("The Copilot response did not contain XML documentation tags.", "MISSING_XML_TAGS");
        }

        var xmlFragment = string.Join(Environment.NewLine, xmlLines);
        if (ContainsForbiddenContent(xmlFragment))
        {
            return Failure("The Copilot response contained forbidden or unrelated content.", "FORBIDDEN_CONTENT");
        }

        try
        {
            var wrapped = $"<root>{xmlFragment}</root>";
            var root = XElement.Parse(wrapped, LoadOptions.PreserveWhitespace);
            var rebuiltLines = root.Elements()
                .Select(static element => $"/// {element.ToString(SaveOptions.DisableFormatting)}")
                .ToList();

            if (rebuiltLines.Count == 0)
            {
                return Failure("The Copilot response did not contain any valid XML documentation elements.", "NO_XML_ELEMENTS");
            }

            var sanitizedText = string.Join(Environment.NewLine, rebuiltLines);
            if (sanitizedText.Length > maxCommentLength && maxCommentLength > 0)
            {
                return Failure($"The Copilot response exceeded the maximum comment length of {maxCommentLength} characters.", "COMMENT_TOO_LONG");
            }

            return new SanitizationResult(true, sanitizedText, []);
        }
        catch (Exception exception)
        {
            return Failure($"The Copilot response contained invalid XML documentation. {exception.Message}", "INVALID_XML");
        }
    }

    private static bool ContainsForbiddenContent(string xmlFragment)
    {
        var forbiddenPatterns = new[]
        {
            "http://",
            "https://",
            "net.tcp://",
            "password",
            "secret",
            "access token",
            "refresh token",
            "api token",
            "credential",
            "username"
        };

        return forbiddenPatterns.Any(pattern => xmlFragment.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static SanitizationResult Failure(string message, string code)
        => new(
            false,
            string.Empty,
            [
                new DocumentationGenerationDiagnostic
                {
                    Severity = "Warning",
                    Code = code,
                    Message = message
                }
            ]);

    public sealed record SanitizationResult(
        bool Success,
        string XmlDocumentationText,
        IReadOnlyList<DocumentationGenerationDiagnostic> Diagnostics);
}
