using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        try
        {
            var requestSettings = OpenAiModelCapabilities.Resolve(options, allowTemperature: true);
            var execution = await SendRequestAsync(prompt, apiKeyResult.ApiKey!, options, requestSettings, cancellationToken).ConfigureAwait(false);

            if (!execution.IsSuccessStatusCode
                && execution.RequestSettings.Temperature.HasValue
                && IsTemperatureUnsupportedResponse(execution.Payload))
            {
                diagnostics.Add(new OpenAiDiagnostic
                {
                    Severity = "Warning",
                    Code = "OPENAI_TEMPERATURE_RETRY",
                    StatusCode = (int)execution.StatusCode,
                    Message = $"Model {requestSettings.ModelName} rejected temperature. Retrying once without temperature."
                });

                var retrySettings = requestSettings with { Temperature = null };
                execution = await SendRequestAsync(prompt, apiKeyResult.ApiKey!, options, retrySettings, cancellationToken).ConfigureAwait(false);
            }

            if (!execution.IsSuccessStatusCode)
            {
                diagnostics.Add(new OpenAiDiagnostic
                {
                    Severity = "Warning",
                    Code = execution.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => "OPENAI_UNAUTHORIZED",
                        HttpStatusCode.TooManyRequests => "OPENAI_RATE_LIMITED",
                        HttpStatusCode.RequestTimeout => "OPENAI_TIMEOUT",
                        _ => "OPENAI_HTTP_ERROR"
                    },
                    StatusCode = (int)execution.StatusCode,
                    Message = $"OpenAI request failed with HTTP {(int)execution.StatusCode}. {ExtractFirstUsefulString(execution.Payload)}".Trim()
                });

                return new OpenAiDocumentationClientResult
                {
                    Success = false,
                    StatusCode = (int)execution.StatusCode,
                    Diagnostics = diagnostics
                };
            }

                diagnostics.Add(new OpenAiDiagnostic
                {
                    Severity = "Info",
                    Code = "OPENAI_REQUEST_SUCCEEDED",
                    StatusCode = (int)execution.StatusCode,
                    Message = apiKeyResult.SourceDescription
                });

            return new OpenAiDocumentationClientResult
            {
                Success = true,
                RawResponseText = ExtractResponseText(execution.Payload),
                StatusCode = (int)execution.StatusCode,
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

    private async Task<RequestExecutionResult> SendRequestAsync(
        string prompt,
        string apiKey,
        OpenAiDocumentationOptions options,
        OpenAiModelCapabilities.ResolvedRequestSettings requestSettings,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ResponsesUri)
        {
            Content = CreateRequestBody(prompt, options, requestSettings)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new RequestExecutionResult(response.StatusCode, response.IsSuccessStatusCode, payload, requestSettings);
    }

    private static StringContent CreateRequestBody(
        string prompt,
        OpenAiDocumentationOptions? options,
        OpenAiModelCapabilities.ResolvedRequestSettings requestSettings)
    {
        var maxOutputTokens = options?.MaxOutputTokens ?? 600;
        var request = new RequestPayload
        {
            Model = requestSettings.ModelName,
            Input =
            [
                new InputMessage
                {
                    Role = "developer",
                    Content =
                    [
                        new InputContent
                        {
                            Type = "input_text",
                            Text = "You generate concise structured JSON for C# XML documentation comments."
                        }
                    ]
                },
                new InputMessage
                {
                    Role = "user",
                    Content =
                    [
                        new InputContent
                        {
                            Type = "input_text",
                            Text = prompt
                        }
                    ]
                }
            ],
            Text = StructuredTextFormat,
            MaxOutputTokens = maxOutputTokens,
            Temperature = requestSettings.Temperature,
            Reasoning = string.IsNullOrWhiteSpace(requestSettings.ReasoningEffort)
                ? null
                : new ReasoningPayload { Effort = requestSettings.ReasoningEffort }
        };

        return new StringContent(
            JsonSerializer.Serialize(request, RequestSerializerOptions),
            Encoding.UTF8,
            "application/json");
    }

    private static StringContent CreateRequestBody(string prompt, OpenAiDocumentationOptions options)
        => CreateRequestBody(prompt, options, OpenAiModelCapabilities.Resolve(options, allowTemperature: true));

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

    private static bool IsTemperatureUnsupportedResponse(string payload)
        => payload.IndexOf("temperature is not supported with this model", StringComparison.OrdinalIgnoreCase) >= 0;

    private static readonly JsonSerializerOptions RequestSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly StructuredTextPayload StructuredTextFormat = new()
    {
        Format = new StructuredFormat
        {
            Type = "json_schema",
            Name = "wcf_method_documentation",
            Strict = true,
            Schema = new StructuredSchema
            {
                Type = "object",
                AdditionalProperties = false,
                Properties = new StructuredSchemaProperties
                {
                    Summary = new SchemaPrimitive { Type = "string" },
                    Parameters = new SchemaArray
                    {
                        Type = "array",
                        Items = new SchemaObject
                        {
                            Type = "object",
                            AdditionalProperties = false,
                            Properties = new SchemaObjectProperties
                            {
                                Name = new SchemaPrimitive { Type = "string" },
                                Description = new SchemaPrimitive { Type = "string" }
                            },
                            Required = ["name", "description"]
                        }
                    },
                    Returns = new SchemaPrimitive { Type = "string" },
                    Exceptions = new SchemaArray
                    {
                        Type = "array",
                        Items = new SchemaObject
                        {
                            Type = "object",
                            AdditionalProperties = false,
                            Properties = new SchemaObjectProperties
                            {
                                Type = new SchemaPrimitive { Type = "string" },
                                Description = new SchemaPrimitive { Type = "string" }
                            },
                            Required = ["type", "description"]
                        }
                    },
                    Remarks = new SchemaPrimitive { Type = "string" }
                },
                Required = ["summary", "parameters", "returns", "exceptions", "remarks"]
            }
        }
    };

    public sealed class OpenAiDocumentationClientResult
    {
        public bool Success { get; init; }

        public string RawResponseText { get; init; } = string.Empty;

        public int? StatusCode { get; init; }

        public IReadOnlyList<OpenAiDiagnostic> Diagnostics { get; init; } = [];
    }

    private sealed record RequestExecutionResult(
        HttpStatusCode StatusCode,
        bool IsSuccessStatusCode,
        string Payload,
        OpenAiModelCapabilities.ResolvedRequestSettings RequestSettings);

    private sealed class RequestPayload
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("input")]
        public IReadOnlyList<InputMessage> Input { get; init; } = [];

        [JsonPropertyName("text")]
        public StructuredTextPayload Text { get; init; } = new();

        [JsonPropertyName("max_output_tokens")]
        public int MaxOutputTokens { get; init; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; init; }

        [JsonPropertyName("reasoning")]
        public ReasoningPayload? Reasoning { get; init; }
    }

    private sealed class InputMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public IReadOnlyList<InputContent> Content { get; init; } = [];
    }

    private sealed class InputContent
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }

    private sealed class StructuredTextPayload
    {
        [JsonPropertyName("format")]
        public StructuredFormat Format { get; init; } = new();
    }

    private sealed class StructuredFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("strict")]
        public bool Strict { get; init; }

        [JsonPropertyName("schema")]
        public StructuredSchema Schema { get; init; } = new();
    }

    private sealed class StructuredSchema
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("additionalProperties")]
        public bool AdditionalProperties { get; init; }

        [JsonPropertyName("properties")]
        public StructuredSchemaProperties Properties { get; init; } = new();

        [JsonPropertyName("required")]
        public IReadOnlyList<string> Required { get; init; } = [];
    }

    private sealed class StructuredSchemaProperties
    {
        [JsonPropertyName("summary")]
        public SchemaPrimitive Summary { get; init; } = new();

        [JsonPropertyName("parameters")]
        public SchemaArray Parameters { get; init; } = new();

        [JsonPropertyName("returns")]
        public SchemaPrimitive Returns { get; init; } = new();

        [JsonPropertyName("exceptions")]
        public SchemaArray Exceptions { get; init; } = new();

        [JsonPropertyName("remarks")]
        public SchemaPrimitive Remarks { get; init; } = new();
    }

    private sealed class SchemaArray
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("items")]
        public SchemaObject Items { get; init; } = new();
    }

    private sealed class SchemaObject
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("additionalProperties")]
        public bool AdditionalProperties { get; init; }

        [JsonPropertyName("properties")]
        public SchemaObjectProperties Properties { get; init; } = new();

        [JsonPropertyName("required")]
        public IReadOnlyList<string> Required { get; init; } = [];
    }

    private sealed class SchemaObjectProperties
    {
        [JsonPropertyName("name")]
        public SchemaPrimitive? Name { get; init; }

        [JsonPropertyName("description")]
        public SchemaPrimitive Description { get; init; } = new();

        [JsonPropertyName("type")]
        public SchemaPrimitive? Type { get; init; }
    }

    private sealed class SchemaPrimitive
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;
    }

    private sealed class ReasoningPayload
    {
        [JsonPropertyName("effort")]
        public string Effort { get; init; } = string.Empty;
    }
}
