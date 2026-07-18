using System.Net.Http.Json;

namespace Djehuti.DjeLab.Services;

/// <summary>
/// Resolves the current DjeLab storage scope from the signed-in account.
/// Signed-in users get their own browser storage namespace; anonymous users
/// fall back to a shared anonymous namespace.
/// </summary>
public sealed class DjeLabStorageScopeService
{
    private const string AnonymousScope = "anon";

    private readonly HttpClient _http;

    public DjeLabStorageScopeService(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> GetScopeAsync()
    {
        try
        {
            var response = await _http.GetAsync("/djehuti/api/auth/me");
            if (!response.IsSuccessStatusCode)
                return AnonymousScope;

            var me = await response.Content.ReadFromJsonAsync<AuthMeResponse>();
            if (!string.IsNullOrWhiteSpace(me?.Id))
                return $"user-{NormalizeScopePart(me.Id)}";
        }
        catch
        {
            // Best effort only. If auth lookup fails, keep the app usable and
            // fall back to anonymous storage rather than blocking the UI.
        }

        return AnonymousScope;
    }

    public async Task<string> QualifyAsync(string storageKey)
    {
        var scope = await GetScopeAsync();
        return $"djelab.{scope}.{storageKey}";
    }

    private static string NormalizeScopePart(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? AnonymousScope
            : value.Trim().Replace(' ', '-').Replace(':', '-').Replace('/', '-').Replace('\\', '-');
    }

    private sealed class AuthMeResponse
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
    }
}
