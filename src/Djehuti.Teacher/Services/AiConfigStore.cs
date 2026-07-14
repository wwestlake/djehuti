using Microsoft.JSInterop;

namespace Djehuti.Teacher.Services;

// BYOK key storage -- same mechanism/philosophy as DjeLab's AiConfigStore:
// plain browser localStorage, never sent to Djehuti's own servers, used to
// call the AI provider directly from the client. Kept as its own copy
// (not shared) since the two apps' localStorage keys must not collide.
public sealed class AiConfigStore
{
    public const string DefaultModel = "claude-sonnet-4-5";

    private const string ApiKeyStorageKey = "teacher.aiApiKey";
    private const string ModelStorageKey = "teacher.aiModel";

    private readonly IJSRuntime _js;

    public AiConfigStore(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string?> GetApiKeyAsync() =>
        await _js.InvokeAsync<string?>("localStorage.getItem", ApiKeyStorageKey);

    public async Task<string> GetModelAsync() =>
        await _js.InvokeAsync<string?>("localStorage.getItem", ModelStorageKey) is { Length: > 0 } model
            ? model
            : DefaultModel;

    public async Task SetApiKeyAsync(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            await _js.InvokeVoidAsync("localStorage.removeItem", ApiKeyStorageKey);
        else
            await _js.InvokeVoidAsync("localStorage.setItem", ApiKeyStorageKey, apiKey.Trim());
    }

    public async Task SetModelAsync(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            await _js.InvokeVoidAsync("localStorage.removeItem", ModelStorageKey);
        else
            await _js.InvokeVoidAsync("localStorage.setItem", ModelStorageKey, model.Trim());
    }

    public async Task ClearAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", ApiKeyStorageKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", ModelStorageKey);
    }
}
