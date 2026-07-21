using System.Text.Json;
using Djehuti.Core;

namespace Djehuti.Core;

/// <summary>
/// Djehuti Cyberscope-specific wrapper around Core.ProjectFileService.
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
