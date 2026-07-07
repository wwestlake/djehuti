using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

    public async Task<FilesResult<DjeLabFileEntry>> ResolvePathAsync(string path, CancellationToken ct = default)
    {
        var normalized = NormalizePath(path);
        if (normalized == "/")
            return FilesResult<DjeLabFileEntry>.Fail("The root folder does not map to a file.");

        var segments = normalized.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var currentPath = "/";
        DjeLabFileEntry? currentEntry = null;

        for (var i = 0; i < segments.Length; i++)
        {
            var entries = await ListFolderAsync(currentPath, ct);
            currentEntry = entries.FirstOrDefault(entry => string.Equals(entry.Name, segments[i], StringComparison.Ordinal));
            if (currentEntry is null)
                return FilesResult<DjeLabFileEntry>.Fail($"Could not find \"{normalized}\".");

            if (i < segments.Length - 1)
            {
                if (!currentEntry.IsFolder)
                    return FilesResult<DjeLabFileEntry>.Fail($"\"{currentEntry.Path}\" is not a folder.");
                currentPath = currentEntry.Path;
            }
        }

        if (currentEntry is null)
            return FilesResult<DjeLabFileEntry>.Fail($"Could not find \"{normalized}\".");

        return FilesResult<DjeLabFileEntry>.Ok(currentEntry);
    }

    public async Task<FilesResult<string>> ReadTextFileAsync(string path, long maxBytes = 1024 * 1024, CancellationToken ct = default)
    {
        var resolved = await ResolvePathAsync(path, ct);
        if (!resolved.Success || resolved.Value is null)
            return FilesResult<string>.Fail(resolved.Error ?? "Could not find that file.");

        if (resolved.Value.IsFolder)
            return FilesResult<string>.Fail("Folders cannot be read as files.");
        if (resolved.Value.SizeBytes is long size && size > maxBytes)
            return FilesResult<string>.Fail($"That file is too large to read safely here ({size} bytes).");

        var url = await GetDownloadUrlAsync(resolved.Value.Id, ct);
        if (string.IsNullOrWhiteSpace(url))
            return FilesResult<string>.Fail("Could not get a download link.");

        var text = await _http.GetStringAsync(url, ct);
        return FilesResult<string>.Ok(text);
    }

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

    public async Task<FilesResult<DjeLabFileEntry>> WriteTextFileAsync(
        string fullPath,
        string content,
        string contentType = "text/plain",
        CancellationToken ct = default)
    {
        var normalized = NormalizePath(fullPath);
        if (normalized == "/")
            return FilesResult<DjeLabFileEntry>.Fail("Choose a file path, not the root folder.");

        var lastSlash = normalized.LastIndexOf('/');
        var parentPath = lastSlash <= 0 ? "/" : normalized[..lastSlash];
        var filename = normalized[(lastSlash + 1)..];
        if (string.IsNullOrWhiteSpace(filename))
            return FilesResult<DjeLabFileEntry>.Fail("Choose a file name.");

        var existing = await ResolvePathAsync(normalized, ct);
        if (existing.Success && existing.Value is not null)
        {
            if (existing.Value.IsFolder)
                return FilesResult<DjeLabFileEntry>.Fail("That path is already a folder.");
            if (!await DeleteAsync(existing.Value.Id, ct))
                return FilesResult<DjeLabFileEntry>.Fail("Could not replace the existing file.");
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        var request = await RequestUploadUrlAsync(parentPath, filename, contentType, bytes.Length, ct);
        if (!request.Success || request.Value is null)
            return FilesResult<DjeLabFileEntry>.Fail(request.Error ?? "Could not request upload URL.");

        await using (var stream = new MemoryStream(bytes))
        {
            await UploadToPresignedUrlAsync(request.Value.PresignedUrl, stream, contentType, ct);
        }

        return await ConfirmUploadAsync(request.Value.FileId, parentPath, filename, contentType, ct);
    }

    private static string NormalizePath(string path)
    {
        var trimmed = (path ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return "/";
        trimmed = trimmed.Replace('\\', '/');
        if (!trimmed.StartsWith('/')) trimmed = "/" + trimmed;
        while (trimmed.Length > 1 && trimmed.EndsWith('/'))
            trimmed = trimmed[..^1];
        return trimmed;
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
