using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Djehuti.DjeLab.Services;

public sealed class DjeLabFileEntry
{
    public Guid Id { get; set; }
    public bool IsFolder { get; set; }
    public string Path { get; set; } = "";
    public string ParentPath { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class StorageUsage
{
    public long UsedBytes { get; set; }
    public long QuotaBytes { get; set; }
    public string TierName { get; set; } = "";
}

public readonly struct FilesResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public string? Error { get; }

    private FilesResult(bool success, T? value, string? error)
    {
        Success = success;
        Value = value;
        Error = error;
    }

    public static FilesResult<T> Ok(T value) => new(true, value, null);
    public static FilesResult<T> Fail(string error) => new(false, default, error);
}

/// <summary>
/// Calls DjeLab's S3-backed file manager endpoints (Djehuti.Api/
/// DjeLabFilesRepository.fs). Unlike AiChatClient, this goes through our own
/// backend for every operation, including the actual byte transfer to S3
/// (via a presigned URL the backend hands out after a quota check) --
/// these are private per-user files, not a BYOK provider call.
/// </summary>
public sealed class DjeLabFilesClient
{
    private const string Base = "/djehuti/api/djelab";

    private readonly HttpClient _http;

    public DjeLabFilesClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<StorageUsage> GetUsageAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<StorageUsage>($"{Base}/storage/usage", ct) ?? new StorageUsage();

    public async Task<List<DjeLabFileEntry>> ListFolderAsync(string path, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<DjeLabFileEntry>>($"{Base}/files?path={Uri.EscapeDataString(path)}", ct)
        ?? new List<DjeLabFileEntry>();

    public async Task<FilesResult<DjeLabFileEntry>> CreateFolderAsync(string parentPath, string name, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"{Base}/files/folder", new { parentPath, name }, ct);
        return await ReadResultAsync<DjeLabFileEntry>(response, ct);
    }

    public async Task<FilesResult<UploadUrlResponse>> RequestUploadUrlAsync(
        string parentPath, string filename, string contentType, long sizeBytes, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"{Base}/files/upload-url", new { parentPath, filename, contentType, sizeBytes }, ct);
        return await ReadResultAsync<UploadUrlResponse>(response, ct);
    }

    public async Task UploadToPresignedUrlAsync(string presignedUrl, Stream content, string contentType, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, presignedUrl);
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = streamContent;
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<FilesResult<DjeLabFileEntry>> ConfirmUploadAsync(
        Guid fileId, string parentPath, string filename, string contentType, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"{Base}/files/confirm", new { fileId = fileId.ToString(), parentPath, filename, contentType }, ct);
        return await ReadResultAsync<DjeLabFileEntry>(response, ct);
    }

    public async Task<string?> GetDownloadUrlAsync(Guid fileId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{Base}/files/{fileId}/download-url", ct);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadFromJsonAsync<UrlResponse>(cancellationToken: ct);
        return body?.Url;
    }

    public async Task<bool> DeleteAsync(Guid fileId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"{Base}/files/{fileId}", ct);
        return response.IsSuccessStatusCode;
    }

    private static async Task<FilesResult<T>> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            return FilesResult<T>.Ok(value!);
        }

        var raw = await response.Content.ReadAsStringAsync(ct);
        // Results.BadRequest(someString) on the F# side serializes as a bare
        // JSON string literal ("message here"), not { error: "..." } -- try
        // to unwrap that so the UI doesn't show literal quote characters.
        string message;
        try { message = JsonSerializer.Deserialize<string>(raw) ?? raw; }
        catch (JsonException) { message = raw; }
        return FilesResult<T>.Fail(string.IsNullOrWhiteSpace(message) ? response.ReasonPhrase ?? "Request failed" : message);
    }

    public sealed class UploadUrlResponse
    {
        public Guid FileId { get; set; }
        public string PresignedUrl { get; set; } = "";
        public string S3Key { get; set; } = "";
    }

    private sealed class UrlResponse
    {
        public string Url { get; set; } = "";
    }
}
