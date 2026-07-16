# Multi-Provider API Key Management

## Overview

Users can add and manage API keys from multiple AI providers (OpenAI, Anthropic, Google, Mistral) and select which provider to use for inference. The system handles different API formats and authentication methods transparently.

## Design Principles

1. **Multiple keys simultaneously** — Users can store keys for several providers at once
2. **One active provider** — Only one provider is used per inference call
3. **Provider abstraction** — Each provider encapsulates its own API format/auth
4. **Easy switching** — Users can change providers mid-session via UI selector
5. **Persistent storage** — Selected provider and all keys survive page reloads

## Architecture

### 1. Provider Definitions

Each provider is defined once with:
- Display name
- Available models
- API endpoint
- Authentication method (header, query param, etc.)
- Request/response format

```csharp
public interface IProviderDefinition
{
    string Name { get; }
    string Endpoint { get; }
    List<string> AvailableModels { get; }
    string DefaultModel { get; }
    void AddAuthHeaders(HttpRequestMessage request, string apiKey);
    Task<string?> ParseResponseAsync(HttpResponseMessage response);
}

// Implementations:
- OpenAiProvider
- AnthropicProvider
- GoogleGeminiProvider
- MistralProvider
```

### 2. Multi-Provider Config Store

Stores all keys and the active provider:

```csharp
public class MultiProviderConfigStore
{
    // Store keys for each provider
    public async Task SetApiKeyAsync(string provider, string apiKey);
    public async Task<string?> GetApiKeyAsync(string provider);
    
    // Store model per provider
    public async Task SetModelAsync(string provider, string model);
    public async Task<string?> GetModelAsync(string provider);
    
    // Active provider selection
    public async Task SetActiveProviderAsync(string provider);
    public async Task<string> GetActiveProviderAsync();
    
    // Convenience: get active provider's key/model
    public async Task<(string? key, string? model)> GetActiveCredentialsAsync();
    
    // List all configured providers
    public async Task<List<string>> GetConfiguredProvidersAsync();
}
```

**Storage (localStorage):**
```
teacher.apiKey.openai = "sk-..."
teacher.model.openai = "gpt-4o-mini"
teacher.apiKey.anthropic = "sk-ant-..."
teacher.model.anthropic = "claude-3-5-sonnet"
teacher.activeProvider = "openai"
```

### 3. AI Setup Page (Redesigned)

Tabs for each provider:
- OpenAI tab: API key input, model selector
- Anthropic tab: API key input, model selector
- Google Gemini tab: API key input, model selector
- Mistral tab: API key input, model selector

**UI pattern:**
```
[OpenAI] [Anthropic] [Google] [Mistral]

OpenAI Tab Content:
  API Key: [________] [show]
  Model: [gpt-4o-mini ▼]
  [Save] [Remove]
  
  Status: Configured ✓ (Active provider: ✓ radio button)
```

Each tab:
1. Shows if that provider is configured
2. Shows if it's the active provider
3. Allow adding/updating key
4. Allow removing key
5. Allow setting as active

### 4. Provider Selector in Chat UI

Shows in tutor header/status area:

```
🟢 Tutor (WebLLM) [Using: OpenAI ▼]
```

Clicking dropdown:
```
[ ] WebLLM (local)
[x] OpenAI (api key configured)
[ ] Anthropic (api key configured)
[ ] Ollama (not running)
```

Click to switch providers in real-time.

### 5. API Client Abstraction

Updated `ApiLlmClient` to use provider definitions:

```csharp
public class ApiLlmClient
{
    private Dictionary<string, IProviderDefinition> _providers;
    private MultiProviderConfigStore _configStore;
    
    public async Task<string?> ChatAsync(
        List<(string Role, string Content)> messages)
    {
        // Get active provider
        var providerName = await _configStore.GetActiveProviderAsync();
        var provider = _providers[providerName];
        
        // Get credentials
        var (key, model) = await _configStore.GetActiveCredentialsAsync();
        
        // Call provider's API
        var request = provider.BuildRequest(messages, model);
        provider.AddAuthHeaders(request, key);
        
        var response = await _http.SendAsync(request);
        return await provider.ParseResponseAsync(response);
    }
}
```

## Implementation Phases

### Phase 1: Infrastructure (Current)
- [ ] `IProviderDefinition` interface
- [ ] Provider implementations (OpenAI, Anthropic, Google, Mistral)
- [ ] `MultiProviderConfigStore` with localStorage
- [ ] Update `ApiLlmClient` to use abstractions
- [ ] Update `Program.cs` to register providers

### Phase 2: AI Setup UI
- [ ] Redesign AiSetup with provider tabs
- [ ] Active provider selector in tabs
- [ ] Add/Remove buttons per provider
- [ ] Show configuration status

### Phase 3: Chat UI Integration
- [ ] Provider selector in tutor header
- [ ] Real-time switching
- [ ] Fallback chain: WebLLM → API (active provider) → Ollama → Helper guidance

### Phase 4: Migration from Old Config
- [ ] Migrate existing single-provider config to new multi-provider store
- [ ] Set migrated provider as active by default

## Supported Providers

| Provider | Endpoint | Auth | Models |
|----------|----------|------|--------|
| OpenAI | `https://api.openai.com/v1/chat/completions` | Bearer token | gpt-4o, gpt-4o-mini, etc. |
| Anthropic | `https://api.anthropic.com/v1/messages` | x-api-key header | claude-3-5-sonnet, claude-3-5-haiku, etc. |
| Google Gemini | `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent` | API key param | gemini-1.5-pro, gemini-1.5-flash, etc. |
| Mistral | `https://api.mistral.ai/v1/chat/completions` | Bearer token | mistral-large, mistral-small, etc. |

## Future Extensions

To add a new provider:
1. Create a class implementing `IProviderDefinition`
2. Define endpoint, auth, models
3. Implement `BuildRequest()` and `ParseResponse()`
4. Register in `Program.cs`
5. UI automatically shows new tab in AiSetup

No changes needed to core inference logic.

## Notes

- Active provider persists across sessions (stored in localStorage)
- All provider keys are stored client-side only (never sent to Djehuti servers)
- Users can have keys from all providers configured simultaneously
- Switching providers happens instantly, no page reload needed
- If active provider's key is deleted, system falls back to next available or shows guidance
