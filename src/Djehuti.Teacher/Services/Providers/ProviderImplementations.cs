using System.Text.Json;
using System.Text.Json.Serialization;

namespace Djehuti.Teacher.Services.Providers;

public class OpenAiProvider : IProviderDefinition
{
    public string Name => "OpenAI";
    public string Endpoint => "https://api.openai.com/v1/chat/completions";
    public List<string> AvailableModels => new() { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo" };
    public string DefaultModel => "gpt-4o-mini";

    public void AddAuthHeaders(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<string?> ParseResponseAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
            {
                return content.GetString();
            }
        }
        return null;
    }

    public HttpRequestMessage BuildRequest(
        IEnumerable<(string Role, string Content)> messages,
        string model,
        string systemPrompt)
    {
        var messageList = messages.ToList();
        var openaiMessages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            openaiMessages.Add(new { role = "system", content = systemPrompt });
        }

        foreach (var msg in messageList.Where(m => m.Role.ToLowerInvariant() != "system"))
        {
            openaiMessages.Add(new
            {
                role = msg.Role.ToLowerInvariant() == "assistant" ? "assistant" : "user",
                content = msg.Content
            });
        }

        var request = new
        {
            model,
            messages = openaiMessages,
            temperature = 0.7,
            max_tokens = 1000
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
        return httpRequest;
    }
}

public class AnthropicProvider : IProviderDefinition
{
    public string Name => "Anthropic";
    public string Endpoint => "https://api.anthropic.com/v1/messages";
    public List<string> AvailableModels => new() { "claude-3-5-sonnet-20241022", "claude-3-5-haiku-20241022", "claude-3-opus-20240229", "claude-3-sonnet-20240229" };
    public string DefaultModel => "claude-3-5-sonnet-20241022";

    public void AddAuthHeaders(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
    }

    public async Task<string?> ParseResponseAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
        {
            var firstContent = contentArray[0];
            if (firstContent.TryGetProperty("text", out var text))
            {
                return text.GetString();
            }
        }
        return null;
    }

    public HttpRequestMessage BuildRequest(
        IEnumerable<(string Role, string Content)> messages,
        string model,
        string systemPrompt)
    {
        var messageList = messages.ToList();
        var nonSystemMessages = messageList.Where(m => m.Role.ToLowerInvariant() != "system").ToList();

        var anthropicMessages = nonSystemMessages.Select(m => new
        {
            role = m.Role.ToLowerInvariant() == "assistant" ? "assistant" : "user",
            content = m.Content
        }).ToList();

        var request = new
        {
            model,
            max_tokens = 1000,
            system = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
            messages = anthropicMessages
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
        return httpRequest;
    }
}

public class GoogleGeminiProvider : IProviderDefinition
{
    public string Name => "Google Gemini";
    public string Endpoint => "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
    public List<string> AvailableModels => new() { "gemini-1.5-pro", "gemini-1.5-flash", "gemini-2.0-flash" };
    public string DefaultModel => "gemini-1.5-flash";

    public void AddAuthHeaders(HttpRequestMessage request, string apiKey)
    {
        // Google uses API key in query string, not headers
        // This is handled in BuildRequest
    }

    public async Task<string?> ParseResponseAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var contentObj) &&
                contentObj.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
            {
                var firstPart = parts[0];
                if (firstPart.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }
        }
        return null;
    }

    public HttpRequestMessage BuildRequest(
        IEnumerable<(string Role, string Content)> messages,
        string model,
        string systemPrompt)
    {
        var messageList = messages.ToList();
        var geminiMessages = new List<object>();

        foreach (var msg in messageList.Where(m => m.Role.ToLowerInvariant() != "system"))
        {
            geminiMessages.Add(new
            {
                role = msg.Role.ToLowerInvariant() == "assistant" ? "model" : "user",
                parts = new[] { new { text = msg.Content } }
            });
        }

        var request = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt ?? "" } } },
            contents = geminiMessages,
            generation_config = new
            {
                temperature = 0.7,
                max_output_tokens = 1000
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var endpoint = Endpoint.Replace("{model}", model);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        return httpRequest;
    }
}

public class MistralProvider : IProviderDefinition
{
    public string Name => "Mistral";
    public string Endpoint => "https://api.mistral.ai/v1/chat/completions";
    public List<string> AvailableModels => new() { "mistral-large-latest", "mistral-small-latest", "mistral-tiny-2312" };
    public string DefaultModel => "mistral-small-latest";

    public void AddAuthHeaders(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<string?> ParseResponseAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
            {
                return content.GetString();
            }
        }
        return null;
    }

    public HttpRequestMessage BuildRequest(
        IEnumerable<(string Role, string Content)> messages,
        string model,
        string systemPrompt)
    {
        var messageList = messages.ToList();
        var mistralMessages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            mistralMessages.Add(new { role = "system", content = systemPrompt });
        }

        foreach (var msg in messageList.Where(m => m.Role.ToLowerInvariant() != "system"))
        {
            mistralMessages.Add(new
            {
                role = msg.Role.ToLowerInvariant() == "assistant" ? "assistant" : "user",
                content = msg.Content
            });
        }

        var request = new
        {
            model,
            messages = mistralMessages,
            temperature = 0.7,
            max_tokens = 1000
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
        return httpRequest;
    }
}
