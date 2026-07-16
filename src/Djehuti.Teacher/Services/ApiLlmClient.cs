using Djehuti.Teacher.Services.Providers;

namespace Djehuti.Teacher.Services;

public sealed class ApiLlmClientNew
{
    private readonly HttpClient _http;
    private readonly MultiProviderConfigStore _configStore;
    private readonly Dictionary<string, IProviderDefinition> _providers;

    public ApiLlmClientNew(
        HttpClient http,
        MultiProviderConfigStore configStore,
        Dictionary<string, IProviderDefinition> providers)
    {
        _http = http;
        _configStore = configStore;
        _providers = providers;
    }

    /// <summary>
    /// Chat using the currently active provider from config
    /// </summary>
    public async Task<string?> ChatAsync(
        IEnumerable<(string Role, string Content)> messages,
        string systemPrompt = "")
    {
        // Get active provider credentials
        var (apiKey, model) = await _configStore.GetActiveCredentialsAsync();

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            Console.WriteLine("ChatAsync: Missing API key or model");
            return null;
        }

        var providerName = await _configStore.GetActiveProviderAsync();
        if (!_providers.TryGetValue(providerName, out var provider))
        {
            Console.WriteLine($"ChatAsync: Provider {providerName} not found");
            return null;
        }

        Console.WriteLine($"ChatAsync: Using {providerName} provider, model: {model}");

        try
        {
            // Build and send request using provider definition
            var request = provider.BuildRequest(messages, model, systemPrompt);
            provider.AddAuthHeaders(request, apiKey);

            var response = await _http.SendAsync(request);
            var reply = await provider.ParseResponseAsync(response);

            if (reply != null)
            {
                Console.WriteLine($"{providerName} API succeeded");
            }
            else
            {
                Console.WriteLine($"{providerName} API returned null");
            }

            return reply;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{providerName} API exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Chat using a specific provider (for explicit selection)
    /// </summary>
    public async Task<string?> ChatAsync(
        IEnumerable<(string Role, string Content)> messages,
        string providerName,
        string apiKey,
        string model,
        string systemPrompt = "")
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            Console.WriteLine("ChatAsync: Missing API key or model");
            return null;
        }

        if (!_providers.TryGetValue(providerName.ToLowerInvariant(), out var provider))
        {
            Console.WriteLine($"ChatAsync: Provider {providerName} not found");
            return null;
        }

        Console.WriteLine($"ChatAsync: Using {providerName} provider, model: {model}");

        try
        {
            var request = provider.BuildRequest(messages, model, systemPrompt);
            provider.AddAuthHeaders(request, apiKey);

            var response = await _http.SendAsync(request);
            var reply = await provider.ParseResponseAsync(response);

            if (reply != null)
            {
                Console.WriteLine($"{providerName} API succeeded");
            }
            else
            {
                Console.WriteLine($"{providerName} API returned null");
            }

            return reply;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{providerName} API exception: {ex.Message}");
            return null;
        }
    }
}
