using System.Text;
using System.Text.RegularExpressions;

namespace WcfNetTcpClientGenerator.Core;

internal static partial class ProxyCodeParser
{
    private static readonly Regex NamespacePattern = NamespaceRegex();
    private static readonly Regex InterfacePattern = InterfaceRegex();
    private static readonly Regex ClientPattern = ClientRegex();
    private static readonly Regex MethodPattern = MethodRegex();

    public static ProxyParseResult Parse(string proxyCode, string fallbackNamespace)
    {
        var proxyNamespace = NamespacePattern.Match(proxyCode).Groups["name"].Value;
        proxyNamespace = string.IsNullOrWhiteSpace(proxyNamespace) ? fallbackNamespace : proxyNamespace;

        var clientLookup = ClientPattern.Matches(proxyCode)
            .Select(static match => new
            {
                InterfaceName = SimplifyTypeName(match.Groups["iface"].Value),
                ClientName = match.Groups["client"].Value
            })
            .ToDictionary(static item => item.InterfaceName, static item => item.ClientName, StringComparer.OrdinalIgnoreCase);

        var contracts = new List<WcfContractModel>();
        var diagnostics = new List<GenerationDiagnostic>();

        foreach (Match interfaceMatch in InterfacePattern.Matches(proxyCode))
        {
            var interfaceName = interfaceMatch.Groups["name"].Value;
            var body = interfaceMatch.Groups["body"].Value;

            var methods = MethodPattern.Matches(body)
                .Select(static match => new
                {
                    ProxyMethodName = match.Groups["name"].Value,
                    ResponseTypeName = ExtractResponseType(match.Groups["returnType"].Value),
                    Parameters = ParseParameters(match.Groups["parameters"].Value)
                })
                .ToArray();

            if (methods.Length == 0)
            {
                continue;
            }

            var uniqueNames = CSharpIdentifierSanitizer.EnsureUnique(methods.Select(static method => method.ProxyMethodName.EndsWith("Async", StringComparison.Ordinal) ? method.ProxyMethodName[..^5] : method.ProxyMethodName));

            for (var index = 0; index < methods.Length; index++)
            {
                if (!uniqueNames[index].Equals(methods[index].ProxyMethodName.EndsWith("Async", StringComparison.Ordinal) ? methods[index].ProxyMethodName[..^5] : methods[index].ProxyMethodName, StringComparison.Ordinal))
                {
                    diagnostics.Add(new GenerationDiagnostic(
                        DiagnosticSeverity.Warning,
                        $"Duplicate operation name detected for contract {interfaceName}. Generated wrapper method renamed to {uniqueNames[index]}.",
                        "DUPLICATE_OPERATION_NAME"));
                }
            }

            var operations = methods
                .Select((method, index) => new WcfOperationModel
                {
                    OperationName = method.ProxyMethodName.EndsWith("Async", StringComparison.Ordinal) ? method.ProxyMethodName[..^5] : method.ProxyMethodName,
                    MethodName = uniqueNames[index],
                    ProxyMethodName = method.ProxyMethodName,
                    ResponseTypeName = method.ResponseTypeName,
                    Parameters = method.Parameters
                })
                .ToArray();

            contracts.Add(new WcfContractModel
            {
                ContractName = interfaceName.TrimStart('I'),
                ClientClassName = clientLookup.GetValueOrDefault(interfaceName, $"{interfaceName.TrimStart('I')}Client"),
                ProxyNamespace = proxyNamespace,
                Operations = operations
            });
        }

        if (contracts.Count == 0)
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                "No service contracts were detected in the generated proxy code.",
                "NO_SERVICE_CONTRACTS"));
        }

        return new ProxyParseResult
        {
            Metadata = contracts.Count == 0
                ? null
                : new WcfServiceMetadataModel
                {
                    ServiceNamespace = proxyNamespace,
                    Contracts = contracts,
                    SourceDescription = string.Empty
                },
            Diagnostics = diagnostics
        };
    }

    private static string ExtractResponseType(string returnType)
    {
        var taskIndex = returnType.IndexOf("Task<", StringComparison.Ordinal);
        if (taskIndex < 0)
        {
            return "void";
        }

        return returnType[(taskIndex + 5)..].TrimEnd('>');
    }

    private static IReadOnlyList<WcfParameterModel> ParseParameters(string parameterList)
    {
        if (string.IsNullOrWhiteSpace(parameterList))
        {
            return [];
        }

        var parameters = new List<WcfParameterModel>();

        foreach (var segment in SplitTopLevel(parameterList))
        {
            var parts = segment.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            parameters.Add(new WcfParameterModel
            {
                Name = CSharpIdentifierSanitizer.SanitizeMemberName(parts[^1]),
                TypeName = string.Join(" ", parts[..^1]),
                IsOptional = segment.Contains('=', StringComparison.Ordinal)
            });
        }

        return parameters;
    }

    private static IEnumerable<string> SplitTopLevel(string input)
    {
        var builder = new StringBuilder();
        var depth = 0;

        foreach (var character in input)
        {
            switch (character)
            {
                case '<':
                    depth++;
                    builder.Append(character);
                    break;
                case '>':
                    depth--;
                    builder.Append(character);
                    break;
                case ',' when depth == 0:
                    yield return builder.ToString();
                    builder.Clear();
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static string SimplifyTypeName(string value)
    {
        var trimmed = value.Trim();
        var separatorIndex = trimmed.LastIndexOf('.');
        return separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..] : trimmed;
    }

    [GeneratedRegex(@"namespace\s+(?<name>[\w\.]+)\s*\{", RegexOptions.Multiline)]
    private static partial Regex NamespaceRegex();

    [GeneratedRegex(@"public\s+(?:partial\s+)?interface\s+(?<name>\w+)\s*\{(?<body>.*?)^\}", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex InterfaceRegex();

    [GeneratedRegex(@"public\s+(?:partial\s+)?class\s+(?<client>\w+)\s*:\s*(?:global::)?System\.ServiceModel\.ClientBase<(?<iface>[^>]+)>", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex ClientRegex();

    [GeneratedRegex(@"(?<returnType>(?:global::)?System\.Threading\.Tasks\.Task(?:<[^;]+?>)?)\s+(?<name>\w+Async)\((?<parameters>[^\)]*)\);", RegexOptions.Multiline)]
    private static partial Regex MethodRegex();

    internal sealed class ProxyParseResult
    {
        public WcfServiceMetadataModel? Metadata { get; init; }

        public IReadOnlyList<GenerationDiagnostic> Diagnostics { get; init; } = [];
    }
}
