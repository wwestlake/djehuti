using System.Net.Http.Json;
using System.Text.Json;

namespace Djehuti.DjeLab.Services;

public sealed class SemanticChunk
{
    public string SourceType { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public double Similarity { get; set; }
    public int Position { get; set; }
}

public sealed class SemanticContextResult
{
    public string Query { get; set; } = "";
    public string AppName { get; set; } = "";
    public List<SemanticChunk> Chunks { get; set; } = new();
    public int Count { get; set; }
}

public sealed class AppContextMetadata
{
    public string AppName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Checksum { get; set; } = "";
    public DateTime LastUpdated { get; set; }
}

public sealed class RagApiClient
{
    private readonly HttpClient _http;

    public RagApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Register an application with the LiteSemRAG system so it can use user-scoped semantic search.
    /// Call this once during app initialization (e.g., Seshat on first render in DjeLab).
    /// </summary>
    public async Task RegisterAppContextAsync(
        string appName,
        string version,
        string instructions,
        List<string> examples,
        string checksum)
    {
        var body = new
        {
            version,
            instructions,
            examples,
            checksum
        };

        var response = await _http.PostAsJsonAsync(
            $"/api/semantic/app-context/{appName}",
            body);

        // Non-fatal if registration fails; semantic search continues to work
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Retrieve user-scoped semantic context for a query. Returns chunks matched from the
    /// semantic graph with user filtering applied (so each user gets different results based
    /// on registered app contexts and user session).
    /// </summary>
    public async Task<SemanticContextResult> GetContextAsync(
        string query,
        string appName = "",
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"query={Uri.EscapeDataString(query)}"
        };

        if (!string.IsNullOrEmpty(appName))
            queryParams.Add($"app={Uri.EscapeDataString(appName)}");

        queryParams.Add($"limit={Math.Max(1, Math.Min(limit, 50))}");

        var url = $"/api/semantic/context?{string.Join("&", queryParams)}";
        var response = await _http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Semantic context retrieval failed: {response.StatusCode}");

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var root = json.RootElement;
        var chunks = new List<SemanticChunk>();

        if (root.TryGetProperty("chunks", out var chunksArray))
        {
            foreach (var chunk in chunksArray.EnumerateArray())
            {
                chunks.Add(new SemanticChunk
                {
                    SourceType = chunk.TryGetProperty("sourceType", out var st) ? st.GetString() ?? "" : "",
                    SourceKey = chunk.TryGetProperty("sourceKey", out var sk) ? sk.GetString() ?? "" : "",
                    Title = chunk.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    Content = chunk.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "",
                    Similarity = chunk.TryGetProperty("similarity", out var sim) ? sim.GetDouble() : 0.0,
                    Position = chunk.TryGetProperty("position", out var pos) ? pos.GetInt32() : 0
                });
            }
        }

        return new SemanticContextResult
        {
            Query = root.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "",
            AppName = root.TryGetProperty("appName", out var a) ? a.GetString() ?? "" : "",
            Chunks = chunks,
            Count = chunks.Count
        };
    }
}
