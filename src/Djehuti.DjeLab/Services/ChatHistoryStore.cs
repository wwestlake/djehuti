using System.Text.Json;
using Microsoft.JSInterop;

namespace Djehuti.DjeLab.Services;

public sealed class ChatTurn
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}

/// <summary>
/// Persists the Chat pane's conversation to localStorage, same mechanism as
/// AiConfigStore and the same philosophy as Cyberscope's Live Lab keeping
/// djehuti.liveTurns -- the chat survives a reload rather than resetting
/// every time the workspace re-renders.
/// </summary>
public sealed class ChatHistoryStore
{
    private const string StorageKey = "djelab.chatTurns";

    private readonly IJSRuntime _js;

    public ChatHistoryStore(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<List<ChatTurn>> GetTurnsAsync()
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return new List<ChatTurn>();

        try
        {
            return JsonSerializer.Deserialize<List<ChatTurn>>(json) ?? new List<ChatTurn>();
        }
        catch (JsonException)
        {
            return new List<ChatTurn>();
        }
    }

    public async Task SaveTurnsAsync(List<ChatTurn> turns)
    {
        var json = JsonSerializer.Serialize(turns);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public async Task ClearAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }
}
