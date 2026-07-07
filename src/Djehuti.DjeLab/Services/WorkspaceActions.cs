using System.Text.Json;

namespace Djehuti.DjeLab.Services;

public sealed record GraphRunOutcome(bool Success, int PointCount, string? Error);
public sealed record GraphDataSnapshot(
    string RunId,
    string PaneId,
    string ChartType,
    string XLabel,
    string YLabel,
    string ZLabel,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    bool Running,
    string? Error);

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
    private readonly object _graphDataLock = new();
    private GraphDataState _graphData = GraphDataState.Empty;

    public event Action<OpenGraphRequest>? OpenGraphRequested;
    public event Action<GraphDataSnapshot>? GraphDataChanged;

    public GraphDataSnapshot CurrentGraphData
    {
        get
        {
            lock (_graphDataLock)
            {
                return _graphData.ToSnapshot();
            }
        }
    }

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

    public void BeginGraphDataRun(string runId, string paneId, string chartType, string xLabel, string yLabel, string zLabel)
    {
        GraphDataSnapshot snapshot;
        lock (_graphDataLock)
        {
            _graphData = new GraphDataState(runId, paneId, chartType, xLabel, yLabel, zLabel);
            snapshot = _graphData.ToSnapshot();
        }

        GraphDataChanged?.Invoke(snapshot);
    }

    public void ReportGraphDataEmit(string runId, JsonElement point)
    {
        GraphDataSnapshot? snapshot = null;
        lock (_graphDataLock)
        {
            if (_graphData.RunId != runId || string.IsNullOrWhiteSpace(_graphData.RunId)) return;

            var row = NormalizePoint(_graphData.ChartType, _graphData.Rows.Count, point);
            if (_graphData.Headers.Count == 0)
                _graphData.Headers = BuildHeaders(_graphData.ChartType, row.Count, _graphData.XLabel, _graphData.YLabel, _graphData.ZLabel).ToList();

            _graphData.Rows.Add(row);
            snapshot = _graphData.ToSnapshot();
        }

        if (snapshot is not null) GraphDataChanged?.Invoke(snapshot);
    }

    public void CompleteGraphDataRun(string runId, GraphRunOutcome outcome)
    {
        GraphDataSnapshot? snapshot = null;
        lock (_graphDataLock)
        {
            if (_graphData.RunId != runId || string.IsNullOrWhiteSpace(_graphData.RunId)) return;

            _graphData.Running = false;
            _graphData.Error = outcome.Success ? null : outcome.Error;
            snapshot = _graphData.ToSnapshot();
        }

        if (snapshot is not null) GraphDataChanged?.Invoke(snapshot);
    }

    private static List<string> NormalizePoint(string chartType, int rowIndex, JsonElement point)
    {
        var values = point.ValueKind == JsonValueKind.Array
            ? point.EnumerateArray().Select(FormatValue).ToList()
            : new List<string> { FormatValue(point) };

        if (values.Count == 0)
            values.Add("");

        return chartType switch
        {
            "histogram" => new List<string> { values.FirstOrDefault() ?? "" },
            "surface" => values,
            "scatter3d" => NormalizeScatter3D(values, rowIndex),
            _ => NormalizeTwoDimensional(values, rowIndex)
        };
    }

    private static List<string> NormalizeTwoDimensional(List<string> values, int rowIndex)
    {
        if (values.Count <= 1)
            return new List<string> { rowIndex.ToString(), values.FirstOrDefault() ?? "" };

        return values;
    }

    private static List<string> NormalizeScatter3D(List<string> values, int rowIndex)
    {
        if (values.Count >= 3)
            return new List<string> { values[0], values[1], values[2] };

        var x = rowIndex.ToString();
        var y = values.ElementAtOrDefault(0) ?? "0";
        var z = values.ElementAtOrDefault(1) ?? "0";
        return new List<string> { x, y, z };
    }

    private static IReadOnlyList<string> BuildHeaders(string chartType, int valueCount, string xLabel, string yLabel, string zLabel)
    {
        return chartType switch
        {
            "histogram" => new[] { "value" },
            "surface" => Enumerable.Range(1, Math.Max(1, valueCount)).Select(i => $"z{i}").ToArray(),
            "scatter3d" => new[] { xLabel, yLabel, zLabel },
            _ when valueCount <= 1 => new[] { xLabel, yLabel },
            _ when valueCount == 2 => new[] { xLabel, yLabel },
            _ => new[] { xLabel }.Concat(Enumerable.Range(1, valueCount - 1).Select(i => $"y{i}")).ToArray()
        };
    }

    private static string FormatValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => value.GetRawText()
        };

    private sealed class GraphDataState
    {
        public static GraphDataState Empty { get; } = new("", "", "line", "x", "y", "z") { Running = false };

        public GraphDataState(string runId, string paneId, string chartType, string xLabel, string yLabel, string zLabel)
        {
            RunId = runId;
            PaneId = paneId;
            ChartType = chartType;
            XLabel = xLabel;
            YLabel = yLabel;
            ZLabel = zLabel;
        }

        public string RunId { get; }
        public string PaneId { get; }
        public string ChartType { get; }
        public string XLabel { get; }
        public string YLabel { get; }
        public string ZLabel { get; }
        public bool Running { get; set; } = true;
        public string? Error { get; set; }
        public List<string> Headers { get; set; } = new();
        public List<List<string>> Rows { get; } = new();

        public GraphDataSnapshot ToSnapshot() =>
            new(
                RunId,
                PaneId,
                ChartType,
                XLabel,
                YLabel,
                ZLabel,
                Headers.ToArray(),
                Rows.Select(row => (IReadOnlyList<string>)row.ToArray()).ToArray(),
                Running,
                Error);
    }
}

public sealed record OpenGraphRequest(string RunId, string ChartType, string XLabel, string YLabel, string ZLabel, string SpinozaSource);
