using System.Security;
using System.Text.Json;

namespace WcfNetTcpClientGenerator.Core;

public sealed class OpenAiMethodDocumentationProvider : IMethodDocumentationProvider
{
    private static readonly HashSet<string> ApprovedExceptionTypes = new(StringComparer.Ordinal)
    {
        "CommunicationException",
        "TimeoutException"
    };

    private readonly OpenAiDocumentationClient _client;
    private readonly OpenAiPromptBuilder _promptBuilder;
    private readonly XmlDocumentationSanitizer _sanitizer;
    private readonly MethodDocumentationCache _cache;
    private readonly NullMethodDocumentationProvider _fallbackProvider;

    public OpenAiMethodDocumentationProvider(
        OpenAiDocumentationClient client,
        OpenAiPromptBuilder promptBuilder,
        XmlDocumentationSanitizer sanitizer,
        MethodDocumentationCache cache,
        NullMethodDocumentationProvider fallbackProvider)
    {
        _client = client;
        _promptBuilder = promptBuilder;
        _sanitizer = sanitizer;
        _cache = cache;
        _fallbackProvider = fallbackProvider;
    }

    public async Task<MethodDocumentationResult> GenerateDocumentationAsync(
        MethodDocumentationRequest request,
        MethodDocumentationOptions options,
        CancellationToken cancellationToken)
    {
        if (options.ProviderKind != DocumentationProviderKind.OpenAI)
        {
            return await _fallbackProvider.GenerateDocumentationAsync(request, options, cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = $"OpenAI|{MethodDocumentationCache.CreateStableHash(request, "OpenAI", options.OpenAi.ModelName)}";
        if (options.CacheGeneratedComments && !options.RegenerateComments && _cache.TryGet(cacheKey, out var cached) && cached is not null)
        {
            return cached with
            {
                Diagnostics =
                [
                    .. cached.Diagnostics,
                    new DocumentationGenerationDiagnostic
                    {
                        Severity = "Info",
                        Code = "OPENAI_CACHE_HIT",
                        Message = $"Reused cached OpenAI documentation for {request.GeneratedWrapperMethodName}."
                    }
                ]
            };
        }

        var diagnostics = new List<DocumentationGenerationDiagnostic>();
        try
        {
            var prompt = _promptBuilder.BuildPrompt(request);
            var attempt = 0;

            while (true)
            {
                attempt++;
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(options.Timeout);

                var clientResult = await _client.GenerateStructuredCommentAsync(prompt, options.OpenAi, timeoutSource.Token).ConfigureAwait(false);
                diagnostics.AddRange(ConvertDiagnostics(clientResult.Diagnostics));

                if (!clientResult.Success)
                {
                    if (clientResult.StatusCode is 429 && attempt <= options.RetryCount + 1)
                    {
                        diagnostics.Add(new DocumentationGenerationDiagnostic
                        {
                            Severity = "Warning",
                            Code = "OPENAI_RETRY",
                            Message = $"Retrying OpenAI documentation for {request.GeneratedWrapperMethodName} after rate limiting. Attempt {attempt}."
                        });

                        await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return await CreateFallbackAsync(request, options, diagnostics, $"OpenAI failed for {request.GeneratedWrapperMethodName}; using fallback comment.", cancellationToken).ConfigureAwait(false);
                }

                if (ContainsRejectedContent(clientResult.RawResponseText))
                {
                    return await CreateFallbackAsync(request, options, diagnostics, "OpenAI response failed validation; using fallback comment.", cancellationToken).ConfigureAwait(false);
                }

                OpenAiStructuredCommentResponse? parsedResponse;
                try
                {
                    parsedResponse = JsonSerializer.Deserialize<OpenAiStructuredCommentResponse>(clientResult.RawResponseText, SerializerOptions);
                }
                catch (Exception exception)
                {
                    diagnostics.Add(new DocumentationGenerationDiagnostic
                    {
                        Severity = "Warning",
                        Code = "OPENAI_STRUCTURED_OUTPUT_PARSE_FAILED",
                        Message = $"Structured OpenAI response parsing failed. {exception.Message}"
                    });

                    return await CreateFallbackAsync(request, options, diagnostics, "OpenAI response failed validation; using fallback comment.", cancellationToken).ConfigureAwait(false);
                }

                if (parsedResponse is null || string.IsNullOrWhiteSpace(parsedResponse.Summary))
                {
                    diagnostics.Add(new DocumentationGenerationDiagnostic
                    {
                        Severity = "Warning",
                        Code = "OPENAI_INVALID_STRUCTURED_RESPONSE",
                        Message = "OpenAI returned an empty or invalid structured response."
                    });

                    return await CreateFallbackAsync(request, options, diagnostics, "OpenAI response failed validation; using fallback comment.", cancellationToken).ConfigureAwait(false);
                }

                var xmlDocumentation = BuildXmlDocumentation(parsedResponse, request, diagnostics);
                var sanitizeResult = _sanitizer.Sanitize(xmlDocumentation, request, options.MaxCommentLength, "OpenAI");
                diagnostics.AddRange(sanitizeResult.Diagnostics);
                if (!sanitizeResult.Success)
                {
                    return await CreateFallbackAsync(request, options, diagnostics, "OpenAI response failed validation; using fallback comment.", cancellationToken).ConfigureAwait(false);
                }

                var result = new MethodDocumentationResult
                {
                    Success = true,
                    XmlDocumentationText = sanitizeResult.XmlDocumentationText,
                    Summary = parsedResponse.Summary,
                    Remarks = parsedResponse.Remarks,
                    Diagnostics = diagnostics,
                    WasGeneratedByAi = true,
                    RawProviderName = nameof(OpenAiMethodDocumentationProvider)
                };

                if (options.CacheGeneratedComments)
                {
                    _cache.Set(cacheKey, result);
                }

                return result;
            }
        }
        catch (Exception exception)
        {
            diagnostics.Add(new DocumentationGenerationDiagnostic
            {
                Severity = "Warning",
                Code = "OPENAI_PROVIDER_EXCEPTION",
                Message = $"OpenAI documentation failed for {request.GeneratedWrapperMethodName}. {exception.Message}"
            });

            return await CreateFallbackAsync(request, options, diagnostics, $"OpenAI failed for {request.GeneratedWrapperMethodName}; using fallback comment.", cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildXmlDocumentation(
        OpenAiStructuredCommentResponse response,
        MethodDocumentationRequest request,
        ICollection<DocumentationGenerationDiagnostic> diagnostics)
    {
        var lines = new List<string>
        {
            "/// <summary>",
            $"/// {Escape(response.Summary)}",
            "/// </summary>"
        };

        var validParameterNames = request.Parameters.Select(static parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var parameter in response.Parameters)
        {
            if (!validParameterNames.Contains(parameter.Name))
            {
                diagnostics.Add(new DocumentationGenerationDiagnostic
                {
                    Severity = "Warning",
                    Code = "OPENAI_UNKNOWN_PARAMETER_REMOVED",
                    Message = $"Removed documentation for unknown parameter '{parameter.Name}'."
                });
                continue;
            }

            lines.Add($"/// <param name=\"{Escape(parameter.Name)}\">{Escape(parameter.Description)}</param>");
        }

        if (!string.IsNullOrWhiteSpace(response.Returns))
        {
            lines.Add($"/// <returns>{Escape(response.Returns)}</returns>");
        }

        foreach (var exception in response.Exceptions)
        {
            var normalizedType = exception.Type.Replace("global::", string.Empty, StringComparison.Ordinal);
            normalizedType = normalizedType.Contains('.', StringComparison.Ordinal)
                ? normalizedType.Split('.').Last()
                : normalizedType;

            if (!ApprovedExceptionTypes.Contains(normalizedType))
            {
                diagnostics.Add(new DocumentationGenerationDiagnostic
                {
                    Severity = "Warning",
                    Code = "OPENAI_EXCEPTION_REMOVED",
                    Message = $"Removed unapproved exception documentation for '{exception.Type}'."
                });
                continue;
            }

            lines.Add($"/// <exception cref=\"{Escape(normalizedType)}\">{Escape(exception.Description)}</exception>");
        }

        if (!string.IsNullOrWhiteSpace(response.Remarks))
        {
            lines.Add($"/// <remarks>{Escape(response.Remarks)}</remarks>");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task<MethodDocumentationResult> CreateFallbackAsync(
        MethodDocumentationRequest request,
        MethodDocumentationOptions options,
        IReadOnlyList<DocumentationGenerationDiagnostic> diagnostics,
        string message,
        CancellationToken cancellationToken)
    {
        var fallback = await _fallbackProvider.GenerateDocumentationAsync(request, options, cancellationToken).ConfigureAwait(false);
        return new MethodDocumentationResult
        {
            Success = fallback.Success,
            XmlDocumentationText = fallback.XmlDocumentationText,
            Summary = fallback.Summary,
            Remarks = fallback.Remarks,
            Diagnostics =
            [
                .. diagnostics,
                new DocumentationGenerationDiagnostic
                {
                    Severity = "Warning",
                    Code = "OPENAI_FALLBACK",
                    Message = message
                },
                .. fallback.Diagnostics
            ],
            WasGeneratedByAi = false,
            RawProviderName = nameof(OpenAiMethodDocumentationProvider)
        };
    }

    private static IReadOnlyList<DocumentationGenerationDiagnostic> ConvertDiagnostics(IReadOnlyList<OpenAiDiagnostic> diagnostics)
        => diagnostics
            .Select(static diagnostic => new DocumentationGenerationDiagnostic
            {
                Severity = diagnostic.Severity,
                Message = diagnostic.Message,
                Code = diagnostic.Code
            })
            .ToList();

    private static bool ContainsRejectedContent(string rawResponseText)
        => rawResponseText.Contains("```", StringComparison.Ordinal)
           || rawResponseText.Contains("public class", StringComparison.OrdinalIgnoreCase)
           || rawResponseText.Contains("public async", StringComparison.OrdinalIgnoreCase)
           || rawResponseText.Contains("namespace ", StringComparison.OrdinalIgnoreCase);

    private static string Escape(string value)
        => SecurityElement.Escape(value) ?? value;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
