using System.Text.Json.Serialization;

namespace Djehuti.DjeLab.Services;

// JSON-serializable demo script that AI can generate
public sealed class DemoScript
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "Demo";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("steps")]
    public List<DemoStep> Steps { get; set; } = new();
}

// Single atomic action + annotation
public sealed class DemoStep
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("action")]
    public DemoAction Action { get; set; } = new();

    [JsonPropertyName("annotation")]
    public string? Annotation { get; set; }

    [JsonPropertyName("pointerTarget")]
    public string? PointerTarget { get; set; } // "pane:graph", "element:import-btn", etc.

    [JsonPropertyName("duration")]
    public int DurationMs { get; set; } = 2000; // How long to show annotation
}

// Action types that DemoMode can execute
public sealed class DemoAction
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "none"; // activatePane, selectFile, setFilterValue, etc.

    [JsonPropertyName("params")]
    public Dictionary<string, object?> Params { get; set; } = new();
}

// Runtime state for annotation display
public sealed class DemoAnnotationState
{
    public string? CurrentStepId { get; set; }
    public string? Annotation { get; set; }
    public string? PointerTarget { get; set; }
    public bool IsVisible { get; set; }
    public int CurrentStepIndex { get; set; }
    public int TotalSteps { get; set; }
}
