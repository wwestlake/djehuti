using System.Text.Json;
using Djehuti.DjeLab;

namespace Djehuti.DjeLab.Services;

/// <summary>
/// DjeLab-specific wrapper around Core.ProjectFileService.
/// Handles Spinoza DSL programs, simulation results, and generated outputs.
/// Uses generalized project file system for save/load/export.
/// </summary>
public sealed class ProjectFileService
{
    private const string AppName = "DjeLab";
    private static readonly Dictionary<string, string> FolderDescriptions = new()
    {
        { "spinoza", "Spinoza DSL source programs" },
        { "simulations", "Simulation data and results" },
        { "graphs", "Generated graph definitions and layouts" },
        { "exports", "Exported outputs (JSON, CSV, visualizations)" },
        { "metadata", "Program metadata, constants, configuration" }
    };

    /// <summary>
    /// Create a DjeLab project file (.dapp) from Spinoza programs and simulation results.
    /// </summary>
    public static byte[] CreateProjectArchive(
        string projectName,
        string? description,
        Dictionary<string, string> spinozaPrograms,
        Dictionary<string, string> simulationResults,
        Dictionary<string, byte[]>? exports = null)
    {
        var contents = new Dictionary<string, byte[]>();

        // Spinoza folder
        foreach (var (progName, progSource) in spinozaPrograms)
        {
            contents[$"spinoza/{progName}.spinoza"] = System.Text.Encoding.UTF8.GetBytes(progSource);
        }

        // Simulations folder
        foreach (var (simName, simJson) in simulationResults)
        {
            contents[$"simulations/{simName}.json"] = System.Text.Encoding.UTF8.GetBytes(simJson);
        }

        // Exports folder (graphs, reports, etc)
        if (exports != null)
        {
            foreach (var (exportPath, exportData) in exports)
            {
                contents[$"exports/{exportPath}"] = exportData;
            }
        }

        return Djehuti.Core.ProjectFileService.CreateProjectArchive(
            appName: AppName,
            projectName: projectName,
            contents: contents,
            description: description,
            folderDescriptions: FolderDescriptions
        );
    }

    /// <summary>
    /// Extract DjeLab project from .dapp file.
    /// </summary>
    public static (Dictionary<string, string> Programs, Dictionary<string, string> Results, Dictionary<string, byte[]> Exports) ExtractProjectArchive(byte[] zipData)
    {
        var archive = Djehuti.Core.ProjectFileService.ExtractProjectArchive(zipData);

        var programs = new Dictionary<string, string>();
        var results = new Dictionary<string, string>();
        var exports = new Dictionary<string, byte[]>();

        foreach (var (path, data) in archive.Contents)
        {
            if (path.StartsWith("spinoza/") && path.EndsWith(".spinoza"))
            {
                var source = System.Text.Encoding.UTF8.GetString(data);
                var progName = Path.GetFileNameWithoutExtension(path.Replace("spinoza/", ""));
                programs[progName] = source;
            }
            else if (path.StartsWith("simulations/") && path.EndsWith(".json"))
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var simName = Path.GetFileNameWithoutExtension(path.Replace("simulations/", ""));
                results[simName] = json;
            }
            else if (path.StartsWith("exports/"))
            {
                var exportPath = path.Replace("exports/", "");
                exports[exportPath] = data;
            }
        }

        return (programs, results, exports);
    }
}
