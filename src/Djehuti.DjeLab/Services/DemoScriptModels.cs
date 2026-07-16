using System.Text.Json.Serialization;

namespace Djehuti.DjeLab.Services;

[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public sealed class DemoActionJson
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "none";

    [JsonPropertyName("params")]
    public Dictionary<string, object?>? Params { get; set; }
}

public sealed class DemoStepJson
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("action")]
    public DemoActionJson Action { get; set; } = new();

    [JsonPropertyName("annotation")]
    public string? Annotation { get; set; }

    [JsonPropertyName("pointerTarget")]
    public string? PointerTarget { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; } = 2000;
}

public sealed class DemoScriptJson
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "Demo";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("steps")]
    public List<DemoStepJson> Steps { get; set; } = new();
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
