# Helper System Architecture

## Overview

The **Helper System** is a decision tree–based guidance engine that handles error conditions and routes users to solutions when the AI inference system (WebLLM) is unavailable.

Unlike passive error messages ("Sorry, tutor unavailable"), the Helper System **actively guides users** through setup options in plain language.

## Design Principles

1. **No technical jargon to users** — explain conditions clearly, without GPU/shader/API complexity
2. **Active participation** — guide users toward solutions, don't make them figure it out
3. **Declarative messaging** — both AI and Helper emit the same message format
4. **Extensible** — add new conditions/decisions without rewriting core logic
5. **Transparent** — users understand what happened and why they're being guided

## Architecture

### 1. Conditions (ICondition interface)

Each condition is a checkable state. Examples:

```csharp
public interface ICondition
{
    string Name { get; }
    Task<bool> EvaluateAsync();
}

// Implementations:
- WebLLMReadyCondition (Is WebLLM initialized and available?)
- WebLLMFailedCondition (Did WebLLM fail during inference?)
- OllamaRunningCondition (Is Ollama running on localhost:11434?)
- ApiKeyConfiguredCondition (Is an API key stored in localStorage?)
- NetworkErrorCondition (Did the request fail due to network?)
```

**Future conditions can be added:**
- `ServerMaintenanceCondition` — is the API down?
- `RateLimitedCondition` — did we hit rate limits?
- `InvalidApiKeyCondition` — is the stored key expired/invalid?

### 2. Decision Tree

The tree evaluates conditions in priority order and returns guidance:

```
Root
├─ If WebLLMReady
│  └─ Use WebLLM (attempt inference)
│
├─ Else if WebLLMFailed
│  ├─ If OllamaRunning
│  │  └─ Suggest Ollama (or auto-use if preferred)
│  │
│  ├─ Else if ApiKeyConfigured
│  │  └─ Use API key (transparent fallback)
│  │
│  └─ Else
│     └─ Guide: "Set up Ollama or add API key"
│
└─ Else if [Future Condition]
   └─ [Future Guidance]
```

### 3. Guidance Message Format

Both AI and Helper emit the same declarative format (JSON):

```csharp
public class GuidanceMessage
{
    public string Type { get; set; } // "inference", "guidance", "error"
    public string Content { get; set; } // Main message text
    public List<GuidanceAction> Actions { get; set; } // Buttons/links
    public string Tone { get; set; } // "helpful", "urgent", "neutral"
}

public class GuidanceAction
{
    public string Label { get; set; } // Button text
    public string ActionType { get; set; } // "navigate", "external", "setup"
    public string Target { get; set; } // URL or page path
    public string Description { get; set; } // Tooltip/explanation
}
```

**Example guidance response:**
```json
{
  "type": "guidance",
  "content": "I can't use the built-in helper right now. But I can help you set up either a local helper that runs free on your computer, or a cloud helper that you control with your own key.",
  "tone": "helpful",
  "actions": [
    {
      "label": "Set up Ollama (local, free)",
      "actionType": "navigate",
      "target": "/setup/ollama",
      "description": "Install and run a free helper on your computer"
    },
    {
      "label": "Add OpenAI Key (cloud, pay-as-you-go)",
      "actionType": "navigate",
      "target": "/setup/openai",
      "description": "Use your own API key for unlimited access"
    }
  ]
}
```

### 4. HelperSystem Class

Main decision engine that:
1. Collects conditions
2. Evaluates them in order
3. Returns appropriate guidance

```csharp
public class HelperSystem
{
    private List<ICondition> _conditions;
    private DecisionTree _decisionTree;

    public async Task<GuidanceMessage> EvaluateAsync()
    {
        // Evaluate all conditions
        var conditionResults = new Dictionary<string, bool>();
        foreach (var condition in _conditions)
        {
            conditionResults[condition.Name] = await condition.EvaluateAsync();
        }

        // Traverse tree based on results
        return _decisionTree.Traverse(conditionResults);
    }
}
```

## Integration with TutorChat

Current flow:
```
User sends message
  → Try WebLLM
    → If success: return response
    → If fail: return null
      → Try API key
        → If success: return response
        → If fail: return null
          → Show generic error "Sorry, tutor unavailable"
```

**New flow:**
```
User sends message
  → Try WebLLM
    → If success: return response
    → If fail:
      → Evaluate HelperSystem
        → If Ollama available: suggest/use it
        → Else if API key set: use it
        → Else: show setup guidance
```

## Extension Pattern: Adding a New Condition

When a new error scenario comes up:

1. **Create condition class:**
   ```csharp
   public class ServerMaintenanceCondition : ICondition
   {
       public string Name => "ServerMaintenance";
       public async Task<bool> EvaluateAsync()
       {
           // Check if API is returning 503/maintenance status
       }
   }
   ```

2. **Add to HelperSystem:**
   ```csharp
   _conditions.Add(new ServerMaintenanceCondition());
   ```

3. **Add decision node:**
   ```csharp
   if (conditionResults["ServerMaintenance"])
       return new GuidanceMessage
       {
           Content = "The AI service is temporarily down for maintenance...",
           Actions = new() { /* retry later, contact support, etc */ }
       };
   ```

No existing logic is touched.

## Phases

### Phase 1: Core Infrastructure
- [ ] `ICondition` interface
- [ ] `GuidanceMessage` / `GuidanceAction` classes
- [ ] Basic `HelperSystem` with decision tree
- [ ] Integrate with TutorChat
- [ ] Handle WebLLM failure scenario

### Phase 2: Guidance Pages
- [ ] `/setup/ollama` page with installation instructions
- [ ] `/setup/openai` page with API key setup wizard
- [ ] Link these from HelperSystem messages

### Phase 3: Future Scenarios
- [ ] Add conditions/guidance as new error types appear
- [ ] Examples: rate limiting, invalid keys, network errors

## Notes

- Conditions are evaluated at decision time, not pre-cached (ensures current state)
- Decision tree is deterministic (no randomness, same input = same output)
- Guidance messages use plain language, no jargon
- Can be tested independently of AI inference layer
