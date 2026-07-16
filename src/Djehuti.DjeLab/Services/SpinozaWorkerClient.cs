using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Djehuti.DjeLab.Services;

public sealed record RunOutcome(bool Success, string? ResultJson, string? Error);

/// <summary>
/// Owns exactly one Web Worker running Spinoza programs off the UI thread
/// (see wwwroot/js/spinoza-worker.js and Simulation/SpinozaWorker.cs).
/// Each graph pane creates its own instance, so multiple graph windows
/// running independent simulations never cross-talk or contend for the
/// same worker thread.
///
/// A running program can only be interrupted by terminating its worker --
/// the worker's own thread is fully occupied by the synchronous evaluator
/// loop while a run is in flight, so no other message (including a "stop")
/// can be serviced until it either finishes or is killed outright. Stop
/// therefore tears the worker down; the next RunAsync call boots a fresh one.
/// </summary>
public sealed class SpinozaWorkerClient : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly string _workerScriptUrl;
    private IJSObjectReference? _module;
    private IJSObjectReference? _worker;
    private DotNetObjectReference<SpinozaWorkerClient>? _selfRef;
    private TaskCompletionSource<bool>? _readyTcs;
    private readonly Dictionary<string, RunHandlers> _activeRuns = new();

    public SpinozaWorkerClient(IJSRuntime js, NavigationManager nav)
    {
        _js = js;
        // Computed here, not in JS, deliberately: spinoza-worker-client.js
        // is itself loaded through Blazor's dynamic import() mechanism,
        // which resolves that module's own import.meta.url to a
        // virtualized path under _framework/ rather than where it's
        // actually served from -- a sibling-relative URL computed from
        // inside that module pointed at the wrong place. NavigationManager
        // knows the real, correct base href (including under a deployment
        // subpath like /math/), so the URL is built here instead.
        _workerScriptUrl = new Uri(new Uri(nav.BaseUri), "js/spinoza-worker.js").ToString();
    }

    private sealed class RunHandlers
    {
        public required Action<JsonElement> OnEmit { get; init; }
        public required TaskCompletionSource<RunOutcome> Completion { get; init; }
    }

    public async Task EnsureStartedAsync()
    {
        if (_worker != null) return;

        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/spinoza-worker-client.js");
        _selfRef = DotNetObjectReference.Create(this);
        _readyTcs = new TaskCompletionSource<bool>();
        _worker = await _module.InvokeAsync<IJSObjectReference>("createWorker", _selfRef, _workerScriptUrl);
        await _readyTcs.Task;
    }

    public async Task<RunOutcome> RunAsync(string source, Action<JsonElement> onEmit, string? runtimeDataJson = null, string? parametersJson = null)
    {
        await EnsureStartedAsync();

        var runId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<RunOutcome>();
        _activeRuns[runId] = new RunHandlers { OnEmit = onEmit, Completion = tcs };

        await _module!.InvokeVoidAsync("postRun", _worker, runId, source, runtimeDataJson, parametersJson);
        return await tcs.Task;
    }

    /// <summary>Kills the current run (if any) and its worker outright.
    /// The next RunAsync call transparently boots a fresh worker.</summary>
    public async Task StopAsync()
    {
        if (_worker == null || _module == null) return;

        await _module.InvokeVoidAsync("terminate", _worker);
        await _worker.DisposeAsync();
        _worker = null;

        foreach (var handlers in _activeRuns.Values)
            handlers.Completion.TrySetResult(new RunOutcome(false, null, "Stopped"));
        _activeRuns.Clear();
    }

    [JSInvokable]
    public void OnWorkerReady(string? error)
    {
        if (error != null) _readyTcs?.TrySetException(new InvalidOperationException(error));
        else _readyTcs?.TrySetResult(true);
    }

    [JSInvokable]
    public void OnEmit(string runId, string json)
    {
        if (_activeRuns.TryGetValue(runId, out var handlers))
            handlers.OnEmit(JsonDocument.Parse(json).RootElement.Clone());
    }

    [JSInvokable]
    public void OnResult(string runId, string json)
    {
        if (_activeRuns.Remove(runId, out var handlers))
            handlers.Completion.TrySetResult(new RunOutcome(true, json, null));
    }

    [JSInvokable]
    public void OnError(string runId, string message)
    {
        if (_activeRuns.Remove(runId, out var handlers))
            handlers.Completion.TrySetResult(new RunOutcome(false, null, message));
    }

    public async ValueTask DisposeAsync()
    {
        if (_worker != null && _module != null)
        {
            await _module.InvokeVoidAsync("terminate", _worker);
            await _worker.DisposeAsync();
        }
        if (_module != null) await _module.DisposeAsync();
        _selfRef?.Dispose();
    }
}
