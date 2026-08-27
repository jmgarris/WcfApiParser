using System.Text;

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
            builder.AppendLine("    /// <summary>");
            builder.AppendLine($"    /// Calls the {operation.OperationName} WCF operation through the configured net.tcp service endpoint.");
            builder.AppendLine("    /// </summary>");
            if (request is not null) builder.AppendLine($"    /// <param name=\"{argument}\">The JSON request body mapped to the WCF request contract.</param>");
            builder.AppendLine("    /// <returns>The JSON response returned from the WCF service.</returns>");
            if (!string.IsNullOrWhiteSpace(docs.XmlDocumentationText)) builder.AppendLine(docs.XmlDocumentationText);
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

    internal static string ToRoute(string value)
    {
        var text = CSharpIdentifierSanitizer.SanitizeMemberName(value).TrimEnd('_');
        var builder = new StringBuilder();
        for (var i = 0; i < text.Length; i++) { var c = text[i]; if (i > 0 && char.IsUpper(c) && (char.IsLower(text[i - 1]) || char.IsDigit(text[i - 1]))) builder.Append('-'); builder.Append(char.ToLowerInvariant(c)); }
        return builder.ToString().Replace('_', '-');
    }
}
