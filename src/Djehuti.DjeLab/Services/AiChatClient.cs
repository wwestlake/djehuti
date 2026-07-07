using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Djehuti.DjeLab.Services;

/// <summary>
/// Calls OpenAI's Responses API directly from the browser using the user's
/// own key (BYOK) -- mirrors Djehuti.Dashboard's Live Lab (src/features/live/
/// liveLab.ts) exactly: same endpoint, same request shape, same response
/// parsing, non-streaming. The key never touches Djehuti's own servers.
/// </summary>
public sealed class AiChatClient
{
    private const string Endpoint = "https://api.openai.com/v1/responses";

    // The system prompt asks for $ $ / $$ $$ (the only delimiters that
    // survive Markdown parsing intact -- see DjeLabSystemPrompt.cs), but
    // models don't reliably follow formatting instructions every time and
    // \( \) / \[ \] are, if anything, the more common LaTeX convention in
    // their training data. Rather than depend on compliance, normalize
    // whatever the model actually sent: this runs on the raw response
    // BEFORE Markdown ever sees it, while the backslashes are still intact,
    // so it's an unambiguous rewrite rather than a guess.
    private static readonly Regex DisplayMathPattern = new(@"\\\[(.+?)\\\]", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex InlineMathPattern = new(@"\\\((.+?)\\\)", RegexOptions.Singleline | RegexOptions.Compiled);

    // KaTeX doesn't support the standalone align/align* environment nested
    // inside $$ $$ (it's meant to establish its own display-math context,
    // which conflicts with being wrapped) -- it renders in an error state
    // instead of the equation. aligned is the form KaTeX actually supports
    // in that position, and has the same "no equation numbering" semantics
    // align* does, so this is a safe like-for-like substitution rather than
    // a lossy one.
    private static readonly Regex AlignEnvironmentPattern = new(@"\\(begin|end)\{align\*?\}", RegexOptions.Compiled);

    private readonly HttpClient _http;

    public AiChatClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> AskAsync(
        string apiKey,
        string model,
        IReadOnlyList<ChatTurn> history,
        string newMessage,
        CancellationToken ct = default)
    {
        var input = new List<object>(history.Count + 1);
        foreach (var turn in history)
            input.Add(new { role = turn.Role, content = turn.Content });
        input.Add(new { role = "user", content = newMessage });

        var body = new
        {
            model,
            instructions = DjeLabSystemPrompt.Text,
            input,
            store = false
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _http.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var message = TryExtractErrorMessage(responseText)
                ?? $"OpenAI request failed with HTTP {(int)response.StatusCode}.";
            throw new InvalidOperationException(message);
        }

        return NormalizeMathDelimiters(ExtractAssistantText(responseText));
    }

    private static string NormalizeMathDelimiters(string content)
    {
        content = DisplayMathPattern.Replace(content, m => $"$${m.Groups[1].Value}$$");
        content = InlineMathPattern.Replace(content, m => $"${m.Groups[1].Value}$");
        content = AlignEnvironmentPattern.Replace(content, m => $"\\{m.Groups[1].Value}{{aligned}}");
        return content;
    }

    private static string? TryExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var msg) &&
                msg.ValueKind == JsonValueKind.String)
            {
                return msg.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON (e.g. an upstream gateway error page) -- fall through to the generic message.
        }
        return null;
    }

    private static string ExtractAssistantText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String)
        {
            var text = outputText.GetString();
            if (!string.IsNullOrWhiteSpace(text)) return text!;
        }

        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        var s = text.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) parts.Add(s!);
                    }
                }
            }
            if (parts.Count > 0) return string.Join("\n", parts);
        }

        throw new InvalidOperationException("OpenAI's response did not contain any assistant text.");
    }
}
