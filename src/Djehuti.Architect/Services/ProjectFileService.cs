using System.Text.Json;
using Djehuti.Architect.Models;
using Djehuti.Core;

namespace Djehuti.Architect.Services;

/// <summary>
/// Architect-specific wrapper around Core.ProjectFileService.
/// Handles .daproj (project) and .datpt (template) files with ArchitectureModel serialization.
/// Uses Core's generalized ZIP handling with auto-generated manifest.
/// </summary>
public sealed class ProjectFileService
{
    private const string AppName = "Architect";
    private static readonly Dictionary<string, string> FolderDescriptions = new()
    {
        { "models", "Architecture model JSON files" },
        { "diagrams", "Generated Mermaid SVG diagrams" },
        { "exports", "Exported formats (Structurizr, PlantUML)" },
        { "docs", "Generated documentation" }
    };

    /// <summary>
    /// Create a .daproj from models and artifacts.
    /// Manifest auto-generates from folder structure.
    /// </summary>
    public static byte[] CreateProjectArchive(
        string projectName,
        string? description,
        Dictionary<string, ArchitectureModel> models,
        Dictionary<string, byte[]>? artifacts = null)
    {
        // Build contents: models/ folder (JSON) + artifacts
        var contents = new Dictionary<string, byte[]>();

        foreach (var (modelName, model) in models)
        {
            var modelJson = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            contents[$"models/{modelName}.json"] = System.Text.Encoding.UTF8.GetBytes(modelJson);
        }

        if (artifacts != null)
        {
            foreach (var (artifactPath, artifactData) in artifacts)
            {
                contents[$"artifacts/{artifactPath}"] = artifactData;
            }
        }

        // Use Core service with auto-generated manifest
        return Djehuti.Core.ProjectFileService.CreateProjectArchive(
            appName: AppName,
            projectName: projectName,
            contents: contents,
            description: description,
            folderDescriptions: FolderDescriptions
        );
    }

    /// <summary>
    /// Extract a .daproj or .datpt ZIP file.
    /// </summary>
    public static (Dictionary<string, ArchitectureModel> Models, Dictionary<string, byte[]> Artifacts) ExtractProjectArchive(byte[] zipData)
    {
        var archive = Djehuti.Core.ProjectFileService.ExtractProjectArchive(zipData);

        var models = new Dictionary<string, ArchitectureModel>();
        var artifacts = new Dictionary<string, byte[]>();

        foreach (var (path, data) in archive.Contents)
        {
            if (path.StartsWith("models/") && path.EndsWith(".json"))
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var model = JsonSerializer.Deserialize<ArchitectureModel>(json);
                if (model != null)
                {
                    var modelName = Path.GetFileNameWithoutExtension(path.Replace("models/", ""));
                    models[modelName] = model;
                }
            }
            else if (path.StartsWith("artifacts/"))
            {
                var artifactPath = path.Replace("artifacts/", "");
                artifacts[artifactPath] = data;
            }
        }

        return (models, artifacts);
    }

    /// <summary>
    /// Get built-in templates.
    /// </summary>
    public static Dictionary<string, (Dictionary<string, ArchitectureModel> Models, Dictionary<string, byte[]> Artifacts)> GetBuiltInTemplates()
    {
        var templates = new Dictionary<string, (Dictionary<string, ArchitectureModel>, Dictionary<string, byte[]>)>();

        var microservicesModels = new Dictionary<string, ArchitectureModel>
        {
            ["overview"] = new ArchitectureModel
            {
                Name = "Microservices Overview",
                Description = "Cloud-native microservices pattern",
                Components = new()
                {
                    new() { Id = "gateway", Name = "API Gateway", Kind = ComponentKind.Service, Technology = "Kong/Nginx" },
                    new() { Id = "auth-svc", Name = "Auth Service", Kind = ComponentKind.Service, Technology = "Node.js" },
                    new() { Id = "user-svc", Name = "User Service", Kind = ComponentKind.Service, Technology = "Node.js" },
                    new() { Id = "order-svc", Name = "Order Service", Kind = ComponentKind.Service, Technology = ".NET" },
                    new() { Id = "db-users", Name = "Users DB", Kind = ComponentKind.Database, Technology = "PostgreSQL" },
                    new() { Id = "db-orders", Name = "Orders DB", Kind = ComponentKind.Database, Technology = "PostgreSQL" },
                    new() { Id = "cache", Name = "Cache", Kind = ComponentKind.Service, Technology = "Redis" }
                },
                Connections = new()
                {
                    new() { FromComponentId = "gateway", ToComponentId = "auth-svc", Protocol = "HTTP/REST" },
                    new() { FromComponentId = "gateway", ToComponentId = "user-svc", Protocol = "HTTP/REST" },
                    new() { FromComponentId = "gateway", ToComponentId = "order-svc", Protocol = "HTTP/REST" },
                    new() { FromComponentId = "auth-svc", ToComponentId = "db-users", Protocol = "SQL" },
                    new() { FromComponentId = "user-svc", ToComponentId = "db-users", Protocol = "SQL" },
                    new() { FromComponentId = "order-svc", ToComponentId = "db-orders", Protocol = "SQL" },
                    new() { FromComponentId = "auth-svc", ToComponentId = "cache", Protocol = "Redis" }
                }
            }
        };

        templates["microservices"] = (microservicesModels, new());

        return templates;
    }
}
