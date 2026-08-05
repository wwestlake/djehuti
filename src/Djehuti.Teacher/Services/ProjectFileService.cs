using System.Text.Json;
using Djehuti.Teacher;

namespace Djehuti.Teacher.Services;

/// <summary>
/// Teacher/Learn-specific wrapper around Core.ProjectFileService.
/// Handles lessons, student submissions, grading, and course materials.
/// Uses generalized project file system for save/load/export.
/// </summary>
public sealed class ProjectFileService
{
    private const string AppName = "Learn";
    private static readonly Dictionary<string, string> FolderDescriptions = new()
    {
        { "lessons", "Lesson content and definitions" },
        { "submissions", "Student submissions and responses" },
        { "grading", "Grading rubrics and feedback" },
        { "materials", "Course materials and resources" },
        { "exports", "Exported reports (transcripts, analytics, documents)" },
        { "metadata", "Course metadata, settings, configuration" }
    };

    /// <summary>
    /// Create a lesson/course project file (.dapp) from lesson data and submissions.
    /// </summary>
    public static byte[] CreateProjectArchive(
        string projectName,
        string? description,
        Dictionary<string, string> lessons,
        Dictionary<string, string> submissions,
        Dictionary<string, string>? grading = null,
        Dictionary<string, byte[]>? exports = null)
    {
        var contents = new Dictionary<string, byte[]>();

        // Lessons folder
        foreach (var (lessonName, lessonJson) in lessons)
        {
            contents[$"lessons/{lessonName}.json"] = System.Text.Encoding.UTF8.GetBytes(lessonJson);
        }

        // Submissions folder
        foreach (var (submissionName, submissionJson) in submissions)
        {
            contents[$"submissions/{submissionName}.json"] = System.Text.Encoding.UTF8.GetBytes(submissionJson);
        }

        // Grading folder
        if (grading != null)
        {
            foreach (var (gradingName, gradingJson) in grading)
            {
                contents[$"grading/{gradingName}.json"] = System.Text.Encoding.UTF8.GetBytes(gradingJson);
            }
        }

        // Exports folder (reports, transcripts, etc)
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
    /// Extract lesson/course project from .dapp file.
    /// </summary>
    public static (Dictionary<string, string> Lessons, Dictionary<string, string> Submissions, Dictionary<string, string> Grading, Dictionary<string, byte[]> Exports) ExtractProjectArchive(byte[] zipData)
    {
        var archive = Djehuti.Core.ProjectFileService.ExtractProjectArchive(zipData);

        var lessons = new Dictionary<string, string>();
        var submissions = new Dictionary<string, string>();
        var grading = new Dictionary<string, string>();
        var exports = new Dictionary<string, byte[]>();

        foreach (var (path, data) in archive.Contents)
        {
            if (path.StartsWith("lessons/") && path.EndsWith(".json"))
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var lessonName = Path.GetFileNameWithoutExtension(path.Replace("lessons/", ""));
                lessons[lessonName] = json;
            }
            else if (path.StartsWith("submissions/") && path.EndsWith(".json"))
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var submissionName = Path.GetFileNameWithoutExtension(path.Replace("submissions/", ""));
                submissions[submissionName] = json;
            }
            else if (path.StartsWith("grading/") && path.EndsWith(".json"))
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var gradingName = Path.GetFileNameWithoutExtension(path.Replace("grading/", ""));
                grading[gradingName] = json;
            }
            else if (path.StartsWith("exports/"))
            {
                var exportPath = path.Replace("exports/", "");
                exports[exportPath] = data;
            }
        }

        return (lessons, submissions, grading, exports);
    }
}
