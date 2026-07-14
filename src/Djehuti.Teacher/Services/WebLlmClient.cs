using System.Text.Json;
using Microsoft.JSInterop;

namespace Djehuti.Teacher.Services;

public enum WebLlmState
{
    Unsupported,
    NotStarted,
    Loading,
    Ready,
    Failed,
}

// Real in-browser LLM inference (mlc-ai/web-llm over WebGPU), not a
// scripted stand-in. See wwwroot/js/webllm-interop.js for what actually
// runs. Same client as DjeLab's WebLlmClient.cs (duplicated, not shared --
// there's no common C# project both Blazor apps reference for services).
// Powers the free, no-key tutor on a lesson page; a BYOK key in AI Setup
// is the upgrade path to a stronger hosted model, same split as DjeLab's
// Ibis/Seshat.
public sealed class WebLlmClient : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<WebLlmClient>? _selfRef;
    private Task<bool>? _initTask;

    public WebLlmState State { get; private set; } = WebLlmState.NotStarted;
    public string ProgressText { get; private set; } = "";
    public double ProgressFraction { get; private set; }

    public event Action? StateChanged;

    public WebLlmClient(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<bool> IsSupportedAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/webllm-interop.js");
        return await _module.InvokeAsync<bool>("isSupported");
    }

    // Idempotent -- safe to call every time the user opens Ibis; only the
    // first call actually triggers a model download, later calls just
    // await the same in-flight/completed task.
    public Task<bool> EnsureReadyAsync()
    {
        _initTask ??= InitCoreAsync();
        return _initTask;
    }

    private async Task<bool> InitCoreAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/webllm-interop.js");

        if (!await _module.InvokeAsync<bool>("isSupported"))
        {
            State = WebLlmState.Unsupported;
            StateChanged?.Invoke();
            return false;
        }

        State = WebLlmState.Loading;
        StateChanged?.Invoke();

        _selfRef = DotNetObjectReference.Create(this);
        try
        {
            var ok = await _module.InvokeAsync<bool>("init", _selfRef);
            State = ok ? WebLlmState.Ready : WebLlmState.Failed;
            StateChanged?.Invoke();
            return ok;
        }
        catch
        {
            State = WebLlmState.Failed;
            StateChanged?.Invoke();
            return false;
        }
    }

    [JSInvokable]
    public void OnWebLlmProgress(string text, double progress)
    {
        ProgressText = text;
        ProgressFraction = progress;
        StateChanged?.Invoke();
    }

    public async Task<string> ChatAsync(IEnumerable<(string Role, string Content)> messages)
    {
        if (State != WebLlmState.Ready || _module is null)
            throw new InvalidOperationException("WebLLM is not ready yet.");

        var payload = messages.Select(m => new { role = m.Role, content = m.Content });
        var json = JsonSerializer.Serialize(payload);
        return await _module.InvokeAsync<string>("chat", json);
    }

    public async ValueTask DisposeAsync()
    {
        _selfRef?.Dispose();
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
