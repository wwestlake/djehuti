using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Djehuti.DjeLab.Services;

/// <summary>A tool handler receives the raw JSON arguments string the model
/// supplied and returns a plain-text result that becomes the tool's
/// function_call_output -- the model sees this and can react to it (e.g.
/// mention how many points a simulation actually produced) in its next
/// turn. Handlers are supplied by the caller (ChatPane), not this class --
/// AiChatClient only knows the OpenAI protocol for running the tool loop,
/// not what any given tool actually does in this app.</summary>
public delegate Task<string> ToolHandler(string argumentsJson);

/// <summary>
/// Calls OpenAI's Responses API directly from the browser using the user's
/// own key (BYOK) -- mirrors Djehuti.Dashboard's Live Lab (src/features/live/
/// liveLab.ts) exactly: same endpoint, same request shape, same response
/// parsing, non-streaming. The key never touches Djehuti's own servers.
///
/// Implements the tool-calling layer from the original architecture spec
/// (research/DjeLab_Architecture_and_Specification_V2.md section 3.1):
/// search_math_references and run_simulation. The Responses API's tool
/// protocol is a loop, not a single round trip -- the model's function_call
/// items must be echoed back into `input` alongside a matching
/// function_call_output before it will produce more output, and it may ask
/// for another tool call after seeing a result, so this keeps requesting
/// until the model responds with plain text instead of a tool call.
/// </summary>
public sealed class AiChatClient
{
    private const string Endpoint = "https://api.openai.com/v1/responses";

    // A tool loop that never terminates would hang the chat forever if a
    // model got stuck calling tools back-to-back; this is a real ceiling,
    // not an expected-case limit. Started at 6 -- found live to be too low
    // for a model retrying run_simulation against a genuinely confusing
    // parser error (see Parser.fs's attempt-wrapping fix); raised once that
    // was fixed, since a model that can actually see its own mistake should
    // converge within a handful of retries, but the margin is worth keeping
    // generous rather than fighting this exact failure mode again.
    private const int MaxToolRounds = 12;

    private static readonly object[] Tools =
    [
        new
        {
            type = "function",
            name = "search_math_references",
            description = "Search DjeLab's indexed reference material (including the Spinoza language reference) for content relevant to the user's question. Use this to ground Spinoza code generation in the actual language spec instead of guessing from memory.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "What to search for, e.g. 'vector indexing' or 'recursion step budget'." }
                },
                required = new[] { "query" },
                additionalProperties = false
            },
            strict = true
        },
        new
        {
            type = "function",
            name = "run_simulation",
            description = "Runs a Spinoza program in a new graph pane and plots the points it emits. Use this whenever the user wants something graphed, plotted, or visualized -- write a complete Spinoza program yourself (it must call emit(point) to produce chart data; see the language reference) and call this tool with it, rather than only describing the program in your reply. Prefer surface for actual height fields and scatter3d for point clouds or parametric curves. For surface, emit one full row of z values per step; use rows with several samples across the y-axis, do not send [x, y, z] tuples, do not build the surface as a nested point-by-point loop, and choose descriptive axis labels instead of generic x/y/z when the math has a clearer name. You will be told whether it ran successfully and how many points it produced.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    chartType = new
                    {
                        type = "string",
                        description = "line/scatter/bar/histogram plot 2-vectors [x, y]; scatter3d plots [x, y, z] tuples; surface plots one full row of z values per emit.",
                        @enum = new[] { "line", "scatter", "bar", "histogram", "scatter3d", "surface" }
                    },
                    xLabel = new { type = "string" },
                    yLabel = new { type = "string" },
                    zLabel = new { type = "string", description = "Only meaningful for scatter3d; for surface, pass an empty string unless you want a label for the height axis." },
                    spinozaSource = new { type = "string", description = "A complete Spinoza program that calls emit(...) to produce chart data." }
                },
                required = new[] { "chartType", "xLabel", "yLabel", "zLabel", "spinozaSource" },
                additionalProperties = false
            },
            strict = true
        },
        new
        {
            type = "function",
            name = "manage_file_data",
            description = "List, read, inspect tree structure, or write the user's DjeLab S3-backed data files. Use this for CSV, JSON, ROOT-linked, and text files in the personal file area when you need to inspect data, save analysis output, or stage a data file for later math work. Large files are previewed and sampled instead of being sent in full, so for CSV inspect the headers and top rows first, then feed those values into the transform or plot the user asked for. If a user says a .txt file is really CSV-format data, treat it as CSV if the content looks tabular. For ROOT files, look for a companion .manifest.json or .root.json file and use that hierarchy if it exists. For non-write actions, send content and contentType as empty strings. Reading returns file content or a compact preview plus a structural summary; writing stores a text file at the requested path.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    action = new
                    {
                        type = "string",
                        @enum = new[] { "list", "read", "tree", "write" }
                    },
                    path = new { type = "string", description = "Folder path for list, file path for read/write." },
                    content = new { type = "string", description = "Text to write when action is write." },
                    contentType = new { type = "string", description = "Optional MIME type for write, e.g. application/json or text/csv." }
                },
                required = new[] { "action", "path", "content", "contentType" },
                additionalProperties = false
            },
            strict = true
        }
    ];

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
        IReadOnlyDictionary<string, ToolHandler> toolHandlers,
        CancellationToken ct = default)
    {
        var input = new List<object>(history.Count + 1);
        foreach (var turn in history)
            input.Add(new { role = turn.Role, content = turn.Content });
        input.Add(new { role = "user", content = newMessage });

        for (var round = 0; round < MaxToolRounds; round++)
        {
            var responseText = await SendAsync(apiKey, model, input, ct);
            using var doc = JsonDocument.Parse(responseText);

            var functionCalls = ExtractFunctionCalls(doc.RootElement);
            if (functionCalls.Count == 0)
            {
                return MathDelimiterNormalizer.Normalize(ExtractAssistantText(doc.RootElement));
            }

            foreach (var call in functionCalls)
            {
                // The API requires the original function_call item echoed
                // back verbatim (id and call_id both) before it will accept
                // the matching output -- this isn't a paraphrase, it's how
                // the call gets tied together across the request boundary.
                input.Add(new
                {
                    type = "function_call",
                    id = call.Id,
                    call_id = call.CallId,
                    name = call.Name,
                    arguments = call.ArgumentsJson
                });

                string output;
                if (toolHandlers.TryGetValue(call.Name, out var handler))
                {
                    try
                    {
                        output = await handler(call.ArgumentsJson);
                    }
                    catch (Exception ex)
                    {
                        output = $"Tool error: {ex.Message}";
                    }
                }
                else
                {
                    output = $"Unknown tool '{call.Name}'.";
                }

                input.Add(new
                {
                    type = "function_call_output",
                    call_id = call.CallId,
                    output
                });
            }
            // Loop again: the model sees the tool outputs and either replies
            // with text or asks for another tool call.
        }

        throw new InvalidOperationException("The assistant made too many tool calls in a row without finishing its reply.");
    }

    private async Task<string> SendAsync(string apiKey, string model, List<object> input, CancellationToken ct)
    {
        var body = new
        {
            model,
            instructions = DjeLabSystemPrompt.Text,
            input,
            tools = Tools,
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

        return responseText;
    }

    private sealed record FunctionCall(string Id, string CallId, string Name, string ArgumentsJson);

    private static List<FunctionCall> ExtractFunctionCalls(JsonElement root)
    {
        var calls = new List<FunctionCall>();
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return calls;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var type) || type.GetString() != "function_call")
                continue;

            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            var callId = item.GetProperty("call_id").GetString() ?? "";
            var name = item.GetProperty("name").GetString() ?? "";
            var arguments = item.TryGetProperty("arguments", out var argsEl) ? argsEl.GetString() ?? "{}" : "{}";
            calls.Add(new FunctionCall(id, callId, name, arguments));
        }
        return calls;
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

    private static string ExtractAssistantText(JsonElement root)
    {
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

        // A turn that was pure tool calls with no accompanying text is valid
        // (the model may reply with text only after seeing the tool result,
        // which happens on a later loop iteration) -- but if this is reached
        // it means the FINAL round produced neither text nor a function
        // call, which genuinely has nothing to show the user.
        throw new InvalidOperationException("OpenAI's response did not contain any assistant text.");
    }
}
