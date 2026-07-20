using System.IO.Compression;
using System.Text.Json;
using Djehuti.Architect.Models;

namespace Djehuti.Architect.Services;

/// <summary>
/// Handles .daproj (project) and .datpt (template) ZIP file formats.
/// Structure: project.json + models/ folder + optional assets/.
/// </summary>
public sealed class ProjectFileService
{
    public sealed class ProjectMetadata
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Version { get; set; } = "1.0";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class ProjectArchive
    {
        public ProjectMetadata Metadata { get; set; } = new();
        public Dictionary<string, ArchitectureModel> Models { get; set; } = new();
    }

    /// <summary>
    /// Create a .daproj ZIP from a project with its models.
    /// </summary>
    public static byte[] CreateProjectArchive(ProjectMetadata metadata, Dictionary<string, ArchitectureModel> models)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Write project.json
            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            var metadataEntry = zip.CreateEntry("project.json");
            using (var writer = new StreamWriter(metadataEntry.Open()))
            {
                writer.Write(metadataJson);
            }

            // Write models/ folder
            foreach (var (modelName, model) in models)
            {
                var modelJson = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                var modelEntry = zip.CreateEntry($"models/{modelName}.json");
                using (var writer = new StreamWriter(modelEntry.Open()))
                {
                    writer.Write(modelJson);
                }
            }
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Extract a .daproj or .datpt ZIP file.
    /// </summary>
    public static ProjectArchive ExtractProjectArchive(byte[] fileContent)
    {
        var archive = new ProjectArchive();

        using (var ms = new MemoryStream(fileContent))
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            // Read project.json
            var metadataEntry = zip.GetEntry("project.json");
            if (metadataEntry != null)
            {
                using (var reader = new StreamReader(metadataEntry.Open()))
                {
                    var json = reader.ReadToEnd();
                    archive.Metadata = JsonSerializer.Deserialize<ProjectMetadata>(json) ?? new();
                }
            }

            // Read models/ folder
            var modelEntries = zip.Entries.Where(e => e.FullName.StartsWith("models/") && e.Name.EndsWith(".json"));
            foreach (var entry in modelEntries)
            {
                using (var reader = new StreamReader(entry.Open()))
                {
                    var json = reader.ReadToEnd();
                    var model = JsonSerializer.Deserialize<ArchitectureModel>(json);
                    if (model != null)
                    {
                        var name = Path.GetFileNameWithoutExtension(entry.Name);
                        archive.Models[name] = model;
                    }
                }
            }
        }

        return archive;
    }

    /// <summary>
    /// Create a template (.datpt) from a project archive.
    /// </summary>
    public static byte[] CreateTemplateArchive(ProjectMetadata metadata, Dictionary<string, ArchitectureModel> models)
    {
        // Same format as .daproj, just different file extension
        return CreateProjectArchive(metadata, models);
    }

    /// <summary>
    /// Get built-in templates (microservices, monolith, event-driven).
    /// </summary>
    public static Dictionary<string, ProjectArchive> GetBuiltInTemplates()
    {
        var templates = new Dictionary<string, ProjectArchive>();

        // Microservices template
        templates["microservices"] = new ProjectArchive
        {
            Metadata = new()
            {
                Name = "Microservices Architecture",
                Description = "Typical cloud-native microservices with API Gateway, services, and data stores",
                Version = "1.0",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            Models = new()
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
            }
        };

        return templates;
    }
}
