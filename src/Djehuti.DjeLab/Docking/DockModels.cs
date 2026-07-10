using System.Text.Json.Serialization;

namespace Djehuti.DjeLab.Docking;

public enum SplitDirection
{
    Row,
    Column
}

public enum DockZone
{
    Center,
    Top,
    Bottom,
    Left,
    Right
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(SplitNode), "split")]
[JsonDerivedType(typeof(TabGroupNode), "tabs")]
public abstract class DockNode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
}

public sealed class SplitNode : DockNode
{
    public SplitDirection Direction { get; set; }
    public List<DockNode> Children { get; } = new();
    public List<double> Sizes { get; set; } = new();
}

public sealed class TabGroupNode : DockNode
{
    public List<string> PaneIds { get; } = new();
    public int ActiveIndex { get; set; }
}

public sealed class PaneDescriptor
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "placeholder";
    public string AccentColor { get; set; } = "#3fa9f5";

    // Set only when a graph pane is created via WorkspaceActions (the AI's
    // run_simulation tool call), not by the "New Graph" button or the
    // initial layout -- GraphPane checks for this on mount and immediately
    // runs it, then reports the outcome back via WorkspaceActions.ReportOutcome
    // keyed by PendingRunId.
    public string? PendingRunId { get; set; }
    public string? PendingChartType { get; set; }
    public string? PendingXLabel { get; set; }
    public string? PendingYLabel { get; set; }
    public string? PendingZLabel { get; set; }
    public string? PendingSource { get; set; }
    public string? PendingRuntimeInputJson { get; set; }
}

public sealed record ResizeStart(SplitNode Split, int Index, double ClientX, double ClientY);
public sealed record TabActivate(TabGroupNode Group, string PaneId);
public sealed record TabStripDrop(string TargetGroupId, int InsertIndex);
public sealed record BodyDrop(string TargetGroupId, DockZone Zone);
