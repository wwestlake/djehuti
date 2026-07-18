namespace Djehuti.Teacher.Services.Helper;

public class WebLLMAvailableCondition : ICondition
{
    private readonly WebLlmInterop _webLlm;

    public string Name => "WebLLMAvailable";

    public WebLLMAvailableCondition(WebLlmInterop webLlm)
    {
        _webLlm = webLlm;
    }

    public Task<bool> EvaluateAsync()
    {
        return Task.FromResult(_webLlm.State == WebLlmState.Ready);
    }
}

public class ApiKeyConfiguredCondition : ICondition
{
    private readonly AiConfigStore _configStore;

    public string Name => "ApiKeyConfigured";

    public ApiKeyConfiguredCondition(AiConfigStore configStore)
    {
        _configStore = configStore;
    }

    public async Task<bool> EvaluateAsync()
    {
        var apiKey = await _configStore.GetApiKeyAsync();
        var model = await _configStore.GetModelAsync();
        return !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(model);
    }
}

public class OllamaRunningCondition : ICondition
{
    private readonly HttpClient _http;

    public string Name => "OllamaRunning";

    public OllamaRunningCondition(HttpClient http)
    {
        _http = http;
    }

    public async Task<bool> EvaluateAsync()
    {
        try
        {
            var response = await _http.GetAsync("http://localhost:11434/api/tags", HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
