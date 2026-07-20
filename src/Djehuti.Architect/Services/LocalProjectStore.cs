using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Djehuti.Architect.Models;

namespace Djehuti.Architect.Services;

public sealed class LocalFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class LocalProjectBundle
{
    public string Name { get; set; } = "Local project";
    public List<LocalFile> Files { get; set; } = [];
}

/// <summary>
/// Free-tier project storage: everything lives in browser memory for the
/// current tab only, nothing ever reaches the server. "Persistence" is the
/// user downloading a bundle (one JSON file holding the project name plus
/// every file's content) and reopening it later -- the same shape a cloud
/// project's tree would have, just serialized flat instead of stored as
/// individual S3 objects, since there's no server round trip to justify the
/// extra structure here.
/// </summary>
public sealed class LocalProjectStore(IJSRuntime js, NavigationManager nav)
{
    public LocalProjectBundle Project { get; private set; } = new();

    public void NewProject(string name)
    {
        Project = new LocalProjectBundle { Name = string.IsNullOrWhiteSpace(name) ? "Local project" : name };
    }

    public LocalFile CreateFile(string name)
    {
        var file = new LocalFile { Name = name };
        Project.Files.Add(file);
        return file;
    }

    public void DeleteFile(string id) => Project.Files.RemoveAll(f => f.Id == id);

    public void SaveFile(string id, string content)
    {
        var file = Project.Files.FirstOrDefault(f => f.Id == id);
        if (file is not null)
        {
            file.Content = content;
            file.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task DownloadAsJsonAsync()
    {
        var json = JsonSerializer.Serialize(Project, new JsonSerializerOptions { WriteIndented = true });
        var filename = $"{Project.Name.Replace(' ', '-')}.json";
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", new Uri(new Uri(nav.BaseUri), "js/file-interop.js").ToString());
        await module.InvokeVoidAsync("downloadTextFile", filename, json, "application/json");
    }

    public async Task DownloadAsDaprojAsync()
    {
        // Convert LocalFile objects to ArchitectureModels
        var models = new Dictionary<string, ArchitectureModel>();
        foreach (var file in Project.Files)
        {
            try
            {
                var model = JsonSerializer.Deserialize<ArchitectureModel>(file.Content);
                if (model != null)
                {
                    models[file.Name] = model;
                }
            }
            catch
            {
                // Skip files that aren't valid architecture models
            }
        }

        var metadata = new ProjectFileService.ProjectMetadata
        {
            Name = Project.Name,
            Version = "1.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var zipData = ProjectFileService.CreateProjectArchive(metadata, models);
        var filename = $"{Project.Name.Replace(' ', '-')}.daproj";

        await using var module = await js.InvokeAsync<IJSObjectReference>("import", new Uri(new Uri(nav.BaseUri), "js/file-interop.js").ToString());
        await module.InvokeVoidAsync("downloadBinaryFile", filename, zipData);
    }

    public async Task DownloadAsync() => await DownloadAsDaprojAsync();

    public void LoadFromJson(string json)
    {
        var loaded = JsonSerializer.Deserialize<LocalProjectBundle>(json);
        if (loaded is not null) Project = loaded;
    }

    public void LoadFromDaproj(byte[] zipData)
    {
        var archive = ProjectFileService.ExtractProjectArchive(zipData);
        Project = new LocalProjectBundle
        {
            Name = archive.Metadata.Name,
            Files = archive.Models.Select(kvp =>
                new LocalFile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = kvp.Key,
                    Content = JsonSerializer.Serialize(kvp.Value, new JsonSerializerOptions { WriteIndented = true }),
                    UpdatedAt = DateTime.UtcNow
                }
            ).ToList()
        };
    }
}
