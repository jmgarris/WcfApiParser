namespace WcfNetTcpClientGenerator.Core;

public interface IMethodDocumentationProvider
{
    Task<MethodDocumentationResult> GenerateDocumentationAsync(
        MethodDocumentationRequest request,
        MethodDocumentationOptions options,
        CancellationToken cancellationToken);
}
