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
        builder.AppendLine($"public interface {interfaceName}");
        builder.AppendLine("{");

        foreach (var operation in contract.Operations)
        {
            var returnType = operation.ResponseTypeName.Equals("void", StringComparison.Ordinal)
                ? "global::System.Threading.Tasks.Task"
                : $"global::System.Threading.Tasks.Task<{operation.ResponseTypeName}>";

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
