namespace WcfNetTcpClientGenerator.Core;

public static class OpenAiModelCapabilities
{
    private static readonly HashSet<string> SupportedReasoningEfforts = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "low",
        "medium",
        "high",
        "xhigh",
        "max"
    };

    private static readonly Dictionary<string, ModelCapabilityMetadata> ExplicitModelMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-5.6-luna"] = new(UseReasoningEffort: true, SupportsTemperature: false),
        ["gpt-4.1"] = new(UseReasoningEffort: false, SupportsTemperature: true),
        ["gpt-4.1-mini"] = new(UseReasoningEffort: false, SupportsTemperature: true),
        ["gpt-4o"] = new(UseReasoningEffort: false, SupportsTemperature: true),
        ["gpt-4o-mini"] = new(UseReasoningEffort: false, SupportsTemperature: true),
        ["gpt-3.5-turbo"] = new(UseReasoningEffort: false, SupportsTemperature: true)
    };

    internal static ResolvedRequestSettings Resolve(OpenAiDocumentationOptions options, bool allowTemperature)
    {
        var modelName = (options.ModelName ?? string.Empty).Trim();
        var normalizedReasoningEffort = NormalizeReasoningEffort(options.ReasoningEffort);

        if (modelName.StartsWith("gpt-5.6", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedRequestSettings(modelName, normalizedReasoningEffort, Temperature: null);
        }

        if (modelName.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
        {
            var supportsTemperature = ExplicitModelMetadata.TryGetValue(modelName, out var metadata) && metadata.SupportsTemperature;
            return new ResolvedRequestSettings(
                modelName,
                normalizedReasoningEffort,
                supportsTemperature && allowTemperature ? options.Temperature : null);
        }

        if (ExplicitModelMetadata.TryGetValue(modelName, out var explicitMetadata))
        {
            return new ResolvedRequestSettings(
                modelName,
                explicitMetadata.UseReasoningEffort ? normalizedReasoningEffort : null,
                explicitMetadata.SupportsTemperature && allowTemperature ? options.Temperature : null);
        }

        return new ResolvedRequestSettings(modelName, ReasoningEffort: null, Temperature: null);
    }

    public static string NormalizeReasoningEffort(string? reasoningEffort)
    {
        var normalized = (reasoningEffort ?? string.Empty).Trim().ToLowerInvariant();
        return SupportedReasoningEfforts.Contains(normalized)
            ? normalized
            : "none";
    }

    internal sealed record ResolvedRequestSettings(
        string ModelName,
        string? ReasoningEffort,
        double? Temperature);

    private sealed record ModelCapabilityMetadata(bool UseReasoningEffort, bool SupportsTemperature);
}
