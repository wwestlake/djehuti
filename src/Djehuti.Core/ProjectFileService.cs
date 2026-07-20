using System.IO.Compression;
using System.Text.Json;

namespace Djehuti.Core;

/// <summary>
/// Generalized project file handling for all Djehuti apps.
/// Standard format: .dapp (Djehuti App Project) ZIP files with manifest and arbitrary structure.
/// Usable by: Architect, DjeLab, Teacher, Dashboard, and any future apps.
/// Each app defines its own folder structure; manifest describes the contents.
/// </summary>
public sealed class ProjectFileService
{
    /// <summary>
    /// Manifest describing project contents and structure.
    /// Tells readers how to interpret the folders inside the ZIP.
    /// </summary>
    public sealed class ProjectManifest
    {
        public string AppName { get; set; } = ""; // "Architect", "DjeLab", "Teacher", etc.
        public string ProjectName { get; set; } = "";
        public string? Description { get; set; }
        public string Version { get; set; } = "1.0";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Describes folder structure and purpose.
        /// Example: { "models": "Architecture model JSON files", "diagrams": "SVG outputs", "exports": "Structurizr/PlantUML exports" }
        /// </summary>
        public Dictionary<string, string> FolderStructure { get; set; } = new();

        /// <summary>
        /// Custom metadata per app (tags, settings, etc.)
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Generic project archive - manifest + arbitrary folder structure.
    /// Apps define their own internal organization.
    /// </summary>
    public sealed class ProjectArchive
    {
        public ProjectManifest Manifest { get; set; } = new();
        public Dictionary<string, byte[]> Contents { get; set; } = new(); // path -> content (any folder structure)
    }

    /// <summary>
    /// Create a .dapp ZIP project file with arbitrary folder structure.
    /// Manifest auto-generates from folder structure; always in sync with contents.
    /// </summary>
    public static byte[] CreateProjectArchive(
        string appName,
        string projectName,
        Dictionary<string, byte[]> contents,
        string? description = null,
        Dictionary<string, object>? metadata = null,
        Dictionary<string, string>? folderDescriptions = null)
    {
        // Auto-generate folder structure from contents
        var folders = contents.Keys
            .Select(path => path.Split('/')[0])
            .Distinct()
            .ToHashSet();

        var manifest = new ProjectManifest
        {
            AppName = appName,
            ProjectName = projectName,
            Description = description,
            Version = "1.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FolderStructure = folders.ToDictionary(
                f => f,
                f => folderDescriptions?.ContainsKey(f) == true ? folderDescriptions[f] : $"{f}/ folder"
            ),
            Metadata = metadata ?? new()
        };

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Write auto-generated manifest.json
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            var manifestEntry = zip.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write(manifestJson);
            }

            // Write all contents with arbitrary folder structure
            foreach (var (path, data) in contents)
            {
                var entry = zip.CreateEntry(path);
                using (var stream = entry.Open())
                {
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Extract a .dapp ZIP project file with arbitrary folder structure.
    /// Reads manifest to understand contents; loads all files as-is.
    /// </summary>
    public static ProjectArchive ExtractProjectArchive(byte[] fileContent)
    {
        var archive = new ProjectArchive();

        using (var ms = new MemoryStream(fileContent))
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            // Read manifest.json (describes the project structure)
            var manifestEntry = zip.GetEntry("manifest.json");
            if (manifestEntry != null)
            {
                using (var reader = new StreamReader(manifestEntry.Open()))
                {
                    var json = reader.ReadToEnd();
                    archive.Manifest = JsonSerializer.Deserialize<ProjectManifest>(json) ?? new();
                }
            }

            // Read all contents (arbitrary folder structure defined by app)
            var contentEntries = zip.Entries.Where(e => !e.Name.EndsWith("/") && e.FullName != "manifest.json");
            foreach (var entry in contentEntries)
            {
                using (var stream = entry.Open())
                {
                    var data = new byte[entry.Length];
                    stream.Read(data, 0, (int)entry.Length);
                    archive.Contents[entry.FullName] = data;
                }
            }
        }

        return archive;
    }

    /// <summary>
    /// Save project to local filesystem.
    /// </summary>
    public static async Task SaveToLocalAsync(
        string directoryPath,
        string fileName,
        byte[] projectData,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Combine(directoryPath, fileName);
        await File.WriteAllBytesAsync(filePath, projectData, ct);
    }

    /// <summary>
    /// Load project from local filesystem.
    /// </summary>
    public static async Task<byte[]> LoadFromLocalAsync(
        string filePath,
        CancellationToken ct = default)
    {
        return await File.ReadAllBytesAsync(filePath, ct);
    }

    /// <summary>
    /// Save project to S3 (requires S3 credentials configured in environment).
    /// Placeholder for integration with S3 client via DI.
    /// </summary>
    public static async Task SaveToS3Async(
        string bucketName,
        string key,
        byte[] projectData,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("S3 save will be implemented via DI S3 client");
    }

    /// <summary>
    /// Load project from S3 (requires S3 credentials configured in environment).
    /// Placeholder for integration with S3 client via DI.
    /// </summary>
    public static async Task<byte[]> LoadFromS3Async(
        string bucketName,
        string key,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("S3 load will be implemented via DI S3 client");
    }
}
