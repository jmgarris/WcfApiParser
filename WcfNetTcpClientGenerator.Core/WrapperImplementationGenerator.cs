using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class WrapperImplementationGenerator
{
    public string Generate(WcfContractModel contract, string libraryNamespace)
    {
        var contractTypeName = CSharpIdentifierSanitizer.SanitizeTypeName(contract.ContractName);
        var interfaceName = $"I{contractTypeName}Client";
        var className = $"{contractTypeName}Client";

        var builder = new StringBuilder();
        builder.AppendLine($"using {libraryNamespace}.Binding;");
        builder.AppendLine($"using {libraryNamespace}.Interfaces;");
        builder.AppendLine($"using {libraryNamespace}.Options;");
        builder.AppendLine($"using ServiceReference = {contract.ProxyNamespace};");
        builder.AppendLine();
        builder.AppendLine($"namespace {libraryNamespace}.Services;");
        builder.AppendLine();
        builder.AppendLine($"public sealed class {className} : {interfaceName}");
        builder.AppendLine("{");
        builder.AppendLine("    private readonly NetTcpWcfClientOptions _options;");
        builder.AppendLine();
        builder.AppendLine($"    public {className}(NetTcpWcfClientOptions options)");
        builder.AppendLine("    {");
        builder.AppendLine("        _options = options ?? throw new global::System.ArgumentNullException(nameof(options));");
        builder.AppendLine("    }");
        builder.AppendLine();

        foreach (var operation in contract.Operations)
        {
            builder.AppendLine(GenerateMethod(contract, operation));
        }

        builder.AppendLine(GenerateCloseMethod());
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateMethod(WcfContractModel contract, WcfOperationModel operation)
    {
        var returnType = operation.ResponseTypeName.Equals("void", StringComparison.Ordinal)
            ? "global::System.Threading.Tasks.Task"
            : $"global::System.Threading.Tasks.Task<{operation.ResponseTypeName}>";

        var parameters = operation.Parameters.Count == 0
            ? string.Empty
            : $"{string.Join(", ", operation.Parameters.Select(static parameter => $"{parameter.TypeName} {parameter.Name}"))}, ";

        var invocationParameters = string.Join(", ", operation.Parameters.Select(static parameter => parameter.Name));
        var invocation = string.IsNullOrWhiteSpace(invocationParameters)
            ? $"await client.{operation.ProxyMethodName}().ConfigureAwait(false);"
            : $"await client.{operation.ProxyMethodName}({invocationParameters}).ConfigureAwait(false);";

        if (!operation.ResponseTypeName.Equals("void", StringComparison.Ordinal))
        {
            invocation = string.IsNullOrWhiteSpace(invocationParameters)
                ? $"return await client.{operation.ProxyMethodName}().ConfigureAwait(false);"
                : $"return await client.{operation.ProxyMethodName}({invocationParameters}).ConfigureAwait(false);";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"    public async {returnType} {operation.MethodName}({parameters}global::System.Threading.CancellationToken cancellationToken = default)");
        builder.AppendLine("    {");
        builder.AppendLine("        cancellationToken.ThrowIfCancellationRequested();");
        builder.AppendLine();
        builder.AppendLine("        var binding = NetTcpBindingFactory.Create(_options);");
        builder.AppendLine("        var endpointAddress = new global::System.ServiceModel.EndpointAddress(_options.EndpointUrl);");
        builder.AppendLine($"        var client = new ServiceReference.{contract.ClientClassName}(binding, endpointAddress);");
        builder.AppendLine();
        builder.AppendLine("        try");
        builder.AppendLine("        {");
        builder.AppendLine("            ApplyCredentials(client);");
        builder.Append("            ");
        builder.AppendLine(invocation);
        if (operation.ResponseTypeName.Equals("void", StringComparison.Ordinal))
        {
            builder.AppendLine("            CloseClient(client);");
            builder.AppendLine("            return;");
        }
        else
        {
            builder.AppendLine("        }");
            builder.AppendLine("        catch");
            builder.AppendLine("        {");
            builder.AppendLine("            client.Abort();");
            builder.AppendLine("            throw;");
            builder.AppendLine("        }");
            builder.AppendLine("        finally");
            builder.AppendLine("        {");
            builder.AppendLine("            if (client.State != global::System.ServiceModel.CommunicationState.Faulted)");
            builder.AppendLine("            {");
            builder.AppendLine("                CloseClient(client);");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            return builder.ToString();
        }

        builder.AppendLine("        }");
        builder.AppendLine("        catch");
        builder.AppendLine("        {");
        builder.AppendLine("            client.Abort();");
        builder.AppendLine("            throw;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        return builder.ToString();
    }

    private static string GenerateCloseMethod()
        => """
    private static void CloseClient(global::System.ServiceModel.ICommunicationObject client)
    {
        try
        {
            if (client.State != global::System.ServiceModel.CommunicationState.Closed)
            {
                client.Close();
            }
        }
        catch
        {
            client.Abort();
            throw;
        }
    }

    private void ApplyCredentials<TChannel>(global::System.ServiceModel.ClientBase<TChannel> client)
        where TChannel : class
    {
        if (string.Equals(_options.TcpClientCredentialType, "UserName", global::System.StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_options.Username)
            && !string.IsNullOrWhiteSpace(_options.Password))
        {
            client.ClientCredentials.UserName.UserName = _options.Username;
            client.ClientCredentials.UserName.Password = _options.Password;
        }
    }
""";
}
