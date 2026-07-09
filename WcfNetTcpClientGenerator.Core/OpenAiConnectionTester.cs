using System.Text.Json;

namespace WcfNetTcpClientGenerator.Core;

public sealed class OpenAiConnectionTester
{
    private readonly OpenAiDocumentationClient _client;

    public OpenAiConnectionTester(OpenAiDocumentationClient client)
    {
        _client = client;
    }

    public async Task<OpenAiConnectionTestResult> TestConnectionAsync(OpenAiDocumentationOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ModelName))
        {
            return new OpenAiConnectionTestResult
            {
                Success = false,
                StatusText = "An OpenAI model name is required.",
                Diagnostics =
                [
                    new OpenAiDiagnostic
                    {
                        Severity = "Error",
                        Code = "OPENAI_MODEL_REQUIRED",
                        Message = "An OpenAI model name is required."
                    }
                ]
            };
        }

        var prompt = """
Return a JSON object for a WCF wrapper method with:
- summary: "Connection test succeeded."
- parameters: []
- returns: "A test response."
- exceptions: []
- remarks: ""
""";

        var result = await _client.GenerateStructuredCommentAsync(prompt, options, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return new OpenAiConnectionTestResult
            {
                Success = false,
                ModelName = options.ModelName,
                StatusText = "OpenAI connection test failed.",
                Diagnostics = result.Diagnostics
            };
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<OpenAiStructuredCommentResponse>(result.RawResponseText, SerializerOptions);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Summary))
            {
                return new OpenAiConnectionTestResult
                {
                    Success = false,
                    ModelName = options.ModelName,
                    StatusText = "OpenAI returned an invalid structured response during the connection test.",
                    Diagnostics =
                    [
                        .. result.Diagnostics,
                        new OpenAiDiagnostic
                        {
                            Severity = "Warning",
                            Code = "OPENAI_INVALID_STRUCTURED_RESPONSE",
                            Message = "OpenAI returned an invalid structured response during the connection test."
                        }
                    ]
                };
            }

            return new OpenAiConnectionTestResult
            {
                Success = true,
                ModelName = options.ModelName,
                StatusText = $"OpenAI connection succeeded with model {options.ModelName}.",
                Diagnostics = result.Diagnostics
            };
        }
        catch (Exception exception)
        {
            return new OpenAiConnectionTestResult
            {
                Success = false,
                ModelName = options.ModelName,
                StatusText = "OpenAI structured output parsing failed during the connection test.",
                Diagnostics =
                [
                    .. result.Diagnostics,
                    new OpenAiDiagnostic
                    {
                        Severity = "Warning",
                        Code = "OPENAI_STRUCTURED_OUTPUT_PARSE_FAILED",
                        Message = exception.Message
                    }
                ]
            };
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
