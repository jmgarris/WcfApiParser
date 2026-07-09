using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WcfNetTcpClientGenerator.Core;

public sealed class CopilotChatClient
{
    private static readonly Uri ConversationsUri = new("https://graph.microsoft.com/beta/copilot/conversations");
    private readonly HttpClient _httpClient;

    public CopilotChatClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CopilotChatResult> CreateConversationAsync(
        string accessToken,
        CopilotChatOptions options,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ConversationsUri)
        {
            Content = CreateJsonContent(options)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CopilotChatResult> SendChatAsync(
        string conversationId,
        string prompt,
        string accessToken,
        CopilotChatOptions options,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"https://graph.microsoft.com/beta/copilot/conversations/{Uri.EscapeDataString(conversationId)}/chat"))
        {
            Content = CreateJsonContent(options, prompt)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static StringContent CreateJsonContent(CopilotChatOptions options, string? prompt = null)
    {
        var payload = prompt is null
            ? JsonSerializer.Serialize(new
            {
                disableWebGrounding = options.DisableWebGrounding
            })
            : JsonSerializer.Serialize(new
            {
                message = new
                {
                    content = prompt
                },
                disableWebGrounding = options.DisableWebGrounding
            });

        return new StringContent(payload, Encoding.UTF8, "application/json");
    }

    private async Task<CopilotChatResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new CopilotChatResult
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    Diagnostics =
                    [
                        new CopilotChatDiagnostic
                        {
                            Severity = "Warning",
                            Code = response.StatusCode switch
                            {
                                HttpStatusCode.Unauthorized => "COPILOT_UNAUTHORIZED",
                                HttpStatusCode.Forbidden => "COPILOT_FORBIDDEN",
                                (HttpStatusCode)429 => "COPILOT_THROTTLED",
                                _ => "COPILOT_HTTP_ERROR"
                            },
                            StatusCode = (int)response.StatusCode,
                            Message = $"Copilot API request failed with HTTP {(int)response.StatusCode}. {ExtractFirstUsefulString(payload)}".Trim()
                        }
                    ]
                };
            }

            var conversationId = TryExtractConversationId(payload);
            var responseText = ExtractResponseText(payload);

            return new CopilotChatResult
            {
                Success = true,
                ConversationId = conversationId,
                ResponseText = responseText,
                StatusCode = (int)response.StatusCode,
                Diagnostics =
                [
                    new CopilotChatDiagnostic
                    {
                        Severity = "Info",
                        Code = "COPILOT_HTTP_SUCCESS",
                        StatusCode = (int)response.StatusCode,
                        Message = "Copilot API request succeeded."
                    }
                ]
            };
        }
        catch (Exception exception)
        {
            return new CopilotChatResult
            {
                Success = false,
                Diagnostics =
                [
                    new CopilotChatDiagnostic
                    {
                        Severity = "Warning",
                        Code = "COPILOT_REQUEST_EXCEPTION",
                        Message = $"Copilot API request failed. {exception.Message}"
                    }
                ]
            };
        }
    }

    private static string TryExtractConversationId(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return FindStringByPropertyName(document.RootElement, "id")
                ?? FindStringByPropertyName(document.RootElement, "conversationId")
                ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractResponseText(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return FindStringByPropertyName(document.RootElement, "content")
                ?? FindStringByPropertyName(document.RootElement, "text")
                ?? FindStringByPropertyName(document.RootElement, "message")
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
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object => element.EnumerateObject().Select(property => FindFirstString(property.Value)).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
            JsonValueKind.Array => element.EnumerateArray().Select(FindFirstString).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
            _ => null
        };
    }
}
