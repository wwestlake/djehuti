namespace Djehuti.Teacher.Services.Providers;

public interface IProviderDefinition
{
    string Name { get; }
    string Endpoint { get; }
    List<string> AvailableModels { get; }
    string DefaultModel { get; }

    void AddAuthHeaders(HttpRequestMessage request, string apiKey);
    Task<string?> ParseResponseAsync(HttpResponseMessage response);
    HttpRequestMessage BuildRequest(
        IEnumerable<(string Role, string Content)> messages,
        string model,
        string systemPrompt);
}
