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
    public const string LegacyStorageKey = "djelab.chatTurns";

    private readonly IJSRuntime _js;
    private readonly DjeLabStorageScopeService _scope;

    public ChatHistoryStore(IJSRuntime js, DjeLabStorageScopeService scope)
    {
        _js = js;
        _scope = scope;
    }

    public async Task<List<ChatTurn>> GetTurnsAsync(string storageKey, string? legacyStorageKey = null)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return new List<ChatTurn>();

        var scopedKey = await _scope.QualifyAsync(storageKey);
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", scopedKey);
        if (string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(legacyStorageKey))
        {
            var legacyJson = await _js.InvokeAsync<string?>("localStorage.getItem", legacyStorageKey);
            if (!string.IsNullOrWhiteSpace(legacyJson))
            {
                json = legacyJson;
                await _js.InvokeVoidAsync("localStorage.setItem", scopedKey, legacyJson);
                await _js.InvokeVoidAsync("localStorage.removeItem", legacyStorageKey);
            }
        }

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

    public async Task SaveTurnsAsync(string storageKey, List<ChatTurn> turns)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return;

        var json = JsonSerializer.Serialize(turns);
        var scopedKey = await _scope.QualifyAsync(storageKey);
        await _js.InvokeVoidAsync("localStorage.setItem", scopedKey, json);
    }

    public async Task ClearAsync(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return;

        var scopedKey = await _scope.QualifyAsync(storageKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", scopedKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", storageKey);
    }
}
