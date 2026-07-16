using System.Text.Json;
using System.Text.Json.Serialization;

namespace Djehuti.Teacher.Services;

public enum ApiProvider
{
    OpenAi,
    Anthropic,
    GoogleGemini,
    Mistral,
}

public sealed class ApiLlmClient
{
    private readonly HttpClient _http;
    private readonly AiConfigStore _config;

    public ApiLlmClient(HttpClient http, AiConfigStore config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string?> ChatAsync(IEnumerable<(string Role, string Content)> messages, ApiProvider provider, string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            Console.WriteLine("ChatAsync: Missing API key or model");
            return null;
        }

        Console.WriteLine($"ChatAsync: Using {provider} provider, model: {model}");

        return provider switch
        {
            ApiProvider.OpenAi => await ChatOpenAiAsync(messages, apiKey, model),
            ApiProvider.Anthropic => await ChatAnthropicAsync(messages, apiKey, model),
            ApiProvider.GoogleGemini => await ChatGeminiAsync(messages, apiKey, model),
            ApiProvider.Mistral => await ChatMistralAsync(messages, apiKey, model),
            _ => null,
        };
    }

    private async Task<string?> ChatOpenAiAsync(IEnumerable<(string Role, string Content)> messages, string apiKey, string model)
    {
        var openaiMessages = messages.Select(m => new
        {
            role = m.Role.ToLowerInvariant() == "system" ? "system" : m.Role.ToLowerInvariant() == "assistant" ? "assistant" : "user",
            content = m.Content
        }).ToList();

        var request = new
        {
            model,
            messages = openaiMessages,
            temperature = 0.7,
            max_tokens = 1000
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        content.Headers.Add("Authorization", $"Bearer {apiKey}");

        try
        {
            var response = await _http.PostAsync("https://api.openai.com/v1/chat/completions", content);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"OpenAI API error: {response.StatusCode}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var messageElem) &&
                    messageElem.TryGetProperty("content", out var textElem))
                {
                    return textElem.GetString();
                }
            }
            Console.WriteLine("OpenAI API: No valid response found");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OpenAI API exception: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> ChatAnthropicAsync(IEnumerable<(string Role, string Content)> messages, string apiKey, string model)
    {
        var messageList = messages.ToList();
        var systemPrompt = messageList.FirstOrDefault(m => m.Role.ToLowerInvariant() == "system").Content ?? "";
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
        content.Headers.Add("x-api-key", apiKey);
        content.Headers.Add("anthropic-version", "2023-06-01");

        try
        {
            var response = await _http.PostAsync("https://api.anthropic.com/v1/messages", content);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Anthropic API error: {response.StatusCode}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
            {
                var firstContent = contentArray[0];
                if (firstContent.TryGetProperty("text", out var textElem))
                {
                    return textElem.GetString();
                }
            }
            Console.WriteLine("Anthropic API: No valid response found");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Anthropic API exception: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> ChatGeminiAsync(IEnumerable<(string Role, string Content)> messages, string apiKey, string model)
    {
        // Placeholder: Google Gemini API integration structure
        // When implemented, would call Google's API similarly to OpenAI
        await Task.Delay(0);
        return null;
    }

    private async Task<string?> ChatMistralAsync(IEnumerable<(string Role, string Content)> messages, string apiKey, string model)
    {
        // Placeholder: Mistral API integration structure
        // When implemented, would call Mistral's API with OpenAI-compatible format
        await Task.Delay(0);
        return null;
    }
}
