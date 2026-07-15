using Microsoft.JSInterop;

namespace Djehuti.Teacher.Services;

// BYOK key storage -- same mechanism/philosophy as DjeLab's AiConfigStore:
// plain browser localStorage, never sent to Djehuti's own servers, used to
// call the AI provider directly from the client. Kept as its own copy
// (not shared) since the two apps' localStorage keys must not collide.
public sealed class AiConfigStore
{
    public const string DefaultModel = "gpt-4o-mini";
    public const ApiProvider DefaultProvider = ApiProvider.OpenAi;

    private const string ApiKeyStorageKey = "teacher.aiApiKey";
    private const string ModelStorageKey = "teacher.aiModel";
    private const string ProviderStorageKey = "teacher.aiProvider";

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

    public async Task<ApiProvider> GetProviderAsync()
    {
        var stored = await _js.InvokeAsync<string?>("localStorage.getItem", ProviderStorageKey);
        return stored switch
        {
            "openai" => ApiProvider.OpenAi,
            "anthropic" => ApiProvider.Anthropic,
            "google_gemini" => ApiProvider.GoogleGemini,
            "mistral" => ApiProvider.Mistral,
            _ => DefaultProvider,
        };
    }

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

    public async Task SetProviderAsync(ApiProvider provider)
    {
        var value = provider switch
        {
            ApiProvider.OpenAi => "openai",
            ApiProvider.Anthropic => "anthropic",
            ApiProvider.GoogleGemini => "google_gemini",
            ApiProvider.Mistral => "mistral",
            _ => "openai",
        };
        await _js.InvokeVoidAsync("localStorage.setItem", ProviderStorageKey, value);
    }

    public async Task ClearAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", ApiKeyStorageKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", ModelStorageKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", ProviderStorageKey);
    }
}
