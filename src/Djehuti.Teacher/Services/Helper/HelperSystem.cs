namespace Djehuti.Teacher.Services.Helper;

public class HelperSystem
{
    private readonly List<ICondition> _conditions;

    public HelperSystem(
        WebLlmClient webLlm,
        AiConfigStore configStore,
        HttpClient http)
    {
        _conditions = new()
        {
            new WebLLMAvailableCondition(webLlm),
            new ApiKeyConfiguredCondition(configStore),
            new OllamaRunningCondition(http),
        };
    }

    public async Task<GuidanceMessage> EvaluateAsync()
    {
        var results = new Dictionary<string, bool>();
        foreach (var condition in _conditions)
        {
            results[condition.Name] = await condition.EvaluateAsync();
        }

        return TraverseDecisionTree(results);
    }

    private GuidanceMessage TraverseDecisionTree(Dictionary<string, bool> conditions)
    {
        // WebLLM is available - no guidance needed, caller should use WebLLM
        if (conditions.Get("WebLLMAvailable"))
        {
            return new GuidanceMessage
            {
                Type = "inference",
                Content = ""
            };
        }

        // WebLLM failed. Check fallback options in priority order.

        // Option 1: Ollama is running
        if (conditions.Get("OllamaRunning"))
        {
            return new GuidanceMessage
            {
                Type = "guidance",
                Content = "I can't use the built-in helper right now, but I found a local helper (Ollama) running on your machine. I can use that instead.",
                Tone = "helpful",
                Actions = new()
                {
                    new GuidanceAction
                    {
                        Label = "Use local helper",
                        ActionType = "setup",
                        Target = "use-ollama",
                        Description = "Connect to Ollama running on your machine"
                    }
                }
            };
        }

        // Option 2: API key is configured
        if (conditions.Get("ApiKeyConfigured"))
        {
            return new GuidanceMessage
            {
                Type = "guidance",
                Content = "I can't use the built-in helper right now, but I can use your API key that you set up in settings.",
                Tone = "helpful",
                Actions = new()
                {
                    new GuidanceAction
                    {
                        Label = "Use my API key",
                        ActionType = "setup",
                        Target = "use-api-key",
                        Description = "Use the API key you set up"
                    }
                }
            };
        }

        // Option 3: No fallback available - guide user through setup
        return new GuidanceMessage
        {
            Type = "guidance",
            Content = "I can't use the built-in helper right now. But I can help you set up either a local helper that runs free on your computer, or a cloud helper that you control with your own key.",
            Tone = "helpful",
            Actions = new()
            {
                new GuidanceAction
                {
                    Label = "Set up Ollama (local, free)",
                    ActionType = "navigate",
                    Target = "/setup/ollama",
                    Description = "Install and run a free helper on your computer"
                },
                new GuidanceAction
                {
                    Label = "Add OpenAI Key (cloud, pay-as-you-go)",
                    ActionType = "navigate",
                    Target = "/setup/openai",
                    Description = "Use your own API key for unlimited access"
                }
            }
        };
    }
}

// Helper extension for safe dictionary access
internal static class DictionaryExtensions
{
    public static bool Get(this Dictionary<string, bool> dict, string key)
    {
        return dict.TryGetValue(key, out var value) ? value : false;
    }
}
