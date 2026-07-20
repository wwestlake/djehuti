using System.Text.Json;
using Djehuti.Architect.Models;

namespace Djehuti.Architect.Services;

/// <summary>
/// Generates ArchitectureModel from source code analysis via AI.
/// Integrates with WebLLM/Ollama/BYOK providers.
/// </summary>
public sealed class AiModelGenerator
{
    private readonly HttpClient _http;

    public AiModelGenerator(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Generate architecture model from repository structure via AI analysis.
    /// </summary>
    public async Task<ArchitectureModel?> GenerateFromRepositoryAsync(
        string repoName,
        string repoUrl,
        Dictionary<string, object> repoStructure,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildAnalysisPrompt(repoName, repoUrl, repoStructure);

            // Call AI analysis endpoint (to be wired up)
            // For now, return a basic model structure
            var model = new ArchitectureModel
            {
                Name = $"{repoName} Architecture",
                Description = $"Auto-generated architecture model from {repoName} repository analysis",
                SourceRepository = repoUrl,
                Components = new()
                {
                    new()
                    {
                        Id = "main-app",
                        Name = "Application",
                        Kind = ComponentKind.Service,
                        Technology = DetectPrimaryTechnology(repoStructure),
                        Description = $"Main application in {repoName}"
                    }
                }
            };

            return model;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AI model generation error: {ex.Message}");
            return null;
        }
    }

    private string BuildAnalysisPrompt(string repoName, string repoUrl, Dictionary<string, object> repoStructure)
    {
        return $@"Analyze this repository structure and generate a software architecture model.

Repository: {repoName}
URL: {repoUrl}

Structure: {JsonSerializer.Serialize(repoStructure)}

Generate a JSON ArchitectureModel with:
- Components (services, libraries, databases, external systems)
- Connections between components
- Technology stack

Return valid JSON only, no markdown or explanation.";
    }

    private string DetectPrimaryTechnology(Dictionary<string, object> repoStructure)
    {
        // Simple detection based on file patterns
        var files = repoStructure.Keys.ToLower().ToString() ?? "";

        return files.Contains("package.json") ? "Node.js" :
               files.Contains(".csproj") ? ".NET" :
               files.Contains("pom.xml") ? "Java/Maven" :
               files.Contains("go.mod") ? "Go" :
               files.Contains("cargo.toml") ? "Rust" :
               files.Contains("requirements.txt") ? "Python" :
               "Unknown";
    }
}
