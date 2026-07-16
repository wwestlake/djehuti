namespace Djehuti.Teacher.Services.Helper;

public class GuidanceMessage
{
    public string Type { get; set; } = "guidance"; // "inference", "guidance", "error"
    public string Content { get; set; } = "";
    public List<GuidanceAction> Actions { get; set; } = new();
    public string Tone { get; set; } = "helpful"; // "helpful", "urgent", "neutral"
}

public class GuidanceAction
{
    public string Label { get; set; } = "";
    public string ActionType { get; set; } = "navigate"; // "navigate", "external", "setup"
    public string Target { get; set; } = "";
    public string Description { get; set; } = "";
}
