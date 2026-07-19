using System.Net.Http.Json;

namespace Djehuti.Architect.Services;

/// <summary>
/// Shared sign-in state for the app -- one HTTP round trip to
/// /djehuti/api/auth/me, cached for every component that needs to know
/// "is someone signed in" (AuthCorner, MyModels, Editor) rather than each
/// page re-fetching it independently. Reuses the site's existing
/// djehuti_auth HttpOnly cookie, same as DjeLab/Teacher -- no new auth
/// system here.
/// </summary>
public sealed class AuthState(HttpClient http)
{
    public sealed class MeResponse
    {
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
    }

    public bool Loading { get; private set; } = true;
    public MeResponse? CurrentUser { get; private set; }
    public bool IsSignedIn => CurrentUser is not null;

    public event Action? Changed;

    private Task? _loadTask;

    public Task EnsureLoadedAsync() => _loadTask ??= LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            var response = await http.GetAsync("/djehuti/api/auth/me");
            if (response.IsSuccessStatusCode)
            {
                CurrentUser = await response.Content.ReadFromJsonAsync<MeResponse>();
            }
        }
        catch
        {
            // Failing to reach the API just means "treat as signed out."
        }
        finally
        {
            Loading = false;
            Changed?.Invoke();
        }
    }

    public async Task SignOutAsync()
    {
        try { await http.PostAsync("/djehuti/api/auth/logout", content: null); }
        catch { /* best effort */ }
        CurrentUser = null;
        Changed?.Invoke();
    }
}
