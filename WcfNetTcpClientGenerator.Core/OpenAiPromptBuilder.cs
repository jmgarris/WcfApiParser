using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class OpenAiPromptBuilder
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
        builder.AppendLine("You are generating structured data for C# XML documentation comments for a .NET 10 WCF client wrapper method.");
        builder.AppendLine("Return only the requested JSON object.");
        builder.AppendLine("Do not return markdown.");
        builder.AppendLine("Do not return C# code.");
        builder.AppendLine("Do not invent endpoint URLs.");
        builder.AppendLine("Do not invent credentials.");
        builder.AppendLine("Do not invent business behavior not implied by the method name, signature, or metadata.");
        builder.AppendLine("Keep comments concise.");
        builder.AppendLine("Describe the method as a WCF client wrapper method.");
        builder.AppendLine("Only include CommunicationException and TimeoutException when they are appropriate.");
        builder.AppendLine();
        builder.AppendLine($"Service name: {request.ServiceName}");
        builder.AppendLine($"WCF operation name: {request.OperationName}");
        builder.AppendLine($"Generated method name: {request.GeneratedWrapperMethodName}");
        builder.AppendLine($"Generated method signature: {request.GeneratedMethodSignature}");
        builder.AppendLine($"Request type name: {request.RequestTypeName}");
        builder.AppendLine($"Response type name: {request.ResponseTypeName}");
        builder.AppendLine($"Parameter names and types: {parameters}");
        builder.AppendLine($"Return type: {request.ReturnType}");
        builder.AppendLine($"Fault contracts: {faults}");
        builder.AppendLine($"Binding type: net.tcp / {request.WcfBindingType}");
        builder.AppendLine($"Is async: {request.IsAsync}");

        if (!string.IsNullOrWhiteSpace(request.WsdlDocumentationText))
        {
            builder.AppendLine($"WSDL documentation text: {request.WsdlDocumentationText}");
        }

        builder.AppendLine();
        builder.AppendLine("Return this JSON shape:");
        builder.AppendLine("{");
        builder.AppendLine("  \"summary\": \"string\",");
        builder.AppendLine("  \"parameters\": [{ \"name\": \"string\", \"description\": \"string\" }],");
        builder.AppendLine("  \"returns\": \"string\",");
        builder.AppendLine("  \"exceptions\": [{ \"type\": \"string\", \"description\": \"string\" }],");
        builder.AppendLine("  \"remarks\": \"string\"");
        builder.AppendLine("}");

        return builder.ToString();
    }
}
