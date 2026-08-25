using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using WcfNetTcpClientGenerator.App.Services;
using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.Tests;

[TestFixture]
public sealed class MethodDocumentationTests
{
    [Test]
    public async Task NullProvider_ReturnsDeterministicComments()
    {
        var provider = new NullMethodDocumentationProvider();
        var result = await provider.GenerateDocumentationAsync(CreateRequest(), new MethodDocumentationOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WasGeneratedByAi, Is.False);
            Assert.That(result.XmlDocumentationText, Does.Contain("Calls the GetCustomer operation"));
            Assert.That(result.XmlDocumentationText, Does.Contain("<param name=\"request\">"));
        });
    }

    [Test]
    public void PromptBuilder_IncludesMethodSignature()
    {
        var prompt = new DocumentationPromptBuilder().BuildPrompt(CreateRequest());
        Assert.That(prompt, Does.Contain("Method signature: global::System.Threading.Tasks.Task<global::Contoso.Contracts.CustomerResponse> GetCustomerAsync(global::Contoso.Contracts.GetCustomerRequest request, global::System.Threading.CancellationToken cancellationToken)"));
    }

    [Test]
    public void PromptBuilder_DoesNotIncludeCredentials()
    {
        var prompt = new DocumentationPromptBuilder().BuildPrompt(CreateRequest());
        Assert.That(prompt, Does.Not.Contain("password").IgnoreCase);
        Assert.That(prompt, Does.Not.Contain("secret").IgnoreCase);
    }

    [Test]
    public void OpenAiPrompt_IncludesMethodSignature()
    {
        var prompt = new OpenAiPromptBuilder().BuildPrompt(CreateRequest());
        Assert.That(prompt, Does.Contain("Generated method signature: global::System.Threading.Tasks.Task<global::Contoso.Contracts.CustomerResponse> GetCustomerAsync(global::Contoso.Contracts.GetCustomerRequest request, global::System.Threading.CancellationToken cancellationToken)"));
    }

    [Test]
    public void OpenAiPrompt_DoesNotIncludeCredentials()
    {
        var prompt = new OpenAiPromptBuilder().BuildPrompt(CreateRequest());
        Assert.That(prompt, Does.Not.Contain("password").IgnoreCase);
        Assert.That(prompt, Does.Not.Contain("secret").IgnoreCase);
    }

    [Test]
    public void XmlSanitizer_RemovesMarkdown()
    {
        var sanitizer = new XmlDocumentationSanitizer();
        var result = sanitizer.Sanitize(
            """
            ```xml
            /// <summary>Loads a customer.</summary>
            /// <param name="request">Request payload.</param>
            /// <returns>A response.</returns>
            ```
            """,
            CreateRequest(),
            500);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.XmlDocumentationText, Does.Not.Contain("```"));
            Assert.That(result.XmlDocumentationText, Does.Contain("/// <summary>Loads a customer.</summary>"));
        });
    }

    [Test]
    public void XmlSanitizer_RejectsInvalidXml()
    {
        var sanitizer = new XmlDocumentationSanitizer();
        var result = sanitizer.Sanitize("/// <summary>Broken", CreateRequest(), 500);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("INVALID_XML"));
        });
    }

    [Test]
    public void XmlSanitizer_PreservesValidSummaryParamAndReturns()
    {
        var sanitizer = new XmlDocumentationSanitizer();
        var result = sanitizer.Sanitize(
            """
            <summary>Loads a customer.</summary>
            <param name="request">Request payload.</param>
            <returns>A response.</returns>
            """,
            CreateRequest(),
            500);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.XmlDocumentationText, Does.Contain("/// <summary>Loads a customer.</summary>"));
            Assert.That(result.XmlDocumentationText, Does.Contain("/// <param name=\"request\">Request payload.</param>"));
            Assert.That(result.XmlDocumentationText, Does.Contain("/// <returns>A response.</returns>"));
        });
    }

    [Test]
    public async Task CopilotProvider_HandlesUnauthorizedResponse()
    {
        var provider = CreateCopilotProvider(
            new FakeTokenProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{"id":"conversation-1"}""")
                },
                new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = JsonContent("""{"error":"unauthorized"}""")
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), CopilotOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WasGeneratedByAi, Is.False);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "COPILOT_UNAUTHORIZED"), Is.True);
        });
    }

    [Test]
    public async Task CopilotProvider_HandlesThrottlingResponse()
    {
        var provider = CreateCopilotProvider(
            new FakeTokenProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{"id":"conversation-1"}""")
                },
                new HttpResponseMessage((HttpStatusCode)429)
                {
                    Content = JsonContent("""{"error":"throttled"}""")
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{"content":"<summary>Loads a customer.</summary><param name=\"request\">Request payload.</param><param name=\"cancellationToken\">Cancellation.</param><returns>A response.</returns>"}""")
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), CopilotOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WasGeneratedByAi, Is.True);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "COPILOT_RETRY"), Is.True);
        });
    }

    [Test]
    public async Task OpenAiProvider_SelectedThroughSettings_GeneratesAiComments()
    {
        var provider = CreateOpenAiProvider(
            new FakeOpenAiApiKeyProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(ResponseWithOutputText("""{"summary":"Calls the operation.","parameters":[{"name":"request","description":"Request payload."},{"name":"cancellationToken","description":"Cancellation."}],"returns":"A response.","exceptions":[{"type":"CommunicationException","description":"Communication failure."}],"remarks":""}"""))
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WasGeneratedByAi, Is.True);
            Assert.That(result.XmlDocumentationText, Does.Contain("/// <summary>"));
        });
    }

    [Test]
    public async Task Gpt56FamilyRequest_OmitsTemperatureAndUsesReasoningEffort()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(ResponseWithOutputText("""{"summary":"Calls the operation.","parameters":[],"returns":"A response.","exceptions":[],"remarks":""}"""))
            });
        var client = new OpenAiDocumentationClient(new HttpClient(handler), new FakeOpenAiApiKeyProvider());

        var result = await client.GenerateStructuredCommentAsync(
            "Prompt",
            new OpenAiDocumentationOptions
            {
                ModelName = "gpt-5.6-luna",
                ReasoningEffort = "low",
                Temperature = 0.9d,
                MaxOutputTokens = 321
            },
            CancellationToken.None);

        using var document = JsonDocument.Parse(handler.RequestBodies.Single());
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(root.TryGetProperty("temperature", out _), Is.False);
            Assert.That(root.GetProperty("reasoning").GetProperty("effort").GetString(), Is.EqualTo("low"));
            Assert.That(root.GetProperty("max_output_tokens").GetInt32(), Is.EqualTo(321));
        });
    }

    [Test]
    public async Task Gpt5Request_OmitsTemperatureWithoutExplicitCapabilitySupport()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(ResponseWithOutputText("""{"summary":"Calls the operation.","parameters":[],"returns":"A response.","exceptions":[],"remarks":""}"""))
            });
        var client = new OpenAiDocumentationClient(new HttpClient(handler), new FakeOpenAiApiKeyProvider());

        var result = await client.GenerateStructuredCommentAsync(
            "Prompt",
            new OpenAiDocumentationOptions
            {
                ModelName = "gpt-5",
                ReasoningEffort = "medium",
                Temperature = 0.7d
            },
            CancellationToken.None);

        using var document = JsonDocument.Parse(handler.RequestBodies.Single());
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(root.TryGetProperty("temperature", out _), Is.False);
            Assert.That(root.GetProperty("reasoning").GetProperty("effort").GetString(), Is.EqualTo("medium"));
        });
    }

    [Test]
    public async Task LegacyModelRequest_KeepsTemperatureWhenCapabilityMetadataSupportsIt()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(ResponseWithOutputText("""{"summary":"Calls the operation.","parameters":[],"returns":"A response.","exceptions":[],"remarks":""}"""))
            });
        var client = new OpenAiDocumentationClient(new HttpClient(handler), new FakeOpenAiApiKeyProvider());

        var result = await client.GenerateStructuredCommentAsync(
            "Prompt",
            new OpenAiDocumentationOptions
            {
                ModelName = "gpt-4.1-mini",
                ReasoningEffort = "max",
                Temperature = 0.6d
            },
            CancellationToken.None);

        using var document = JsonDocument.Parse(handler.RequestBodies.Single());
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(root.GetProperty("temperature").GetDouble(), Is.EqualTo(0.6d));
            Assert.That(root.TryGetProperty("reasoning", out _), Is.False);
        });
    }

    [Test]
    public async Task UnsupportedTemperatureResponse_RetriesOnceWithoutTemperature()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent("""{"error":{"message":"temperature is not supported with this model"}}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(ResponseWithOutputText("""{"summary":"Calls the operation.","parameters":[],"returns":"A response.","exceptions":[],"remarks":""}"""))
            });
        var client = new OpenAiDocumentationClient(new HttpClient(handler), new FakeOpenAiApiKeyProvider());

        var result = await client.GenerateStructuredCommentAsync(
            "Prompt",
            new OpenAiDocumentationOptions
            {
                ModelName = "gpt-4.1-mini",
                Temperature = 0.4d
            },
            CancellationToken.None);

        using var firstRequest = JsonDocument.Parse(handler.RequestBodies[0]);
        using var secondRequest = JsonDocument.Parse(handler.RequestBodies[1]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(handler.RequestBodies, Has.Count.EqualTo(2));
            Assert.That(firstRequest.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.4d));
            Assert.That(secondRequest.RootElement.TryGetProperty("temperature", out _), Is.False);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_TEMPERATURE_RETRY"), Is.True);
        });
    }

    [Test]
    public async Task ApiKeyLoadedFromEnvironmentVariable()
    {
        const string variableName = "OPENAI_API_KEY";
        var originalValue = Environment.GetEnvironmentVariable(variableName);

        try
        {
            Environment.SetEnvironmentVariable(variableName, "test-key");
            var provider = new OpenAiApiKeyProvider(new FakeOpenAiSecretStore());
            var result = await provider.ResolveApiKeyAsync(new OpenAiDocumentationOptions(), CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.ApiKey, Is.EqualTo("test-key"));
                Assert.That(result.SourceDescription, Is.EqualTo("Reading OpenAI API key from environment variable."));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }

    [Test]
    public async Task MissingApiKeyProducesClearDiagnostic()
    {
        const string variableName = "OPENAI_API_KEY";
        var originalValue = Environment.GetEnvironmentVariable(variableName);

        try
        {
            Environment.SetEnvironmentVariable(variableName, null);
            var provider = new OpenAiApiKeyProvider(new FakeOpenAiSecretStore());
            var result = await provider.ResolveApiKeyAsync(new OpenAiDocumentationOptions(), CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("OPENAI_API_KEY_MISSING"));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }

    [Test]
    public async Task StructuredOpenAiResponse_ConvertsToValidXmlDocumentationComments()
    {
        var provider = CreateOpenAiProvider(
            new FakeOpenAiApiKeyProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(ResponseWithOutputText("""{"summary":"Calls the GetCustomer operation on the configured WCF net.tcp service.","parameters":[{"name":"request","description":"The request payload for the GetCustomer operation."},{"name":"cancellationToken","description":"A token used to observe cancellation requests."}],"returns":"A task that represents the asynchronous operation. The task result contains the service response.","exceptions":[{"type":"CommunicationException","description":"Thrown when the communication channel faults."},{"type":"TimeoutException","description":"Thrown when the operation times out."}],"remarks":"Review the generated wrapper behavior against the target service."}"""))
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.XmlDocumentationText, Does.Contain("/// <summary>"));
            Assert.That(result.XmlDocumentationText, Does.Contain("/// <exception cref=\"CommunicationException\">"));
            Assert.That(result.XmlDocumentationText, Does.Contain("/// <remarks>"));
        });
    }

    [Test]
    public async Task InvalidStructuredResponse_FallsBackToLocalComments()
    {
        var provider = CreateOpenAiProvider(
            new FakeOpenAiApiKeyProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(ResponseWithOutputText("""{"summary":"","parameters":[],"returns":"","exceptions":[],"remarks":""}"""))
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WasGeneratedByAi, Is.False);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_INVALID_STRUCTURED_RESPONSE"), Is.True);
        });
    }

    [Test]
    public async Task UnknownParameterNames_AreRemoved()
    {
        var provider = CreateOpenAiProvider(
            new FakeOpenAiApiKeyProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(ResponseWithOutputText("""{"summary":"Calls the operation.","parameters":[{"name":"request","description":"Request payload."},{"name":"unexpected","description":"Should be dropped."}],"returns":"A response.","exceptions":[],"remarks":""}"""))
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.XmlDocumentationText, Does.Contain("name=\"request\""));
            Assert.That(result.XmlDocumentationText, Does.Not.Contain("unexpected"));
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_UNKNOWN_PARAMETER_REMOVED"), Is.True);
        });
    }

    [Test]
    public async Task XmlSpecialCharacters_AreEscaped()
    {
        var provider = CreateOpenAiProvider(
            new FakeOpenAiApiKeyProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(ResponseWithOutputText("""{"summary":"Calls <GetCustomer> & validates input.","parameters":[{"name":"request","description":"Contains <criteria> & filters."},{"name":"cancellationToken","description":"Supports cancellation."}],"returns":"A response & status.","exceptions":[],"remarks":""}"""))
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.XmlDocumentationText, Does.Contain("&lt;GetCustomer&gt;"));
            Assert.That(result.XmlDocumentationText, Does.Contain("&amp;"));
        });
    }

    [Test]
    public async Task MarkdownOutput_IsRejected()
    {
        var provider = CreateOpenAiProvider(
            new FakeOpenAiApiKeyProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(ResponseWithOutputText("```json { } ```"))
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.WasGeneratedByAi, Is.False);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_FALLBACK"), Is.True);
        });
    }

    [Test]
    public async Task GeneratedCSharpCodeOutput_IsRejected()
    {
        var provider = CreateOpenAiProvider(
            new FakeOpenAiApiKeyProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent(ResponseWithOutputText("""namespace Example { public class Bad {} }"""))
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.WasGeneratedByAi, Is.False);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_FALLBACK"), Is.True);
        });
    }

    [Test]
    public async Task UnauthorizedResponse_FallsBackWithoutFailingGeneration()
    {
        var provider = CreateOpenAiProvider(
            new FakeOpenAiApiKeyProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = JsonContent("""{"error":"unauthorized"}""")
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WasGeneratedByAi, Is.False);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_UNAUTHORIZED"), Is.True);
        });
    }

    [Test]
    public async Task RateLimitResponse_FallsBackWithoutFailingGeneration()
    {
        var provider = CreateOpenAiProvider(
            new FakeOpenAiApiKeyProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = JsonContent("""{"error":"rate limited"}""")
                },
                new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = JsonContent("""{"error":"rate limited"}""")
                },
                new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = JsonContent("""{"error":"rate limited"}""")
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WasGeneratedByAi, Is.False);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_RETRY"), Is.True);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_FALLBACK"), Is.True);
        });
    }

    [Test]
    public async Task Timeout_FallsBackWithoutFailingGeneration()
    {
        var provider = CreateOpenAiProvider(
            new FakeOpenAiApiKeyProvider(),
            new ThrowingHandler(new OperationCanceledException("timed out")));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WasGeneratedByAi, Is.False);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_TIMEOUT"), Is.True);
        });
    }

    [Test]
    public async Task CacheKeyChangesWhenMethodSignatureChanges()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(ResponseWithOutputText("""{"summary":"First.","parameters":[{"name":"request","description":"Request."},{"name":"cancellationToken","description":"Cancellation."}],"returns":"A response.","exceptions":[],"remarks":""}"""))
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(ResponseWithOutputText("""{"summary":"Second.","parameters":[{"name":"request","description":"Request."},{"name":"cancellationToken","description":"Cancellation."}],"returns":"A response.","exceptions":[],"remarks":""}"""))
            });

        var provider = CreateOpenAiProvider(new FakeOpenAiApiKeyProvider(), handler);
        var firstRequest = CreateRequest();
        var secondRequest = CreateRequest(
            generatedMethodSignature: "global::System.Threading.Tasks.Task<global::Contoso.Contracts.CustomerResponse> GetCustomerAsync(global::Contoso.Contracts.GetCustomerRequest request, int version, global::System.Threading.CancellationToken cancellationToken)");

        var firstResult = await provider.GenerateDocumentationAsync(firstRequest, OpenAiOptions(), CancellationToken.None);
        var secondResult = await provider.GenerateDocumentationAsync(firstRequest, OpenAiOptions(), CancellationToken.None);
        var thirdResult = await provider.GenerateDocumentationAsync(secondRequest, OpenAiOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(firstResult.WasGeneratedByAi, Is.True);
            Assert.That(secondResult.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_CACHE_HIT"), Is.True);
            Assert.That(thirdResult.Diagnostics.Any(static diagnostic => diagnostic.Code == "OPENAI_CACHE_HIT"), Is.False);
            Assert.That(handler.RequestCount, Is.EqualTo(2));
        });
    }

    private static CopilotMethodDocumentationProvider CreateCopilotProvider(IGraphAccessTokenProvider tokenProvider, HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var chatClient = new CopilotChatClient(httpClient);
        var conversationManager = new CopilotConversationManager(chatClient, tokenProvider);
        return new CopilotMethodDocumentationProvider(
            tokenProvider,
            chatClient,
            conversationManager,
            new DocumentationPromptBuilder(),
            new XmlDocumentationSanitizer(),
            new MethodDocumentationCache(),
            new NullMethodDocumentationProvider());
    }

    private static OpenAiMethodDocumentationProvider CreateOpenAiProvider(IOpenAiApiKeyProvider apiKeyProvider, HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var client = new OpenAiDocumentationClient(httpClient, apiKeyProvider);
        return new OpenAiMethodDocumentationProvider(
            client,
            new OpenAiPromptBuilder(),
            new XmlDocumentationSanitizer(),
            new MethodDocumentationCache(),
            new NullMethodDocumentationProvider());
    }

    private static MethodDocumentationRequest CreateRequest(string? generatedMethodSignature = null)
        => new()
        {
            ServiceName = "CustomerService",
            OperationName = "GetCustomer",
            GeneratedWrapperMethodName = "GetCustomerAsync",
            RequestTypeName = "global::Contoso.Contracts.GetCustomerRequest",
            ResponseTypeName = "global::Contoso.Contracts.CustomerResponse",
            Parameters =
            [
                new WcfParameterModel
                {
                    Name = "request",
                    TypeName = "global::Contoso.Contracts.GetCustomerRequest"
                },
                new WcfParameterModel
                {
                    Name = "cancellationToken",
                    TypeName = "global::System.Threading.CancellationToken"
                }
            ],
            ReturnType = "global::System.Threading.Tasks.Task<global::Contoso.Contracts.CustomerResponse>",
            WcfBindingType = "NetTcpBinding",
            IsAsync = true,
            GeneratedMethodSignature = generatedMethodSignature ?? "global::System.Threading.Tasks.Task<global::Contoso.Contracts.CustomerResponse> GetCustomerAsync(global::Contoso.Contracts.GetCustomerRequest request, global::System.Threading.CancellationToken cancellationToken)"
        };

    private static MethodDocumentationOptions CopilotOptions()
        => new()
        {
            ProviderKind = DocumentationProviderKind.Microsoft365Copilot,
            RetryCount = 1,
            Timeout = TimeSpan.FromSeconds(5),
            MaxCommentLength = 1000,
            CopilotChat = new CopilotChatOptions
            {
                ClientId = "client-id",
                TenantId = "organizations"
            }
        };

    private static MethodDocumentationOptions OpenAiOptions()
        => new()
        {
            ProviderKind = DocumentationProviderKind.OpenAI,
            RetryCount = 1,
            Timeout = TimeSpan.FromSeconds(5),
            MaxCommentLength = 1000,
            OpenAi = new OpenAiDocumentationOptions
            {
                ModelName = "gpt-5.6-luna",
                MaxOutputTokens = 300,
                ReasoningEffort = "none",
                Temperature = 0.2d
            }
        };

    private static StringContent JsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

    private static string ResponseWithOutputText(string outputText)
        => $$"""{"output_text":{{System.Text.Json.JsonSerializer.Serialize(outputText)}}}""";

    private sealed class FakeTokenProvider : IGraphAccessTokenProvider
    {
        public Task<GraphAccessTokenResult> GetAccessTokenAsync(CopilotChatOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new GraphAccessTokenResult
            {
                Success = true,
                AccessToken = "token",
                AccountName = "tester@example.com"
            });
    }

    private sealed class FakeOpenAiApiKeyProvider : IOpenAiApiKeyProvider
    {
        public Task<OpenAiApiKeyResult> ResolveApiKeyAsync(OpenAiDocumentationOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new OpenAiApiKeyResult
            {
                Success = true,
                ApiKey = "test-key",
                SourceDescription = "Reading OpenAI API key from environment variable."
            });
    }

    private sealed class FakeOpenAiSecretStore : IOpenAiSecretStore
    {
        private string? _apiKey;

        public Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken)
        {
            _apiKey = apiKey;
            return Task.CompletedTask;
        }

        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken)
            => Task.FromResult(_apiKey);

        public Task ClearApiKeyAsync(CancellationToken cancellationToken)
        {
            _apiKey = null;
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(_exception);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public RecordingHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}
