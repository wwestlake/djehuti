namespace Djehuti.DjeLab.Docking;

public enum FlowNodeKind
{
    Source,
    Transform,
    Filter,
    Integrator,
    Plot
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
    IReadOnlyList<string> Warnings);

public sealed record FlowPaneSnapshot(List<FlowNodeModel> Nodes);
