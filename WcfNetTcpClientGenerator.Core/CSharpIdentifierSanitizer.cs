using System.Text;
using System.Text.RegularExpressions;

namespace WcfNetTcpClientGenerator.Core;

public static class CSharpIdentifierSanitizer
{
    private static readonly Regex InvalidCharacters = new("[^a-zA-Z0-9_]", RegexOptions.Compiled);

    private static readonly HashSet<string> Keywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate",
        "do", "double", "else", "enum", "event", "explicit", "extern", "false",
        "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
        "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    ];

    public static string SanitizeTypeName(string? value, string fallback = "GeneratedType")
        => SanitizeIdentifier(value, fallback);

    public static string SanitizeMemberName(string? value, string fallback = "GeneratedMember")
        => SanitizeIdentifier(value, fallback);

    public static string SanitizeNamespace(string? value, string fallback = "Generated.Wcf")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var parts = value
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static part => SanitizeIdentifier(part, "Generated"))
            .ToArray();

        return parts.Length == 0 ? fallback : string.Join(".", parts);
    }

    public static IReadOnlyList<string> EnsureUnique(IEnumerable<string> values)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        foreach (var value in values)
        {
            var sanitized = SanitizeMemberName(value, "GeneratedMember");
            if (!counts.TryGetValue(sanitized, out var count))
            {
                counts[sanitized] = 1;
                results.Add(sanitized);
                continue;
            }

            count++;
            counts[sanitized] = count;
            results.Add($"{sanitized}{count}");
        }

        return results;
    }

    private static string SanitizeIdentifier(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = InvalidCharacters.Replace(value.Trim(), "_");
        if (normalized.Length == 0)
        {
            normalized = fallback;
        }

        var builder = new StringBuilder(normalized.Length + 1);

        if (!SyntaxFacts.IsIdentifierStartCharacter(normalized[0]))
        {
            builder.Append('_');
        }

        foreach (var character in normalized)
        {
            builder.Append(SyntaxFacts.IsIdentifierPartCharacter(character) ? character : '_');
        }

        var result = builder.ToString();
        if (Keywords.Contains(result))
        {
            result = $"{result}_";
        }

        return result;
    }

    private static class SyntaxFacts
    {
        public static bool IsIdentifierStartCharacter(char value)
            => char.IsLetter(value) || value == '_';

        public static bool IsIdentifierPartCharacter(char value)
            => char.IsLetterOrDigit(value) || value == '_';
    }
}
