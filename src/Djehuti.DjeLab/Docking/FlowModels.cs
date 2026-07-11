namespace Djehuti.DjeLab.Docking;

// New members must be appended at the end, never inserted -- FlowNodeModel
// instances persist to localStorage with this enum serialized as an integer
// (System.Text.Json's default), so inserting a value in the middle silently
// reinterprets every stale saved node whose Kind integer now points at a
// different member. Confirmed this exact failure mode live: inserting
// Constant before Transform made every previously-saved Transform node
// render (and compile) as a Constant instead.
public enum FlowNodeKind
{
    Source,
    SequenceSource,
    Transform,
    Filter,
    Integrator,
    Plot,
    Constant
}

public enum FlowConstantValueKind
{
    Number,
    Bool,
    Vector
}

public enum FlowIntegratorMethod
{
    Euler,
    RK4
}

public sealed class FlowNodeModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public FlowNodeKind Kind { get; set; }
    public string Title { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public string? InputFromNodeId { get; set; }
    public string? OutputToNodeId { get; set; }
    public string FilePath { get; set; } = "";
    public string ColumnsCsv { get; set; } = "";
    public string SequenceKind { get; set; } = "range";
    public string StartExpression { get; set; } = "0.0";
    public string StopExpression { get; set; } = "10.0";
    public string StepExpression { get; set; } = "1.0";
    public string CountExpression { get; set; } = "100.0";
    public string TransformExpression { get; set; } = "row[0]";
    public string FilterExpression { get; set; } = "true";
    public FlowIntegratorMethod IntegratorMethod { get; set; } = FlowIntegratorMethod.Euler;
    public string InitialStateExpression { get; set; } = "0.0";
    public string StepSizeExpression { get; set; } = "1.0";
    public string DerivativeExpression { get; set; } = "signal";
    public string ChartType { get; set; } = "histogram";
    public string XLabel { get; set; } = "Value";
    public string YLabel { get; set; } = "Frequency";
    public string ZLabel { get; set; } = "Height";
    public bool IsCollapsed { get; set; }
    public string ConstantName { get; set; } = "k";
    public FlowConstantValueKind ConstantValueKind { get; set; } = FlowConstantValueKind.Number;
    public string ConstantValueExpression { get; set; } = "1.0";
}

public sealed record FlowCompileResult(
    bool Success,
    string Source,
    string? RuntimeDataJson,
    string ChartType,
    string XLabel,
    string YLabel,
    string ZLabel,
    string Summary,
    IReadOnlyList<string> Warnings)
{
    // Spinoza's grammar has no comment syntax at all -- confirmed directly
    // against the real parser, a `//` line fails to parse, full stop. Source
    // (with `//` annotations) is for the human-facing "Source code" viewer
    // only; ExecutableSource is what actually gets sent to the sandbox/worker
    // to run. Kept as a computed property (not a stored field the caller must
    // remember to set) so there's exactly one place this stripping happens.
    public string ExecutableSource =>
        string.Join('\n', Source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
}

public sealed record FlowPaneSnapshot(List<FlowNodeModel> Nodes);
