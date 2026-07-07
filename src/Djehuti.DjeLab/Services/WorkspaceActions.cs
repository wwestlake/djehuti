namespace Djehuti.DjeLab.Services;

public sealed record GraphRunOutcome(bool Success, int PointCount, string? Error);

/// <summary>
/// The bridge between Chat's run_simulation tool handler and the docking
/// workspace. ChatPane and DockWorkspace are siblings in the component tree
/// with no direct reference to each other, so this is the shared channel:
/// Chat calls RunInNewGraphAsync and awaits the real outcome (not just
/// "request sent") so the AI's next reply can accurately describe what
/// happened; DockWorkspace listens for the request event to actually create
/// the pane, and GraphPane calls ReportOutcome once its auto-triggered run
/// finishes.
///
/// Registered as a singleton (Program.cs) -- Blazor WASM has exactly one
/// root scope for the whole app session anyway, so this is the same
/// lifetime as "shared for the page," just made explicit.
/// </summary>
public sealed class WorkspaceActions
{
    private readonly Dictionary<string, TaskCompletionSource<GraphRunOutcome>> _pending = new();

    public event Action<OpenGraphRequest>? OpenGraphRequested;

    public Task<GraphRunOutcome> RunInNewGraphAsync(string chartType, string xLabel, string yLabel, string zLabel, string spinozaSource)
    {
        var runId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<GraphRunOutcome>();
        _pending[runId] = tcs;

        OpenGraphRequested?.Invoke(new OpenGraphRequest(runId, chartType, xLabel, yLabel, zLabel, spinozaSource));

        return tcs.Task;
    }

    public void ReportOutcome(string runId, GraphRunOutcome outcome)
    {
        if (_pending.Remove(runId, out var tcs))
            tcs.TrySetResult(outcome);
    }
}

public sealed record OpenGraphRequest(string RunId, string ChartType, string XLabel, string YLabel, string ZLabel, string SpinozaSource);
