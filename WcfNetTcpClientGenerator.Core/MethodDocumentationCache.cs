using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WcfNetTcpClientGenerator.Core;

public sealed class MethodDocumentationCache
{
    private readonly ConcurrentDictionary<string, MethodDocumentationResult> _cache = new(StringComparer.Ordinal);

    public bool TryGet(MethodDocumentationRequest request, out MethodDocumentationResult? result)
        => _cache.TryGetValue(CreateStableHash(request), out result);

    public void Set(MethodDocumentationRequest request, MethodDocumentationResult result)
        => _cache[CreateStableHash(request)] = result;

    internal static string CreateStableHash(MethodDocumentationRequest request)
    {
        var serialized = JsonSerializer.Serialize(new
        {
            request.ServiceName,
            request.OperationName,
            request.GeneratedWrapperMethodName,
            request.RequestTypeName,
            request.ResponseTypeName,
            Parameters = request.Parameters.Select(static parameter => new { parameter.Name, parameter.TypeName, parameter.IsOptional }),
            request.ReturnType,
            Faults = request.FaultContracts.Select(static fault => new { fault.Name, fault.TypeName }),
            request.WcfBindingType,
            request.IsAsync,
            request.WsdlDocumentationText,
            request.GeneratedMethodSignature,
            request.SampleRequestTypeName,
            request.SampleResponseTypeName
        });

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return Convert.ToHexString(hashBytes);
    }
}
