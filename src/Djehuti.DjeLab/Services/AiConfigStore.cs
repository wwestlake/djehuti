using Microsoft.JSInterop;

namespace Djehuti.DjeLab.Services;

/// <summary>
/// Reads/writes the user's BYOK OpenAI key and model choice to localStorage --
/// same storage mechanism and philosophy as Cyberscope's Live Lab
/// (Djehuti.Dashboard): a plain browser-local key, never sent to Djehuti's
/// own servers, used to call OpenAI directly from the client. No encryption,
/// no server-side validation call -- matches that existing precedent rather
/// than inventing a heavier pattern for the same concern.
/// </summary>
public sealed class AiConfigStore
{
    public const string DefaultModel = "gpt-4o-mini";

    private const string ApiKeyStorageKey = "djelab.aiApiKey";
    private const string ModelStorageKey = "djelab.aiModel";

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
