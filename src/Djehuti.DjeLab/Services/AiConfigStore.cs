using Microsoft.JSInterop;

namespace Djehuti.DjeLab.Services;

/// <summary>
/// Reads/writes the user's BYOK OpenAI key and model choice to localStorage --
/// same storage mechanism and philosophy as Cyberscope's Live Lab. DjeLab
/// scopes those values to the signed-in account so one login does not leak
/// settings into another login on the same browser.
/// </summary>
public sealed class AiConfigStore
{
    public const string DefaultModel = "gpt-4o-mini";

    private const string ApiKeyStorageKey = "djelab.aiApiKey";
    private const string ModelStorageKey = "djelab.aiModel";

    private readonly IJSRuntime _js;
    private readonly DjeLabStorageScopeService _scope;

    public AiConfigStore(IJSRuntime js, DjeLabStorageScopeService scope)
    {
        _js = js;
        _scope = scope;
    }

    public async Task<string?> GetApiKeyAsync()
    {
        var scopedKey = await _scope.QualifyAsync(ApiKeyStorageKey);
        var apiKey = await _js.InvokeAsync<string?>("localStorage.getItem", scopedKey);
        if (!string.IsNullOrWhiteSpace(apiKey))
            return apiKey;

        var legacyApiKey = await _js.InvokeAsync<string?>("localStorage.getItem", ApiKeyStorageKey);
        if (string.IsNullOrWhiteSpace(legacyApiKey))
            return null;

        await _js.InvokeVoidAsync("localStorage.setItem", scopedKey, legacyApiKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", ApiKeyStorageKey);
        return legacyApiKey;
    }

    public async Task<string> GetModelAsync()
    {
        var scopedKey = await _scope.QualifyAsync(ModelStorageKey);
        var model = await _js.InvokeAsync<string?>("localStorage.getItem", scopedKey);
        if (!string.IsNullOrWhiteSpace(model))
            return model;

        var legacyModel = await _js.InvokeAsync<string?>("localStorage.getItem", ModelStorageKey);
        if (!string.IsNullOrWhiteSpace(legacyModel))
        {
            await _js.InvokeVoidAsync("localStorage.setItem", scopedKey, legacyModel);
            await _js.InvokeVoidAsync("localStorage.removeItem", ModelStorageKey);
            return legacyModel;
        }

        return DefaultModel;
    }

    public async Task SetApiKeyAsync(string? apiKey)
    {
        var scopedKey = await _scope.QualifyAsync(ApiKeyStorageKey);
        if (string.IsNullOrWhiteSpace(apiKey))
            await _js.InvokeVoidAsync("localStorage.removeItem", scopedKey);
        else
            await _js.InvokeVoidAsync("localStorage.setItem", scopedKey, apiKey.Trim());

        await _js.InvokeVoidAsync("localStorage.removeItem", ApiKeyStorageKey);
    }

    public async Task SetModelAsync(string? model)
    {
        var scopedKey = await _scope.QualifyAsync(ModelStorageKey);
        if (string.IsNullOrWhiteSpace(model))
            await _js.InvokeVoidAsync("localStorage.removeItem", scopedKey);
        else
            await _js.InvokeVoidAsync("localStorage.setItem", scopedKey, model.Trim());

        await _js.InvokeVoidAsync("localStorage.removeItem", ModelStorageKey);
    }

    public async Task ClearAsync()
    {
        var scopedApiKey = await _scope.QualifyAsync(ApiKeyStorageKey);
        var scopedModel = await _scope.QualifyAsync(ModelStorageKey);

        await _js.InvokeVoidAsync("localStorage.removeItem", scopedApiKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", scopedModel);
        await _js.InvokeVoidAsync("localStorage.removeItem", ApiKeyStorageKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", ModelStorageKey);
    }
}
