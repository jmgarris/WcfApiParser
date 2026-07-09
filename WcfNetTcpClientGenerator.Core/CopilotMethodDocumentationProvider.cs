namespace WcfNetTcpClientGenerator.Core;

public sealed class CopilotMethodDocumentationProvider : IMethodDocumentationProvider
{
    private readonly IGraphAccessTokenProvider _tokenProvider;
    private readonly CopilotChatClient _chatClient;
    private readonly CopilotConversationManager _conversationManager;
    private readonly DocumentationPromptBuilder _promptBuilder;
    private readonly XmlDocumentationSanitizer _sanitizer;
    private readonly MethodDocumentationCache _cache;
    private readonly NullMethodDocumentationProvider _fallbackProvider;

    public CopilotMethodDocumentationProvider(
        IGraphAccessTokenProvider tokenProvider,
        CopilotChatClient chatClient,
        CopilotConversationManager conversationManager,
        DocumentationPromptBuilder promptBuilder,
        XmlDocumentationSanitizer sanitizer,
        MethodDocumentationCache cache,
        NullMethodDocumentationProvider fallbackProvider)
    {
        _tokenProvider = tokenProvider;
        _chatClient = chatClient;
        _conversationManager = conversationManager;
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
        if (options.ProviderKind != DocumentationProviderKind.Microsoft365Copilot)
        {
            return await _fallbackProvider.GenerateDocumentationAsync(request, options, cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = $"Microsoft365Copilot|{MethodDocumentationCache.CreateStableHash(request, "Microsoft365Copilot", options.CopilotChat.TenantId, options.CopilotChat.ClientId)}";
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
                        Code = "COPILOT_CACHE_HIT",
                        Message = $"Reused cached documentation for {request.GeneratedWrapperMethodName}."
                    }
                ]
            };
        }

        var diagnostics = new List<DocumentationGenerationDiagnostic>();
        try
        {
            var conversationResult = await _conversationManager.EnsureConversationAsync(options.CopilotChat, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(ConvertDiagnostics(conversationResult.Diagnostics));
            if (!conversationResult.Success || string.IsNullOrWhiteSpace(conversationResult.ConversationId))
            {
                return await CreateFallbackAsync(request, options, diagnostics, "Failed to create a Copilot conversation.", cancellationToken).ConfigureAwait(false);
            }

            var prompt = _promptBuilder.BuildPrompt(request);
            var attempt = 0;
            while (true)
            {
                attempt++;
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(options.Timeout);

                var tokenResult = await _tokenProvider.GetAccessTokenAsync(options.CopilotChat, timeoutSource.Token).ConfigureAwait(false);
                diagnostics.AddRange(ConvertDiagnostics(tokenResult.Diagnostics));
                if (!tokenResult.Success)
                {
                    return await CreateFallbackAsync(request, options, diagnostics, "Microsoft 365 sign-in did not provide an access token.", cancellationToken).ConfigureAwait(false);
                }

                var chatResult = await _chatClient.SendChatAsync(
                    conversationResult.ConversationId,
                    prompt,
                    tokenResult.AccessToken,
                    options.CopilotChat,
                    timeoutSource.Token).ConfigureAwait(false);

                diagnostics.AddRange(ConvertDiagnostics(chatResult.Diagnostics));

                if (!chatResult.Success)
                {
                    if (chatResult.StatusCode is 429 && attempt <= options.RetryCount + 1)
                    {
                        diagnostics.Add(new DocumentationGenerationDiagnostic
                        {
                            Severity = "Warning",
                            Code = "COPILOT_RETRY",
                            Message = $"Retrying Copilot documentation for {request.GeneratedWrapperMethodName} after throttling. Attempt {attempt}."
                        });

                        await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return await CreateFallbackAsync(request, options, diagnostics, $"Copilot failed for {request.GeneratedWrapperMethodName}; using fallback comment.", cancellationToken).ConfigureAwait(false);
                }

                var sanitizeResult = _sanitizer.Sanitize(chatResult.ResponseText, request, options.MaxCommentLength, "Copilot");
                diagnostics.AddRange(sanitizeResult.Diagnostics);
                if (!sanitizeResult.Success)
                {
                    return await CreateFallbackAsync(request, options, diagnostics, $"Copilot returned invalid XML documentation for {request.GeneratedWrapperMethodName}; using fallback comment.", cancellationToken).ConfigureAwait(false);
                }

                var aiResult = new MethodDocumentationResult
                {
                    Success = true,
                    XmlDocumentationText = sanitizeResult.XmlDocumentationText,
                    Summary = $"AI-generated documentation for {request.GeneratedWrapperMethodName}.",
                    Diagnostics = diagnostics,
                    WasGeneratedByAi = true,
                    RawProviderName = nameof(CopilotMethodDocumentationProvider)
                };

                if (options.CacheGeneratedComments)
                {
                    _cache.Set(cacheKey, aiResult);
                }

                return aiResult;
            }
        }
        catch (Exception exception)
        {
            diagnostics.Add(new DocumentationGenerationDiagnostic
            {
                Severity = "Warning",
                Code = "COPILOT_PROVIDER_EXCEPTION",
                Message = $"Copilot documentation failed for {request.GeneratedWrapperMethodName}. {exception.Message}"
            });

            return await CreateFallbackAsync(request, options, diagnostics, $"Copilot failed for {request.GeneratedWrapperMethodName}; using fallback comment.", cancellationToken).ConfigureAwait(false);
        }
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
                    Code = "COPILOT_FALLBACK",
                    Message = message
                },
                .. fallback.Diagnostics
            ],
            WasGeneratedByAi = false,
            RawProviderName = nameof(CopilotMethodDocumentationProvider)
        };
    }

    private static IReadOnlyList<DocumentationGenerationDiagnostic> ConvertDiagnostics(IReadOnlyList<CopilotChatDiagnostic> diagnostics)
        => diagnostics
            .Select(static diagnostic => new DocumentationGenerationDiagnostic
            {
                Severity = diagnostic.Severity,
                Message = diagnostic.Message,
                Code = diagnostic.Code
            })
            .ToList();
}
