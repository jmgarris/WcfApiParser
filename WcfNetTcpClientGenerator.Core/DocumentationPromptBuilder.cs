using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class DocumentationPromptBuilder
{
    public string BuildPrompt(MethodDocumentationRequest request)
    {
        var parameters = request.Parameters.Count == 0
            ? "None"
            : string.Join(", ", request.Parameters.Select(static parameter => $"{parameter.TypeName} {parameter.Name}"));

        var faults = request.FaultContracts.Count == 0
            ? "None"
            : string.Join(", ", request.FaultContracts.Select(static fault => $"{fault.TypeName} ({fault.Name})"));

        var builder = new StringBuilder();
        builder.AppendLine("You are generating XML documentation comments for a C# .NET 10 WCF client wrapper method.");
        builder.AppendLine("Return only valid C# XML documentation comments.");
        builder.AppendLine("Do not include markdown.");
        builder.AppendLine("Do not include code.");
        builder.AppendLine("Do not invent service behavior that is not implied by the method signature or metadata.");
        builder.AppendLine();
        builder.AppendLine($"Service name: {request.ServiceName}");
        builder.AppendLine($"Operation name: {request.OperationName}");
        builder.AppendLine($"Method signature: {request.GeneratedMethodSignature}");
        builder.AppendLine($"Request type: {request.RequestTypeName}");
        builder.AppendLine($"Response type: {request.ResponseTypeName}");
        builder.AppendLine($"Parameters: {parameters}");
        builder.AppendLine($"Fault contracts: {faults}");
        builder.AppendLine($"Binding: net.tcp / {request.WcfBindingType}");

        if (!string.IsNullOrWhiteSpace(request.WsdlDocumentationText))
        {
            builder.AppendLine($"WSDL documentation: {request.WsdlDocumentationText}");
        }

        builder.AppendLine();
        builder.AppendLine("Generate:");
        builder.AppendLine("* summary");
        builder.AppendLine("* param tags");
        builder.AppendLine("* returns tag");
        builder.AppendLine("* exception tags for common WCF failures only when appropriate");

        return builder.ToString();
    }
}
