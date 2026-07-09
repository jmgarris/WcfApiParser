using System.Security;
using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class NullMethodDocumentationProvider : IMethodDocumentationProvider
{
    public Task<MethodDocumentationResult> GenerateDocumentationAsync(
        MethodDocumentationRequest request,
        MethodDocumentationOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var summary = $"Calls the {request.OperationName} operation on the configured WCF service.";
        var builder = new StringBuilder();
        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// {Escape(summary)}");
        builder.AppendLine("/// </summary>");

        foreach (var parameter in request.Parameters)
        {
            var description = parameter.Name.Equals("cancellationToken", StringComparison.Ordinal)
                ? "A token used to observe cancellation requests."
                : $"The {parameter.Name} payload for the operation.";

            builder.AppendLine($"/// <param name=\"{Escape(parameter.Name)}\">{Escape(description)}</param>");
        }

        if (!string.Equals(request.ReturnType, "global::System.Threading.Tasks.Task", StringComparison.Ordinal))
        {
            builder.AppendLine("/// <returns>A task that represents the asynchronous WCF call. The task result contains the service response.</returns>");
        }
        else
        {
            builder.AppendLine("/// <returns>A task that represents the asynchronous WCF call.</returns>");
        }

        builder.AppendLine("/// <exception cref=\"CommunicationException\">Thrown when the WCF service cannot be reached or the channel faults.</exception>");
        builder.AppendLine("/// <exception cref=\"TimeoutException\">Thrown when the WCF operation exceeds the configured timeout.</exception>");

        return Task.FromResult(new MethodDocumentationResult
        {
            Success = true,
            XmlDocumentationText = builder.ToString().TrimEnd(),
            Summary = summary,
            Diagnostics =
            [
                new DocumentationGenerationDiagnostic
                {
                    Severity = "Info",
                    Code = "DETERMINISTIC_DOCUMENTATION",
                    Message = $"Generated deterministic XML documentation for {request.GeneratedWrapperMethodName}."
                }
            ],
            WasGeneratedByAi = false,
            RawProviderName = nameof(NullMethodDocumentationProvider)
        });
    }

    private static string Escape(string value)
        => SecurityElement.Escape(value) ?? value;
}
