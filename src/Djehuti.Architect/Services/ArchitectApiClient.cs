using System.Net.Http.Json;
using System.Text.Json;
using Djehuti.Architect.Models;

namespace Djehuti.Architect.Services;

public sealed class ModelSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public sealed class ModelListResponse
{
    public List<ModelSummary> Models { get; set; } = [];
    public int Count { get; set; }
    public int Max { get; set; }
}

public sealed class ModelRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ModelJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ArchitectureModel? DeserializeModel() =>
        JsonSerializer.Deserialize<ArchitectureModel>(ModelJson, Services.ModelJson.Options);
}

/// <summary>
/// Thin wrapper over /api/architect/models -- one saved model per row,
/// stored as an opaque JSON blob server-side (see the migration-79 comment
/// in Database.fs). Errors surface as a plain message string rather than a
/// thrown exception, since "you've hit your saved-model limit" is an
/// expected, user-facing outcome, not a bug.
/// </summary>
public sealed class ArchitectApiClient(HttpClient http)
{
    public async Task<ModelListResponse?> ListAsync()
    {
        var response = await http.GetAsync("/djehuti/api/architect/models");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ModelListResponse>();
    }

    public async Task<ModelRecord?> GetAsync(string id)
    {
        var response = await http.GetAsync($"/djehuti/api/architect/models/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ModelRecord>();
    }

    public async Task<(ModelRecord? record, string? error)> CreateAsync(string name, ArchitectureModel model)
    {
        var json = JsonSerializer.Serialize(model, Services.ModelJson.Options);
        var response = await http.PostAsJsonAsync("/djehuti/api/architect/models", new { name, modelJson = json });
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<ModelRecord>(), null);
        return (null, await response.Content.ReadAsStringAsync());
    }

    public async Task<(ModelRecord? record, string? error)> UpdateAsync(string id, string name, ArchitectureModel model)
    {
        var json = JsonSerializer.Serialize(model, Services.ModelJson.Options);
        var response = await http.PutAsJsonAsync($"/djehuti/api/architect/models/{id}", new { name, modelJson = json });
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<ModelRecord>(), null);
        return (null, await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var response = await http.DeleteAsync($"/djehuti/api/architect/models/{id}");
        return response.IsSuccessStatusCode;
    }
}
