using System.IO.Compression;
using System.Text.Json;

namespace Djehuti.Core;

/// <summary>
/// Generalized ZIP-based project file system for all Lagdaemon applications.
/// Handles creation and extraction of .dapp (project) and .datpt (template) files.
/// Each app defines its own folder structure via FolderDescriptions.
/// Auto-generates manifest.json from file paths in the ZIP.
/// </summary>
public static class ProjectFileService
{
    /// <summary>
    /// Result of extracting a project archive.
    /// </summary>
    public record ProjectArchive(
        string AppName,
        string ProjectName,
        string? Description,
        Dictionary<string, string> FolderDescriptions,
        Dictionary<string, byte[]> Contents,
        string Manifest
    );

    /// <summary>
    /// Create a project archive (.dapp) from file contents and folder descriptions.
    /// Automatically generates manifest.json describing the folder structure.
    /// </summary>
    public static byte[] CreateProjectArchive(
        string appName,
        string projectName,
        Dictionary<string, byte[]> contents,
        string? description = null,
        Dictionary<string, string>? folderDescriptions = null)
    {
        using var memoryStream = new MemoryStream();
        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Write all content files
            foreach (var (path, data) in contents)
            {
                var entry = zipArchive.CreateEntry(path);
                using var entryStream = entry.Open();
                entryStream.Write(data, 0, data.Length);
            }

            // Generate and write manifest.json
            var manifest = new
            {
                appName,
                projectName,
                description,
                folderDescriptions = folderDescriptions ?? new Dictionary<string, string>(),
                files = contents.Keys.ToList(),
                generatedAt = DateTime.UtcNow.ToString("O")
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            var manifestEntry = zipArchive.CreateEntry("manifest.json");
            using var manifestStream = manifestEntry.Open();
            var manifestBytes = System.Text.Encoding.UTF8.GetBytes(manifestJson);
            manifestStream.Write(manifestBytes, 0, manifestBytes.Length);
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Extract a project archive (.dapp) and return its contents and metadata.
    /// </summary>
    public static ProjectArchive ExtractProjectArchive(byte[] zipData)
    {
        using var memoryStream = new MemoryStream(zipData);
        using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        var contents = new Dictionary<string, byte[]>();
        string appName = "";
        string projectName = "";
        string? description = null;
        var folderDescriptions = new Dictionary<string, string>();
        var manifestJson = "";

        // Extract all files except manifest
        foreach (var entry in zipArchive.Entries)
        {
            if (entry.Name == "manifest.json")
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                manifestJson = reader.ReadToEnd();

                // Parse manifest to extract metadata
                var manifest = JsonSerializer.Deserialize<JsonElement>(manifestJson);
                if (manifest.ValueKind == JsonValueKind.Object)
                {
                    if (manifest.TryGetProperty("appName", out var appNameElement))
                        appName = appNameElement.GetString() ?? "";
                    if (manifest.TryGetProperty("projectName", out var projectNameElement))
                        projectName = projectNameElement.GetString() ?? "";
                    if (manifest.TryGetProperty("description", out var descElement))
                        description = descElement.GetString();
                    if (manifest.TryGetProperty("folderDescriptions", out var foldersElement) && foldersElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in foldersElement.EnumerateObject())
                        {
                            folderDescriptions[prop.Name] = prop.Value.GetString() ?? "";
                        }
                    }
                }
            }
            else if (entry.FullName != entry.Name || !entry.FullName.Contains("/")) // Only extract files, not directory entries
            {
                using var stream = entry.Open();
                using var memStream = new MemoryStream();
                stream.CopyTo(memStream);
                contents[entry.FullName] = memStream.ToArray();
            }
        }

        return new ProjectArchive(appName, projectName, description, folderDescriptions, contents, manifestJson);
    }
}

/// <summary>
/// Djehuti Cyberscope-specific wrapper around ProjectFileService.
/// Handles measurement sessions, analysis results, and LLM trajectory data.
/// Uses generalized project file system for save/load/export.
/// </summary>
public sealed class MeasurementProjectService
{
    private const string AppName = "Djehuti Cyberscope AI+";
    private static readonly Dictionary<string, string> FolderDescriptions = new()
    {
        { "sessions", "Measurement session data (prompts, responses, trajectories)" },
        { "analysis", "Analysis results and computed observables" },
        { "calibration", "Calibration records and baseline measurements" },
        { "exports", "Exported reports (JSON, CSV, analysis summaries)" },
        { "metadata", "Session metadata, configuration, constants" }
    };

    /// <summary>
    /// Create a measurement project file (.dapp) from session data.
    /// </summary>
    public static byte[] CreateProjectArchive(
        string projectName,
        string? description,
        Dictionary<string, string> sessions,
        Dictionary<string, string> analysisResults,
        Dictionary<string, byte[]>? exports = null)
    {
        var contents = new Dictionary<string, byte[]>();

        // Sessions folder
        foreach (var (sessionName, sessionJson) in sessions)
        {
            contents[$"sessions/{sessionName}.json"] = System.Text.Encoding.UTF8.GetBytes(sessionJson);
        }

        // Analysis folder
        foreach (var (analysisName, analysisJson) in analysisResults)
        {
            contents[$"analysis/{analysisName}.json"] = System.Text.Encoding.UTF8.GetBytes(analysisJson);
        }

        // Exports folder (reports, summaries, etc)
        if (exports != null)
        {
            foreach (var (exportPath, exportData) in exports)
            {
                contents[$"exports/{exportPath}"] = exportData;
            }
        }

        return ProjectFileService.CreateProjectArchive(
            appName: AppName,
            projectName: projectName,
            contents: contents,
            description: description,
            folderDescriptions: FolderDescriptions
        );
    }

    /// <summary>
    /// Extract measurement project from .dapp file.
    /// </summary>
    public static (Dictionary<string, string> Sessions, Dictionary<string, string> Analysis, Dictionary<string, byte[]> Exports) ExtractProjectArchive(byte[] zipData)
    {
        var archive = ProjectFileService.ExtractProjectArchive(zipData);

        var sessions = new Dictionary<string, string>();
        var analysis = new Dictionary<string, string>();
        var exports = new Dictionary<string, byte[]>();

        foreach (var (path, data) in archive.Contents)
        {
            if (path.StartsWith("sessions/") && path.EndsWith(".json"))
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var sessionName = Path.GetFileNameWithoutExtension(path.Replace("sessions/", ""));
                sessions[sessionName] = json;
            }
            else if (path.StartsWith("analysis/") && path.EndsWith(".json"))
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var analysisName = Path.GetFileNameWithoutExtension(path.Replace("analysis/", ""));
                analysis[analysisName] = json;
            }
            else if (path.StartsWith("exports/"))
            {
                var exportPath = path.Replace("exports/", "");
                exports[exportPath] = data;
            }
        }

        return (sessions, analysis, exports);
    }
}
