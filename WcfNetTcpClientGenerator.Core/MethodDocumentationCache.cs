using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WcfNetTcpClientGenerator.Core;

public sealed class MethodDocumentationCache
{
    private readonly ConcurrentDictionary<string, MethodDocumentationResult> _cache = new(StringComparer.Ordinal);

    public bool TryGet(string cacheKey, out MethodDocumentationResult? result)
        => _cache.TryGetValue(cacheKey, out result);

    public void Set(string cacheKey, MethodDocumentationResult result)
        => _cache[cacheKey] = result;

    public void Clear()
        => _cache.Clear();

    public void ClearByPrefix(string prefix)
    {
        var keysToRemove = _cache.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }

    internal static string CreateStableHash(MethodDocumentationRequest request, params string[] keyParts)
    {
        var serialized = JsonSerializer.Serialize(new
        {
            KeyParts = keyParts,
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
