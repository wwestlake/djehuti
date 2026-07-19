using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Djehuti.DjeLab.Services;

/// <summary>A tool handler's result: `Output` is the plain-text payload sent
/// back to the model as the function_call_output (unaffected by `IsError` --
/// the model always sees the real text either way). `IsError` is read only
/// by the local tool-call loop, to decide whether this attempt counts
/// toward the same-tool-repeated-failure circuit breaker. A tool that ran
/// and reported a legitimate result -- including "no source" from
/// validate_spinoza -- is not the same thing as a tool that could not
/// complete its job; only the latter should trip the breaker.</summary>
public readonly record struct ToolResult(string Output, bool IsError)
{
    public static ToolResult Ok(string output) => new(output, false);
    public static ToolResult Error(string output) => new(output, true);
}

/// <summary>A tool handler receives the raw JSON arguments string the model
/// supplied and returns a `ToolResult`. Handlers are supplied by the caller
/// (ChatPane), not this class -- AiChatClient only knows the OpenAI protocol
/// for running the tool loop, not what any given tool actually does in this
/// app.</summary>
public delegate Task<ToolResult> ToolHandler(string argumentsJson, CancellationToken ct);

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

    // Two-tier stop condition. The real signal that something is stuck is
    // the SAME tool erroring several times in a row -- a model retrying
    // run_simulation against a syntax error it isn't fixing looks nothing
    // like a model that's several genuinely-progressing calls deep into a
    // file-based analysis (inspect, bundle, validate, run). Counting total
    // rounds conflated those two cases and forced picking one number that
    // was wrong for both; MaxConsecutiveSameToolErrors targets the actual
    // failure pattern instead. MaxToolRounds stays only as a last-resort
    // backstop against a loop that never errors but also never finishes
    // (e.g. ping-ponging between different tools without converging), so it
    // can be generous -- it's not expected to be the thing that fires.
    private const int MaxConsecutiveSameToolErrors = 4;
    private const int MaxToolRounds = 40;

    private static readonly object[] Tools =
    [
        new
        {
            type = "web_search_preview"
        },
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
            description = "Runs a Spinoza program in a new graph pane and plots the points it emits. Use this whenever the user wants something graphed, plotted, or visualized -- write a complete Spinoza program yourself (it must call emit(point) to produce chart data; see the language reference) and call this tool with it, rather than only describing the program in your reply. Prefer surface for actual height fields and scatter3d for point clouds or parametric curves. For surface, emit one full row of z values per step; use rows with several samples across the y-axis, do not send [x, y, z] tuples, do not build the surface as a nested point-by-point loop, and choose descriptive axis labels instead of generic x/y/z when the math has a clearer name. If the user provided a dataPath, the host will read the file directly and inject the selected data columns into the runtime as a `data` binding, so you can write code against the real dataset without copying the whole file into chat context. You will be told whether it ran successfully and how many points it produced.",
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
                    spinozaSource = new { type = "string", description = "A complete Spinoza program that calls emit(...) to produce chart data. For multi-file projects, you may leave this empty and provide projectPath instead." },
                    projectPath = new { type = "string", description = "Optional entry file or project folder path for a multi-file Spinoza project. The host will bundle import/include directives before running it." },
                    dataPath = new { type = "string", description = "Optional S3 file path for the runtime dataset. The host will read it directly and inject the selected columns as a `data` binding." },
                    dataColumns = new
                    {
                        type = "array",
                        description = "Optional column names or zero-based indices to extract from the dataset before execution. Leave empty to use all columns.",
                        items = new { type = "string" }
                    }
                },
                required = new[] { "chartType", "xLabel", "yLabel", "zLabel", "spinozaSource", "projectPath", "dataPath", "dataColumns" },
                additionalProperties = false
            },
            strict = true
        },
        new
        {
            type = "function",
            name = "validate_spinoza",
            description = "Validate a Spinoza program before running it. Use this as the preflight step for any generated code that looks non-trivial, especially if the code came from the AI and you want syntax checks and basic language-safety feedback before calling run_simulation. This returns parser errors and a few targeted warnings such as file I/O attempts, semicolons, or comment-like text that Spinoza does not support.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    spinozaSource = new { type = "string", description = "The Spinoza program to validate." }
                },
                required = new[] { "spinozaSource" },
                additionalProperties = false
            },
            strict = true
        },
        new
        {
            type = "function",
            name = "manage_file_data",
            description = "List, read, inspect tree structure, bundle multi-file Spinoza projects, or write the user's DjeLab S3-backed data files. Use this for CSV, JSON, ROOT-linked, text, and source files in the personal file area when you need to inspect data, save analysis output, or stage a file for later math work. Large files are previewed and sampled instead of being sent in full, so for CSV inspect the headers, top rows, and column profiles first, then feed those values into the transform or plot the user asked for. If a user says a .txt file is really CSV-format data, treat it as CSV if the content looks tabular. For Spinoza projects, use the bundle action to expand import/include directives into a single runnable source file before validate_spinoza or run_simulation. For physics datasets from the LHC, CMS, or ATLAS, assume the file contains real observed experimental data unless the user explicitly says simulated, and only mention sample counts or provenance that appear in the file preview or tool output. For ROOT files, look for a companion .manifest.json or .root.json file and use that hierarchy if it exists. For non-write actions, send content and contentType as empty strings. Reading returns file content or a compact preview plus a structural summary; bundling returns a pre-expanded single-file program with the imported files recorded separately; writing stores a text file at the requested path.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    action = new
                    {
                        type = "string",
                        @enum = new[] { "list", "read", "tree", "bundle", "write" }
                    },
                    path = new { type = "string", description = "Folder path for list, tree, or bundle; file path for read/write." },
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
        string systemPrompt,
        IReadOnlyDictionary<string, ToolHandler> toolHandlers,
        Action<string>? onStatus = null,
        string? additionalInstructions = null,
        CancellationToken ct = default)
    {
        var input = new List<object>(history.Count + 1);
        foreach (var turn in history)
            input.Add(new { role = turn.Role, content = turn.Content });
        input.Add(new { role = "user", content = newMessage });

        string? lastErroredTool = null;
        var consecutiveSameToolErrors = 0;

        for (var round = 0; round < MaxToolRounds; round++)
        {
            onStatus?.Invoke(round == 0 ? "Figuring out the request..." : "Checking the last result...");
            var responseText = await SendAsync(apiKey, model, input, systemPrompt, additionalInstructions, ct);
            using var doc = JsonDocument.Parse(responseText);

            var functionCalls = ExtractFunctionCalls(doc.RootElement);
            if (functionCalls.Count == 0)
            {
                onStatus?.Invoke("Wrapping up...");
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

                ToolResult result;
                if (toolHandlers.TryGetValue(call.Name, out var handler))
                {
                    try
                    {
                        onStatus?.Invoke(DescribeToolStatus(call.Name, call.ArgumentsJson));
                        result = await handler(call.ArgumentsJson, ct);
                    }
                    catch (Exception ex)
                    {
                        result = ToolResult.Error($"Tool error: {ex.Message}");
                    }
                }
                else
                {
                    result = ToolResult.Error($"Unknown tool '{call.Name}'.");
                }

                if (result.IsError && call.Name == lastErroredTool)
                {
                    consecutiveSameToolErrors++;
                }
                else if (result.IsError)
                {
                    lastErroredTool = call.Name;
                    consecutiveSameToolErrors = 1;
                }
                else
                {
                    lastErroredTool = null;
                    consecutiveSameToolErrors = 0;
                }

                input.Add(new
                {
                    type = "function_call_output",
                    call_id = call.CallId,
                    output = result.Output
                });

                if (consecutiveSameToolErrors >= MaxConsecutiveSameToolErrors)
                {
                    throw new InvalidOperationException(
                        $"The assistant called '{call.Name}' and got an error {consecutiveSameToolErrors} times in a row without success. Stopping to avoid a runaway loop.");
                }
            }
            // Loop again: the model sees the tool outputs and either replies
            // with text or asks for another tool call.
        }

        throw new InvalidOperationException("The assistant made too many tool calls without finishing its reply.");
    }

    private static string DescribeToolStatus(string toolName, string argumentsJson)
    {
        return toolName switch
        {
            "search_math_references" => "Checking the reference notes...",
            "validate_spinoza" => "Checking the code...",
            "run_simulation" => DescribeRunSimulationStatus(argumentsJson),
            "manage_file_data" => DescribeManageFileDataStatus(argumentsJson),
            _ => $"Running {toolName}..."
        };
    }

    private static string DescribeRunSimulationStatus(string argumentsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.TryGetProperty("chartType", out var chartType) && chartType.ValueKind == JsonValueKind.String)
            {
                return chartType.GetString() switch
                {
                    "surface" => "Drawing the surface...",
                    "scatter3d" => "Plotting the 3D points...",
                    "histogram" => "Building the histogram...",
                    "line" => "Drawing the line graph...",
                    "scatter" => "Plotting the scatter points...",
                    "bar" => "Building the bar chart...",
                    _ => "Running the graph..."
                };
            }
        }
        catch (JsonException)
        {
            // If the tool arguments are malformed, fall back to a generic status.
        }

        return "Running the graph...";
    }

    private static string DescribeManageFileDataStatus(string argumentsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.TryGetProperty("action", out var action) && action.ValueKind == JsonValueKind.String)
            {
                return action.GetString() switch
                {
                    "list" => "Looking through the folder...",
                    "read" => "Reading the file...",
                    "tree" => "Tracing the data structure...",
                    "bundle" => "Combining the project files...",
                    "write" => "Saving the file...",
                    _ => "Handling the file..."
                };
            }
        }
        catch (JsonException)
        {
            // Fall back to a generic message if the tool arguments aren't parseable.
        }

        return "Handling the file...";
    }

    private async Task<string> SendAsync(string apiKey, string model, List<object> input, string systemPrompt, string? additionalInstructions, CancellationToken ct)
    {
        const int maxRetries = 3;
        int retryCount = 0;

        while (true)
        {
            try
            {
                var body = new
                {
                    model,
                    instructions = string.IsNullOrWhiteSpace(additionalInstructions)
                        ? systemPrompt
                        : $"{systemPrompt}\n\n{additionalInstructions}",
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

                    // Detect rate limit error (429) and retry with backoff
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && retryCount < maxRetries)
                    {
                        retryCount++;
                        var backoffMs = (int)(Math.Pow(2, retryCount) * 1000); // exponential backoff: 2s, 4s, 8s
                        await Task.Delay(backoffMs, ct);
                        continue; // retry the request
                    }

                    throw new InvalidOperationException(message);
                }

                return responseText;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException) when (retryCount > 0)
            {
                // Already retried, don't retry again on non-rate-limit errors
                throw;
            }
        }
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
