using System.Net.Http.Json;

namespace Djehuti.Architect.Services;

public sealed class ProjectSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public sealed class ProjectListResponse
{
    public List<ProjectSummary> Projects { get; set; } = [];
    public bool IsPaidTier { get; set; }
}

public sealed class ProjectRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class FileEntry
{
    public string Id { get; set; } = "";
    public bool IsFolder { get; set; }
    public string Path { get; set; } = "";
    public string ParentPath { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class FileContentResponse
{
    public FileEntry Entry { get; set; } = new();
    public string Content { get; set; } = "";
}

/// <summary>
/// Thin wrapper over /api/architect/projects -- cloud (paid-tier) storage
/// only. Free-tier "local projects" never call this client at all; that flow
/// lives entirely in ProjectWorkspace.razor's in-memory state plus browser
/// download/upload.
/// </summary>
public sealed class ArchitectApiClient(HttpClient http)
{
    public async Task<ProjectListResponse?> ListProjectsAsync()
    {
        var response = await http.GetAsync("/djehuti/api/architect/projects");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProjectListResponse>();
    }

    public async Task<(ProjectRecord? record, string? error)> CreateProjectAsync(string name, string? description)
    {
        var response = await http.PostAsJsonAsync("/djehuti/api/architect/projects", new { name, description });
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<ProjectRecord>(), null);
        return (null, await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> DeleteProjectAsync(string id)
    {
        var response = await http.DeleteAsync($"/djehuti/api/architect/projects/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<FileEntry>?> ListTreeAsync(string projectId, string parentPath)
    {
        var response = await http.GetAsync($"/djehuti/api/architect/projects/{projectId}/tree?path={Uri.EscapeDataString(parentPath)}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<FileEntry>>();
    }

    public async Task<(FileEntry? entry, string? error)> CreateFolderAsync(string projectId, string parentPath, string name)
    {
        var response = await http.PostAsJsonAsync($"/djehuti/api/architect/projects/{projectId}/folder", new { parentPath, name });
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<FileEntry>(), null);
        return (null, await response.Content.ReadAsStringAsync());
    }

    public async Task<(FileEntry? entry, string? error)> SaveFileAsync(string projectId, string parentPath, string name, string contentType, string content)
    {
        var response = await http.PostAsJsonAsync($"/djehuti/api/architect/projects/{projectId}/files", new { parentPath, name, contentType, content });
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<FileEntry>(), null);
        return (null, await response.Content.ReadAsStringAsync());
    }

    public async Task<FileContentResponse?> GetFileContentAsync(string projectId, string fileId)
    {
        var response = await http.GetAsync($"/djehuti/api/architect/projects/{projectId}/files/{fileId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<FileContentResponse>();
    }

    public async Task<bool> DeleteEntryAsync(string projectId, string fileId)
    {
        var response = await http.DeleteAsync($"/djehuti/api/architect/projects/{projectId}/files/{fileId}");
        return response.IsSuccessStatusCode;
    }
}
