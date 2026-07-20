using System.IO.Compression;
using System.Text.Json;

namespace Djehuti.Core;

/// <summary>
/// Generalized project file handling for all Djehuti apps.
/// Standard format: .dapp (Djehuti App Project) ZIP files with metadata and artifacts.
/// Usable by: Architect, DjeLab, Teacher, Dashboard, and any future apps.
/// </summary>
public sealed class ProjectFileService
{
    /// <summary>
    /// Metadata for any Djehuti app project.
    /// </summary>
    public sealed class ProjectMetadata
    {
        public string AppName { get; set; } = ""; // "Architect", "DjeLab", "Teacher", etc.
        public string ProjectName { get; set; } = "";
        public string? Description { get; set; }
        public string Version { get; set; } = "1.0";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new(); // Custom metadata per app
    }

    /// <summary>
    /// Generic project archive structure.
    /// </summary>
    public sealed class ProjectArchive
    {
        public ProjectMetadata Metadata { get; set; } = new();
        public Dictionary<string, string> Files { get; set; } = new(); // filename -> JSON content
        public Dictionary<string, byte[]> Artifacts { get; set; } = new(); // path -> binary (diagrams, exports, etc.)
    }

    /// <summary>
    /// Create a .dapp ZIP project file.
    /// </summary>
    public static byte[] CreateProjectArchive(
        ProjectMetadata metadata,
        Dictionary<string, string> files,
        Dictionary<string, byte[]>? artifacts = null)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Write metadata.json
            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            var metadataEntry = zip.CreateEntry("metadata.json");
            using (var writer = new StreamWriter(metadataEntry.Open()))
            {
                writer.Write(metadataJson);
            }

            // Write files/ folder (JSON content)
            foreach (var (fileName, fileContent) in files)
            {
                var fileEntry = zip.CreateEntry($"files/{fileName}");
                using (var writer = new StreamWriter(fileEntry.Open()))
                {
                    writer.Write(fileContent);
                }
            }

            // Write artifacts/ folder (generated outputs: diagrams, exports, docs, etc.)
            if (artifacts != null)
            {
                foreach (var (artifactPath, artifactData) in artifacts)
                {
                    var artifactEntry = zip.CreateEntry($"artifacts/{artifactPath}");
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
    /// Extract a .dapp ZIP project file.
    /// </summary>
    public static ProjectArchive ExtractProjectArchive(byte[] fileContent)
    {
        var archive = new ProjectArchive();

        using (var ms = new MemoryStream(fileContent))
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            // Read metadata.json
            var metadataEntry = zip.GetEntry("metadata.json");
            if (metadataEntry != null)
            {
                using (var reader = new StreamReader(metadataEntry.Open()))
                {
                    var json = reader.ReadToEnd();
                    archive.Metadata = JsonSerializer.Deserialize<ProjectMetadata>(json) ?? new();
                }
            }

            // Read files/ folder (JSON content)
            var fileEntries = zip.Entries.Where(e => e.FullName.StartsWith("files/") && !e.Name.EndsWith("/"));
            foreach (var entry in fileEntries)
            {
                using (var reader = new StreamReader(entry.Open()))
                {
                    var content = reader.ReadToEnd();
                    var fileName = entry.FullName.Replace("files/", "");
                    archive.Files[fileName] = content;
                }
            }

            // Read artifacts/ folder (binary content)
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
    /// Save project to S3 (requires S3 credentials configured).
    /// </summary>
    public static async Task SaveToS3Async(
        string s3Client,
        string bucketName,
        string key,
        byte[] projectData,
        CancellationToken ct = default)
    {
        // This will be implemented when S3 client is available in the service
        // For now, provides the interface that apps will use
        throw new NotImplementedException("S3 save will be implemented via DI S3 client");
    }

    /// <summary>
    /// Load project from S3 (requires S3 credentials configured).
    /// </summary>
    public static async Task<byte[]> LoadFromS3Async(
        string s3Client,
        string bucketName,
        string key,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("S3 load will be implemented via DI S3 client");
    }
}
