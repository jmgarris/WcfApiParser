using System.Text;
using System.Xml.Linq;

namespace WcfNetTcpClientGenerator.Core;

public sealed class RestControllerGenerator
{
    public async Task<WrapperImplementationGenerator.GeneratedImplementationResult> GenerateAsync(
        WcfContractModel contract, string rootNamespace, ClientLibraryGenerationOptions options,
        IMethodDocumentationProvider documentationProvider, CancellationToken cancellationToken)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        var name = CSharpIdentifierSanitizer.SanitizeTypeName(contract.ContractName);
        var client = CSharpIdentifierSanitizer.SanitizeTypeName(contract.ClientClassName);
        var builder = new StringBuilder();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Net;");
        builder.AppendLine("using System.ServiceModel;");
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine("using System.Web.Http;");
        builder.AppendLine("using System.Web.Http.Description;");
        builder.AppendLine($"using {rootNamespace}.Models;");
        builder.AppendLine($"using {rootNamespace}.Wcf;");
        builder.AppendLine();
        builder.AppendLine($"namespace {rootNamespace}.Controllers;");
        builder.AppendLine();
        var routePrefix = ToRoute(name.Replace("Service", string.Empty, StringComparison.OrdinalIgnoreCase));
        builder.AppendLine($"[RoutePrefix(\"api/{routePrefix}\")]");
        builder.AppendLine($"public sealed class {name}Controller : ApiController");
        builder.AppendLine("{");
        builder.AppendLine("    private readonly WcfClientFactory _clientFactory = new WcfClientFactory();");
        foreach (var operation in contract.Operations)
        {
            var request = operation.Parameters.FirstOrDefault();
            var parameter = request is null ? string.Empty : $"{request.TypeName} {CSharpIdentifierSanitizer.SanitizeMemberName(request.Name)}";
            var argument = request is null ? string.Empty : CSharpIdentifierSanitizer.SanitizeMemberName(request.Name);
            var action = CSharpIdentifierSanitizer.SanitizeMemberName(operation.MethodName.Replace("Async", string.Empty, StringComparison.Ordinal));
            var docs = await documentationProvider.GenerateDocumentationAsync(new MethodDocumentationRequest { ServiceName = contract.ContractName, OperationName = operation.OperationName, GeneratedWrapperMethodName = action, Parameters = operation.Parameters, ResponseTypeName = operation.ResponseTypeName, RequestTypeName = request?.TypeName ?? "None" }, options.DocumentationOptions, cancellationToken).ConfigureAwait(false);
            if (!AppendXmlDocumentation(builder, docs.XmlDocumentationText, "    "))
            {
                AppendFallbackDocumentation(builder, operation, request is not null, argument, "    ");
            }
            if (!string.Equals(operation.ResponseTypeName, "void", StringComparison.OrdinalIgnoreCase)) builder.AppendLine($"    [ResponseType(typeof({operation.ResponseTypeName}))]");
            builder.AppendLine("    [HttpPost]");
            builder.AppendLine($"    [Route(\"{ToRoute(operation.OperationName)}\")]");
            builder.AppendLine($"    public async Task<IHttpActionResult> {action}({parameter})");
            builder.AppendLine("    {");
            builder.AppendLine("        try"); builder.AppendLine("        {");
            builder.AppendLine($"            var client = _clientFactory.Create{client}();");
            builder.AppendLine("            try"); builder.AppendLine("            {");
            builder.AppendLine($"                var response = await client.{operation.ProxyMethodName}({argument}).ConfigureAwait(false);");
            builder.AppendLine("                return Ok(response);"); builder.AppendLine("            }");
            builder.AppendLine("            finally { _clientFactory.CloseOrAbort(client); }");
            builder.AppendLine("        }");
            builder.AppendLine("        catch (TimeoutException ex) { return Content(HttpStatusCode.GatewayTimeout, RestErrorResponse.FromException(ex)); }");
            builder.AppendLine("        catch (CommunicationException ex) { return Content(HttpStatusCode.BadGateway, RestErrorResponse.FromException(ex)); }");
            builder.AppendLine("    }"); builder.AppendLine();
        }
        builder.AppendLine("}");
        return new WrapperImplementationGenerator.GeneratedImplementationResult(builder.ToString(), diagnostics);
    }

    /// <summary>
    /// Appends untrusted provider output only after it has been parsed as an XML fragment.
    /// This ensures provider text cannot escape a C# XML documentation comment.
    /// </summary>
    private static bool AppendXmlDocumentation(StringBuilder builder, string? documentation, string indentation)
    {
        if (string.IsNullOrWhiteSpace(documentation))
        {
            return false;
        }

        var xmlLines = documentation
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => !line.StartsWith("```", StringComparison.Ordinal))
            .Select(static line => line.StartsWith("///", StringComparison.Ordinal) ? line[3..].TrimStart() : line)
            .Where(static line => !line.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var fragment = string.Join("\n", xmlLines).Trim();
        if (fragment.Length == 0)
        {
            return false;
        }

        try
        {
            var root = XElement.Parse($"<root>{fragment}</root>", LoadOptions.PreserveWhitespace);
            if (root.Elements().Any() is false || root.Nodes().Any(node => node is not XElement && (node is not XText text || !string.IsNullOrWhiteSpace(text.Value))))
            {
                return false;
            }

            foreach (var element in root.Elements())
            {
                AppendCommentLines(builder, element.ToString(SaveOptions.DisableFormatting), indentation);
            }

            return true;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    private static void AppendCommentLines(StringBuilder builder, string xmlDocumentation, string indentation)
    {
        var lines = xmlDocumentation
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        foreach (var line in lines)
        {
            builder.Append(indentation);
            builder.Append("///");
            if (line.Length > 0)
            {
                builder.Append(' ');
                builder.Append(line);
            }
            builder.AppendLine();
        }
    }

    private static void AppendFallbackDocumentation(StringBuilder builder, WcfOperationModel operation, bool hasRequest, string argument, string indentation)
    {
        builder.AppendLine($"{indentation}/// <summary>");
        builder.AppendLine($"{indentation}/// Calls the {operation.OperationName} WCF operation through the configured net.tcp service endpoint.");
        builder.AppendLine($"{indentation}/// </summary>");
        if (hasRequest) builder.AppendLine($"{indentation}/// <param name=\"{argument}\">The JSON request body mapped to the WCF request contract.</param>");
        builder.AppendLine($"{indentation}/// <returns>The JSON response returned from the WCF service.</returns>");
    }

    internal static string ToRoute(string value)
    {
        var text = CSharpIdentifierSanitizer.SanitizeMemberName(value).TrimEnd('_');
        var builder = new StringBuilder();
        for (var i = 0; i < text.Length; i++) { var c = text[i]; if (i > 0 && char.IsUpper(c) && (char.IsLower(text[i - 1]) || char.IsDigit(text[i - 1]))) builder.Append('-'); builder.Append(char.ToLowerInvariant(c)); }
        return builder.ToString().Replace('_', '-');
    }
}
