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
        public Dictionary<string, byte[]> Artifacts { get; set; } = new(); // diagrams, docs, exports
    }

    /// <summary>
    /// Create a .daproj ZIP from a project with its models and artifacts.
    /// Structure: project.json, models/, artifacts/
    /// </summary>
    public static byte[] CreateProjectArchive(ProjectMetadata metadata, Dictionary<string, ArchitectureModel> models, Dictionary<string, byte[]>? artifacts = null)
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

            // Write artifacts/ folder (diagrams, exports, docs)
            if (artifacts != null)
            {
                foreach (var (artifactName, artifactData) in artifacts)
                {
                    var artifactEntry = zip.CreateEntry($"artifacts/{artifactName}");
                    using (var stream = artifactEntry.Open())
                    {
                        stream.Write(artifactData, 0, artifactData.Length);
                    }
                }
            }
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Extract a .daproj or .datpt ZIP file (models + artifacts).
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

            // Read artifacts/ folder (diagrams, exports, docs)
            var artifactEntries = zip.Entries.Where(e => e.FullName.StartsWith("artifacts/") && !e.Name.EndsWith("/"));
            foreach (var entry in artifactEntries)
            {
                using (var stream = entry.Open())
                {
                    var data = new byte[entry.Length];
                    stream.Read(data, 0, (int)entry.Length);
                    var artifactPath = entry.FullName.Replace("artifacts/", "");
                    archive.Artifacts[artifactPath] = data;
                }
            }
        }

        return archive;
    }

    /// <summary>
    /// Create a template (.datpt) from a project archive.
    /// Same format as .daproj, just different file extension when saved.
    /// </summary>
    public static byte[] CreateTemplateArchive(ProjectMetadata metadata, Dictionary<string, ArchitectureModel> models, Dictionary<string, byte[]>? artifacts = null)
    {
        return CreateProjectArchive(metadata, models, artifacts);
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
