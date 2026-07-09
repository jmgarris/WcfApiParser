using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WcfNetTcpClientGenerator.Core;

public sealed class OpenAiDocumentationClient
{
    private static readonly Uri ResponsesUri = new("https://api.openai.com/v1/responses");
    private readonly HttpClient _httpClient;
    private readonly IOpenAiApiKeyProvider _apiKeyProvider;

    public OpenAiDocumentationClient(HttpClient httpClient, IOpenAiApiKeyProvider apiKeyProvider)
    {
        _httpClient = httpClient;
        _apiKeyProvider = apiKeyProvider;
    }

    public async Task<OpenAiDocumentationClientResult> GenerateStructuredCommentAsync(
        string prompt,
        OpenAiDocumentationOptions options,
        CancellationToken cancellationToken)
    {
        var apiKeyResult = await _apiKeyProvider.ResolveApiKeyAsync(options, cancellationToken).ConfigureAwait(false);
        var diagnostics = apiKeyResult.Diagnostics.ToList();
        if (!apiKeyResult.Success)
        {
            return new OpenAiDocumentationClientResult
            {
                Success = false,
                Diagnostics = diagnostics
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, ResponsesUri)
        {
            Content = CreateRequestBody(prompt, options)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKeyResult.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                diagnostics.Add(new OpenAiDiagnostic
                {
                    Severity = "Warning",
                    Code = response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => "OPENAI_UNAUTHORIZED",
                        HttpStatusCode.TooManyRequests => "OPENAI_RATE_LIMITED",
                        HttpStatusCode.RequestTimeout => "OPENAI_TIMEOUT",
                        _ => "OPENAI_HTTP_ERROR"
                    },
                    StatusCode = (int)response.StatusCode,
                    Message = $"OpenAI request failed with HTTP {(int)response.StatusCode}. {ExtractFirstUsefulString(payload)}".Trim()
                });

                return new OpenAiDocumentationClientResult
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    Diagnostics = diagnostics
                };
            }

            diagnostics.Add(new OpenAiDiagnostic
            {
                Severity = "Info",
                Code = "OPENAI_REQUEST_SUCCEEDED",
                StatusCode = (int)response.StatusCode,
                Message = apiKeyResult.SourceDescription
            });

            return new OpenAiDocumentationClientResult
            {
                Success = true,
                RawResponseText = ExtractResponseText(payload),
                StatusCode = (int)response.StatusCode,
                Diagnostics = diagnostics
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            diagnostics.Add(new OpenAiDiagnostic
            {
                Severity = "Warning",
                Code = "OPENAI_TIMEOUT",
                Message = "The OpenAI request timed out."
            });

            return new OpenAiDocumentationClientResult
            {
                Success = false,
                Diagnostics = diagnostics
            };
        }
        catch (Exception exception)
        {
            diagnostics.Add(new OpenAiDiagnostic
            {
                Severity = "Warning",
                Code = "OPENAI_REQUEST_EXCEPTION",
                Message = $"The OpenAI request failed. {exception.Message}"
            });

            return new OpenAiDocumentationClientResult
            {
                Success = false,
                Diagnostics = diagnostics
            };
        }
    }

    private static StringContent CreateRequestBody(string prompt, OpenAiDocumentationOptions options)
    {
        var request = new
        {
            model = options.ModelName,
            input = new object[]
            {
                new
                {
                    role = "developer",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text = "You generate concise structured JSON for C# XML documentation comments."
                        }
                    }
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text = prompt
                        }
                    }
                }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "wcf_method_documentation",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            summary = new { type = "string" },
                            parameters = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    additionalProperties = false,
                                    properties = new
                                    {
                                        name = new { type = "string" },
                                        description = new { type = "string" }
                                    },
                                    required = new[] { "name", "description" }
                                }
                            },
                            returns = new { type = "string" },
                            exceptions = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    additionalProperties = false,
                                    properties = new
                                    {
                                        type = new { type = "string" },
                                        description = new { type = "string" }
                                    },
                                    required = new[] { "type", "description" }
                                }
                            },
                            remarks = new { type = "string" }
                        },
                        required = new[] { "summary", "parameters", "returns", "exceptions", "remarks" }
                    }
                }
            },
            max_output_tokens = options.MaxOutputTokens,
            temperature = options.Temperature
        };

        return new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
    }

    private static string ExtractResponseText(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return FindStringByPropertyName(document.RootElement, "output_text")
                ?? FindStructuredText(document.RootElement)
                ?? payload;
        }
        catch
        {
            return payload;
        }
    }

    private static string ExtractFirstUsefulString(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return FindFirstString(document.RootElement) ?? string.Empty;
        }
        catch
        {
            return payload;
        }
    }

    private static string? FindStructuredText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("text") && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }

                var nested = FindStructuredText(property.Value);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindStructuredText(item);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static string? FindStringByPropertyName(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName) && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }

                var nested = FindStringByPropertyName(property.Value, propertyName);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindStringByPropertyName(item, propertyName);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static string? FindFirstString(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object => element.EnumerateObject().Select(property => FindFirstString(property.Value)).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
            JsonValueKind.Array => element.EnumerateArray().Select(FindFirstString).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
            _ => null
        };

    public sealed class OpenAiDocumentationClientResult
    {
        public bool Success { get; init; }

        public string RawResponseText { get; init; } = string.Empty;

        public int? StatusCode { get; init; }

        public IReadOnlyList<OpenAiDiagnostic> Diagnostics { get; init; } = [];
    }
}
