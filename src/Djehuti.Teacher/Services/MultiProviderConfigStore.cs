using Microsoft.JSInterop;

namespace Djehuti.Teacher.Services;

public class MultiProviderConfigStore
{
    private readonly IJSRuntime _js;
    private const string ACTIVE_PROVIDER_KEY = "teacher.activeProvider";
    private const string API_KEY_PREFIX = "teacher.apiKey.";
    private const string MODEL_PREFIX = "teacher.model.";

    public MultiProviderConfigStore(IJSRuntime js)
    {
        _js = js;
    }

    // Set API key for a specific provider
    public async Task SetApiKeyAsync(string provider, string apiKey)
    {
        var key = $"{API_KEY_PREFIX}{provider.ToLowerInvariant()}";
        await _js.InvokeVoidAsync("localStorage.setItem", key, apiKey);
    }

    // Get API key for a specific provider
    public async Task<string?> GetApiKeyAsync(string provider)
    {
        var key = $"{API_KEY_PREFIX}{provider.ToLowerInvariant()}";
        return await _js.InvokeAsync<string?>("localStorage.getItem", key);
    }

    // Set model for a specific provider
    public async Task SetModelAsync(string provider, string model)
    {
        var key = $"{MODEL_PREFIX}{provider.ToLowerInvariant()}";
        await _js.InvokeVoidAsync("localStorage.setItem", key, model);
    }

    // Get model for a specific provider
    public async Task<string?> GetModelAsync(string provider)
    {
        var key = $"{MODEL_PREFIX}{provider.ToLowerInvariant()}";
        return await _js.InvokeAsync<string?>("localStorage.getItem", key);
    }

    // Set which provider is active
    public async Task SetActiveProviderAsync(string provider)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", ACTIVE_PROVIDER_KEY, provider.ToLowerInvariant());
    }

    // Get the currently active provider
    public async Task<string> GetActiveProviderAsync()
    {
        var active = await _js.InvokeAsync<string?>("localStorage.getItem", ACTIVE_PROVIDER_KEY);
        return active ?? "openai"; // Default to OpenAI if not set
    }

    // Get key and model for the active provider
    public async Task<(string? Key, string? Model)> GetActiveCredentialsAsync()
    {
        var provider = await GetActiveProviderAsync();
        var key = await GetApiKeyAsync(provider);
        var model = await GetModelAsync(provider);
        return (key, model);
    }

    // List all providers that have been configured (have a key)
    public async Task<List<string>> GetConfiguredProvidersAsync()
    {
        var providers = new List<string> { "openai", "anthropic", "google", "mistral" };
        var configured = new List<string>();

        foreach (var provider in providers)
        {
            var key = await GetApiKeyAsync(provider);
            if (!string.IsNullOrEmpty(key))
            {
                configured.Add(provider);
            }
        }

        return configured;
    }

    // Remove a provider's configuration
    public async Task ClearProviderAsync(string provider)
    {
        var keyKey = $"{API_KEY_PREFIX}{provider.ToLowerInvariant()}";
        var modelKey = $"{MODEL_PREFIX}{provider.ToLowerInvariant()}";

        await _js.InvokeVoidAsync("localStorage.removeItem", keyKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", modelKey);

        // If this was the active provider, switch to another
        var active = await GetActiveProviderAsync();
        if (active == provider.ToLowerInvariant())
        {
            var remaining = await GetConfiguredProvidersAsync();
            if (remaining.Any())
            {
                await SetActiveProviderAsync(remaining.First());
            }
            else
            {
                await _js.InvokeVoidAsync("localStorage.removeItem", ACTIVE_PROVIDER_KEY);
            }
        }
    }

    // Clear all configurations
    public async Task ClearAllAsync()
    {
        var providers = new[] { "openai", "anthropic", "google", "mistral" };
        foreach (var provider in providers)
        {
            await ClearProviderAsync(provider);
        }
    }

    // Migrate from old single-provider config to new multi-provider
    public async Task MigrateFromLegacyAsync(AiConfigStore legacyStore)
    {
        try
        {
            var legacyKey = await legacyStore.GetApiKeyAsync();
            var legacyModel = await legacyStore.GetModelAsync();
            var legacyProvider = await legacyStore.GetProviderAsync();

            if (!string.IsNullOrEmpty(legacyKey))
            {
                var providerName = legacyProvider switch
                {
                    ApiProvider.OpenAi => "openai",
                    ApiProvider.Anthropic => "anthropic",
                    ApiProvider.GoogleGemini => "google",
                    ApiProvider.Mistral => "mistral",
                    _ => "openai"
                };

                await SetApiKeyAsync(providerName, legacyKey);
                await SetModelAsync(providerName, legacyModel ?? "default");
                await SetActiveProviderAsync(providerName);
            }
        }
        catch
        {
            // Migration failed, legacy config remains unchanged
        }
    }
}
