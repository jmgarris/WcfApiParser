using System.Net;
using System.Net.Http;
using System.Text;
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
        var provider = CreateProvider(
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

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), EnabledOptions(), CancellationToken.None);

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
        var provider = CreateProvider(
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

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), EnabledOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WasGeneratedByAi, Is.True);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "COPILOT_RETRY"), Is.True);
        });
    }

    [Test]
    public async Task CopilotProvider_HandlesInvalidAiOutput()
    {
        var provider = CreateProvider(
            new FakeTokenProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{"id":"conversation-1"}""")
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{"content":"```xml\n<summary>broken\n```"}""")
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), EnabledOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WasGeneratedByAi, Is.False);
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "INVALID_XML"), Is.True);
        });
    }

    [Test]
    public async Task CopilotProvider_FallsBackWithoutFailingGeneration()
    {
        var provider = CreateProvider(
            new FakeTokenProvider(),
            new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("""{"id":"conversation-1"}""")
                },
                new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = JsonContent("""{"error":"forbidden"}""")
                }));

        var result = await provider.GenerateDocumentationAsync(CreateRequest(), EnabledOptions(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.XmlDocumentationText, Does.Contain("Calls the GetCustomer operation"));
            Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Code == "COPILOT_FALLBACK"), Is.True);
        });
    }

    private static CopilotMethodDocumentationProvider CreateProvider(IGraphAccessTokenProvider tokenProvider, HttpMessageHandler handler)
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

    private static MethodDocumentationRequest CreateRequest()
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
            GeneratedMethodSignature = "global::System.Threading.Tasks.Task<global::Contoso.Contracts.CustomerResponse> GetCustomerAsync(global::Contoso.Contracts.GetCustomerRequest request, global::System.Threading.CancellationToken cancellationToken)"
        };

    private static MethodDocumentationOptions EnabledOptions()
        => new()
        {
            EnableCopilotComments = true,
            RetryCount = 1,
            Timeout = TimeSpan.FromSeconds(5),
            MaxCommentLength = 1000,
            CopilotChat = new CopilotChatOptions
            {
                ClientId = "client-id",
                TenantId = "organizations"
            }
        };

    private static StringContent JsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

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

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responses.Dequeue());
    }
}
