using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class WrapperInterfaceGenerator
{
    public string Generate(WcfContractModel contract, string libraryNamespace)
    {
        var interfaceName = $"I{CSharpIdentifierSanitizer.SanitizeTypeName(contract.ContractName)}Client";
        var builder = new StringBuilder();

        builder.AppendLine($"namespace {libraryNamespace}.Interfaces;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Provides a strongly typed client for the {contract.ContractName} WCF contract.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"public interface {interfaceName}");
        builder.AppendLine("{");

        foreach (var operation in contract.Operations)
        {
            var returnType = operation.ResponseTypeName.Equals("void", StringComparison.Ordinal)
                ? "global::System.Threading.Tasks.Task"
                : $"global::System.Threading.Tasks.Task<{operation.ResponseTypeName}>";

            builder.AppendLine("    /// <summary>");
            builder.AppendLine($"    /// Calls the {operation.OperationName} operation on the configured WCF service.");
            builder.AppendLine("    /// </summary>");
            foreach (var parameter in operation.Parameters)
            {
                builder.AppendLine($"    /// <param name=\"{parameter.Name}\">The {parameter.Name} payload for the operation.</param>");
            }
            builder.AppendLine("    /// <param name=\"cancellationToken\">A token used to observe cancellation requests.</param>");
            builder.AppendLine(operation.ResponseTypeName.Equals("void", StringComparison.Ordinal)
                ? "    /// <returns>A task that represents the asynchronous WCF call.</returns>"
                : "    /// <returns>A task that represents the asynchronous WCF call and its service response.</returns>");
            builder.Append("    ");
            builder.Append(returnType);
            builder.Append(' ');
            builder.Append(operation.MethodName);
            builder.Append('(');

            if (operation.Parameters.Count > 0)
            {
                builder.Append(string.Join(", ", operation.Parameters.Select(static parameter => $"{parameter.TypeName} {parameter.Name}")));
                builder.Append(", ");
            }

            builder.Append("global::System.Threading.CancellationToken cancellationToken = default);");
            builder.AppendLine();
        }

        builder.AppendLine("}");

        return builder.ToString();
    }
}
