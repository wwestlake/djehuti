using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Djehuti.Core;

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
    private const long DefaultPreviewBytes = 128 * 1024;

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
            currentEntry = FindEntry(entries, segments[i]);
            if (currentEntry is null)
                return FilesResult<DjeLabFileEntry>.Fail(BuildNotFoundMessage(normalized, segments[i], entries));

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

    private static DjeLabFileEntry? FindEntry(IReadOnlyList<DjeLabFileEntry> entries, string segment)
    {
        var exact = entries.FirstOrDefault(entry => string.Equals(entry.Name, segment, StringComparison.Ordinal));
        if (exact is not null)
            return exact;

        var caseInsensitive = entries.FirstOrDefault(entry => string.Equals(entry.Name, segment, StringComparison.OrdinalIgnoreCase));
        if (caseInsensitive is not null)
            return caseInsensitive;

        var requestedStem = Path.GetFileNameWithoutExtension(segment);
        var stemMatch = entries.FirstOrDefault(entry =>
            string.Equals(Path.GetFileNameWithoutExtension(entry.Name), requestedStem, StringComparison.OrdinalIgnoreCase));
        if (stemMatch is not null)
            return stemMatch;

        if (!segment.Contains('.'))
        {
            var prefixMatch = entries.FirstOrDefault(entry =>
                Path.GetFileNameWithoutExtension(entry.Name).StartsWith(segment, StringComparison.OrdinalIgnoreCase));
            if (prefixMatch is not null)
                return prefixMatch;
        }

        return null;
    }

    private static string BuildNotFoundMessage(string normalized, string segment, IReadOnlyList<DjeLabFileEntry> entries)
    {
        var suggestions =
            entries
                .Where(entry =>
                    entry.Name.Contains(segment, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileNameWithoutExtension(entry.Name).Equals(Path.GetFileNameWithoutExtension(segment), StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Path)
                .Take(5)
                .ToArray();

        if (suggestions.Length == 0)
            return $"Could not find \"{normalized}\".";

        return $"Could not find \"{normalized}\". Closest matches: {string.Join(", ", suggestions)}.";
    }

    public async Task<FilesResult<string>> ReadTextFileAsync(string path, long maxBytes = DefaultPreviewBytes, CancellationToken ct = default)
    {
        var preview = await ReadTextPreviewAsync(path, maxBytes, ct);
        if (!preview.Success || preview.Value is null)
            return FilesResult<string>.Fail(preview.Error ?? "Could not read that file.");

        return FilesResult<string>.Ok(preview.Value.Content);
    }

    public async Task<FilesResult<TextPreviewResult>> ReadTextPreviewAsync(string path, long maxBytes = DefaultPreviewBytes, CancellationToken ct = default)
    {
        var resolved = await ResolvePathAsync(path, ct);
        if (!resolved.Success || resolved.Value is null)
            return FilesResult<TextPreviewResult>.Fail(resolved.Error ?? "Could not find that file.");

        if (resolved.Value.IsFolder)
            return FilesResult<TextPreviewResult>.Fail("Folders cannot be read as files.");

        var url = await GetDownloadUrlAsync(resolved.Value.Id, ct);
        if (string.IsNullOrWhiteSpace(url))
            return FilesResult<TextPreviewResult>.Fail("Could not get a download link.");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            return FilesResult<TextPreviewResult>.Fail($"Could not read the file content (HTTP {(int)response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var scratch = new byte[8192];
        long totalRead = 0;
        while (totalRead < maxBytes)
        {
            var toRead = (int)Math.Min(scratch.Length, maxBytes - totalRead);
            var read = await stream.ReadAsync(scratch.AsMemory(0, toRead), ct);
            if (read <= 0)
                break;

            buffer.Write(scratch, 0, read);
            totalRead += read;
        }

        var text = Encoding.UTF8.GetString(buffer.ToArray());
        var truncated = resolved.Value.SizeBytes is long size ? size > totalRead : totalRead >= maxBytes;
        return FilesResult<TextPreviewResult>.Ok(new TextPreviewResult(text, truncated, totalRead));
    }

    public async Task<FilesResult<SourceBundleResult>> BundleProjectAsync(string path, CancellationToken ct = default)
    {
        var entry = await ResolveProjectEntryAsync(path, ct);
        if (!entry.Success || entry.Value is null)
            return FilesResult<SourceBundleResult>.Fail(entry.Error ?? "Could not resolve a project entry file.");

        var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var stack = new Stack<string>();

        var expanded = await BundleFileAsync(entry.Value.Path, included, warnings, stack, ct);
        if (!expanded.Success)
            return FilesResult<SourceBundleResult>.Fail(expanded.Error ?? "Could not bundle that project.");

        return FilesResult<SourceBundleResult>.Ok(new SourceBundleResult
        {
            EntryPath = path,
            ResolvedEntryPath = entry.Value.Path,
            IncludedFiles = included.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            Warnings = warnings.ToArray(),
            ExpandedSource = expanded.Value.ExpandedSource,
            TotalBytes = expanded.Value.TotalBytes
        });
    }

    public async Task<FilesResult<string>> ReadStructuredDataAsync(string path, CancellationToken ct = default)
    {
        var resolved = await ResolvePathAsync(path, ct);
        if (!resolved.Success || resolved.Value is null)
            return FilesResult<string>.Fail(resolved.Error ?? "Could not find that file.");

        if (resolved.Value.IsFolder)
        {
            var folderTree = await BuildFolderTreeAsync(resolved.Value, ct);
            return FilesResult<string>.Ok(JsonSerializer.Serialize(folderTree));
        }

        var contentType = (resolved.Value.ContentType ?? "").ToLowerInvariant();
        var extension = Path.GetExtension(resolved.Value.Name).ToLowerInvariant();

        if (contentType.Contains("json") || extension == ".json")
        {
            var rawJson = await ReadTextPreviewAsync(path, ct: ct);
            if (!rawJson.Success || rawJson.Value is null)
                return FilesResult<string>.Fail(rawJson.Error ?? "Could not read JSON content.");

            if (!rawJson.Value.Truncated)
            {
                var document = HierarchicalData.fromJsonText(resolved.Value.Name, rawJson.Value.Content);
                var treeJson = JsonSerializer.Serialize(HierarchicalData.toSerializable(document.Root));
                await StoreHierarchySnapshotAsync(resolved.Value.Id, document.SourceKind, treeJson, ct);

                using var treeDoc = JsonDocument.Parse(treeJson);
                var treeStats = HierarchicalData.summarize(document.Root);
                var structured = new
                {
                    kind = "json",
                    name = resolved.Value.Name,
                    truncated = false,
                    previewBytes = rawJson.Value.BytesRead,
                    nodeCount = treeStats.NodeCount,
                    leafCount = treeStats.LeafCount,
                    maxDepth = treeStats.MaxDepth,
                    tree = treeDoc.RootElement.Clone()
                };

                return FilesResult<string>.Ok(JsonSerializer.Serialize(structured));
            }

            var previewStructured = new
            {
                kind = "json-preview",
                name = resolved.Value.Name,
                truncated = true,
                previewBytes = rawJson.Value.BytesRead,
                preview = rawJson.Value.Content,
                note = "Large JSON files are previewed here so the AI can inspect the top of the file without loading the whole thing."
            };

            return FilesResult<string>.Ok(JsonSerializer.Serialize(previewStructured));
        }

        if (contentType.Contains("csv") || extension == ".csv")
        {
            var rawCsv = await ReadTextPreviewAsync(path, ct: ct);
            if (!rawCsv.Success || rawCsv.Value is null)
                return FilesResult<string>.Fail(rawCsv.Error ?? "Could not read CSV content.");

            var parsed = CsvText.parse(rawCsv.Value.Content);
            var csvTree = HierarchicalData.fromCsv(resolved.Value.Name, parsed.Headers, parsed.Rows);
            var treeJson = JsonSerializer.Serialize(HierarchicalData.toSerializable(csvTree.Root));
            await StoreHierarchySnapshotAsync(resolved.Value.Id, csvTree.SourceKind, treeJson, ct);

            using var treeDoc = JsonDocument.Parse(treeJson);
            var headers = parsed.Headers.ToArray();
            var rows = parsed.Rows.Select(row => row.ToArray()).ToArray();
            var columnProfiles = BuildCsvColumnProfiles(headers, rows);
            var treeStats = HierarchicalData.summarize(csvTree.Root);
            var structured = new
            {
                kind = "csv",
                name = resolved.Value.Name,
                truncated = rawCsv.Value.Truncated,
                previewBytes = rawCsv.Value.BytesRead,
                headers,
                rows,
                rowCount = rows.Length,
                columnCount = headers.Length,
                columnProfiles,
                nodeCount = treeStats.NodeCount,
                leafCount = treeStats.LeafCount,
                maxDepth = treeStats.MaxDepth,
                tree = treeDoc.RootElement.Clone()
            };

            return FilesResult<string>.Ok(JsonSerializer.Serialize(structured));
        }

        if (extension == ".root" || contentType.Contains("root"))
        {
            var manifest = await TryReadRootManifestAsync(resolved.Value, ct);
            if (manifest.Success && manifest.Value is not null)
            {
                var document = HierarchicalData.fromRootManifest(resolved.Value.Name, manifest.Value.Value);
                var manifestTreeJson = JsonSerializer.Serialize(HierarchicalData.toSerializable(document.Root));
                await StoreHierarchySnapshotAsync(resolved.Value.Id, document.SourceKind, manifestTreeJson, ct);

                using var manifestTreeDoc = JsonDocument.Parse(manifestTreeJson);
                var treeStats = HierarchicalData.summarize(document.Root);
                var manifestStructured = new
                {
                    kind = "root-manifest",
                    name = resolved.Value.Name,
                    manifestPath = manifest.ManifestPath,
                    nodeCount = treeStats.NodeCount,
                    leafCount = treeStats.LeafCount,
                    maxDepth = treeStats.MaxDepth,
                    tree = manifestTreeDoc.RootElement.Clone()
                };

                return FilesResult<string>.Ok(JsonSerializer.Serialize(manifestStructured));
            }

            var rootTreeJson = JsonSerializer.Serialize(new
            {
                name = resolved.Value.Name,
                kind = "root-file",
                path = resolved.Value.Path,
                sizeBytes = resolved.Value.SizeBytes,
                contentType = resolved.Value.ContentType,
                note = "Binary ROOT parsing is not wired in yet. Add a companion .manifest.json or .root.json file to describe the hierarchy."
            });
            await StoreHierarchySnapshotAsync(resolved.Value.Id, "root-file", rootTreeJson, ct);

            using var rootTreeDoc = JsonDocument.Parse(rootTreeJson);
            var rootStructured = new
            {
                kind = "root-file",
                name = resolved.Value.Name,
                sizeBytes = resolved.Value.SizeBytes,
                contentType = resolved.Value.ContentType,
                nodeCount = 1,
                leafCount = 1,
                maxDepth = 0,
                tree = rootTreeDoc.RootElement.Clone()
            };

            return FilesResult<string>.Ok(JsonSerializer.Serialize(rootStructured));
        }

        var raw = await ReadTextFileAsync(path, ct: ct);
        if (!raw.Success || raw.Value is null)
            return FilesResult<string>.Fail(raw.Error ?? "Could not read that file.");

        return FilesResult<string>.Ok(JsonSerializer.Serialize(new
        {
            name = resolved.Value.Name,
            kind = "text",
            path = resolved.Value.Path,
            sizeBytes = resolved.Value.SizeBytes,
            truncated = resolved.Value.SizeBytes is long size && size > raw.Value.Length,
            preview = raw.Value.Length > 5000 ? raw.Value[..5000] + "\n..." : raw.Value
        }));
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

    public async Task<bool> StoreHierarchySnapshotAsync(
        Guid fileId, string sourceKind, string treeJson, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"{Base}/files/{fileId}/hierarchy",
            new { sourceKind, treeJson },
            ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<HierarchicalNodeRecord>> GetHierarchyNodesAsync(Guid fileId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<HierarchicalNodeRecord>>($"{Base}/files/{fileId}/hierarchy/nodes", ct)
        ?? new List<HierarchicalNodeRecord>();

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

    private async Task<object> BuildFolderTreeAsync(DjeLabFileEntry folder, CancellationToken ct)
    {
        var entries = await ListFolderAsync(folder.Path, ct);
        var children = new List<object>();

        foreach (var entry in entries)
        {
            if (entry.IsFolder)
            {
                children.Add(await BuildFolderTreeAsync(entry, ct));
            }
            else
            {
                children.Add(new
                {
                    name = entry.Name,
                    kind = "file",
                    path = entry.Path,
                    contentType = entry.ContentType,
                    sizeBytes = entry.SizeBytes
                });
            }
        }

        return new
        {
            name = folder.Name,
            kind = "folder",
            path = folder.Path,
            children
        };
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

    public sealed class SourceBundleResult
    {
        public string EntryPath { get; set; } = "";
        public string ResolvedEntryPath { get; set; } = "";
        public string ExpandedSource { get; set; } = "";
        public string[] IncludedFiles { get; set; } = Array.Empty<string>();
        public string[] Warnings { get; set; } = Array.Empty<string>();
        public long TotalBytes { get; set; }
    }

    private sealed class UrlResponse
    {
        public string Url { get; set; } = "";
    }

    private sealed record CsvColumnProfile(
        string Name,
        string Kind,
        int NonEmptyCount,
        int ParsedNumericCount,
        int ParsedBooleanCount,
        string[] SampleValues,
        double? Minimum,
        double? Maximum,
        double? Mean);

    public sealed record TextPreviewResult(string Content, bool Truncated, long BytesRead);

    public sealed class HierarchicalNodeRecord
    {
        public Guid Id { get; set; }
        public Guid SnapshotId { get; set; }
        public Guid? ParentId { get; set; }
        public string NodePath { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public string? ValueText { get; set; }
        public string MetadataJson { get; set; } = "{}";
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class RootManifestReadResult
    {
        public bool Success { get; init; }
        public JsonElement? Value { get; init; }
        public string? ManifestPath { get; init; }
    }

    private async Task<RootManifestReadResult> TryReadRootManifestAsync(DjeLabFileEntry rootFile, CancellationToken ct)
    {
        foreach (var candidate in BuildRootManifestCandidates(rootFile))
        {
            var manifest = await ReadTextFileAsync(candidate, ct: ct);
            if (!manifest.Success || manifest.Value is null)
                continue;

            try
            {
                using var document = JsonDocument.Parse(manifest.Value);
                return new RootManifestReadResult
                {
                    Success = true,
                    Value = document.RootElement.Clone(),
                    ManifestPath = candidate
                };
            }
            catch (JsonException)
            {
                // Keep looking; a non-JSON sibling is not a hierarchy manifest.
            }
        }

        return new RootManifestReadResult { Success = false };
    }

    private async Task<FilesResult<DjeLabFileEntry>> ResolveProjectEntryAsync(string path, CancellationToken ct)
    {
        var resolved = await ResolvePathAsync(path, ct);
        if (!resolved.Success || resolved.Value is null)
            return FilesResult<DjeLabFileEntry>.Fail(resolved.Error ?? "Could not resolve that project path.");

        if (!resolved.Value.IsFolder)
            return resolved;

        var entries = await ListFolderAsync(resolved.Value.Path, ct);
        var sourceEntries = entries
            .Where(entry => !entry.IsFolder && IsSourceFile(entry.Name))
            .ToList();

        var entryCandidates =
            new[]
            {
                "main.spi", "main.spinoza", "main.spz",
                "index.spi", "index.spinoza", "index.spz",
                $"{resolved.Value.Name}.spi", $"{resolved.Value.Name}.spinoza", $"{resolved.Value.Name}.spz"
            }
            .Select(candidate => sourceEntries.FirstOrDefault(entry => string.Equals(entry.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            .Where(entry => entry is not null)
            .ToList();

        if (entryCandidates.Count > 0)
            return FilesResult<DjeLabFileEntry>.Ok(entryCandidates[0]!);

        if (sourceEntries.Count == 1)
            return FilesResult<DjeLabFileEntry>.Ok(sourceEntries[0]);

        if (sourceEntries.Count == 0)
        {
            return FilesResult<DjeLabFileEntry>.Fail(
                $"No Spinoza source file was found in \"{resolved.Value.Path}\". Add a `main.spi`, `index.spi`, or similarly named source file.");
        }

        var names = string.Join(", ", sourceEntries.Take(8).Select(entry => entry.Name));
        return FilesResult<DjeLabFileEntry>.Fail(
            $"Multiple source files were found in \"{resolved.Value.Path}\" ({names}). Choose a single entry file to bundle.");
    }

    private static bool IsSourceFile(string name)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        return extension is ".spi" or ".spinoza" or ".spz" or ".djl";
    }

    private async Task<FilesResult<(string ExpandedSource, long TotalBytes)>> BundleFileAsync(
        string path,
        HashSet<string> included,
        List<string> warnings,
        Stack<string> stack,
        CancellationToken ct)
    {
        var resolved = await ResolvePathAsync(path, ct);
        if (!resolved.Success || resolved.Value is null)
            return FilesResult<(string ExpandedSource, long TotalBytes)>.Fail(resolved.Error ?? $"Could not resolve \"{path}\".");

        if (resolved.Value.IsFolder)
            return FilesResult<(string ExpandedSource, long TotalBytes)>.Fail($"\"{resolved.Value.Path}\" is a folder, not a source file.");

        if (stack.Any(value => string.Equals(value, resolved.Value.Path, StringComparison.OrdinalIgnoreCase)))
        {
            var cycle = string.Join(" -> ", stack.Reverse().Concat(new[] { resolved.Value.Path }));
            return FilesResult<(string ExpandedSource, long TotalBytes)>.Fail($"Import cycle detected: {cycle}");
        }

        if (!included.Add(resolved.Value.Path))
            return FilesResult<(string ExpandedSource, long TotalBytes)>.Ok(("", 0));

        stack.Push(resolved.Value.Path);
        try
        {
            var text = await ReadTextPreviewAsync(resolved.Value.Path, maxBytes: 512 * 1024, ct);
            if (!text.Success || text.Value is null)
                return FilesResult<(string ExpandedSource, long TotalBytes)>.Fail(text.Error ?? $"Could not read \"{resolved.Value.Path}\".");

            if (text.Value.Truncated)
            {
                warnings.Add($"Bundling stopped early because \"{resolved.Value.Path}\" exceeded the 512 KiB source limit.");
                return FilesResult<(string ExpandedSource, long TotalBytes)>.Fail(
                    $"\"{resolved.Value.Path}\" is too large to bundle safely. Keep library files smaller than 512 KiB.");
            }

            var lines = text.Value.Content.Replace("\r\n", "\n").Split('\n');
            var importedSources = new List<string>();
            var body = new StringBuilder();
            var totalBytes = text.Value.BytesRead;

            foreach (var line in lines)
            {
                if (TryParseImportDirective(line, out var importedPath))
                {
                    var resolvedImport = await ResolveImportedPathAsync(resolved.Value.Path, importedPath, ct);
                    if (!resolvedImport.Success || resolvedImport.Value is null)
                        return FilesResult<(string ExpandedSource, long TotalBytes)>.Fail(resolvedImport.Error ?? $"Could not resolve import \"{importedPath}\".");

                    var child = await BundleFileAsync(resolvedImport.Value.Path, included, warnings, stack, ct);
                    if (!child.Success)
                        return FilesResult<(string ExpandedSource, long TotalBytes)>.Fail(child.Error ?? $"Could not bundle import \"{importedPath}\".");

                    if (!string.IsNullOrWhiteSpace(child.Value.ExpandedSource))
                        importedSources.Add(child.Value.ExpandedSource.TrimEnd());

                    totalBytes += child.Value.TotalBytes;
                    continue;
                }

                body.AppendLine(line);
            }

            var expandedParts = new List<string>(importedSources.Count + 1);
            expandedParts.AddRange(importedSources);
            expandedParts.Add(body.ToString().TrimStart());
            var expanded = string.Join(Environment.NewLine + Environment.NewLine, expandedParts.Where(part => !string.IsNullOrWhiteSpace(part)));

            return FilesResult<(string ExpandedSource, long TotalBytes)>.Ok((expanded, totalBytes));
        }
        finally
        {
            stack.Pop();
        }
    }

    private async Task<FilesResult<DjeLabFileEntry>> ResolveImportedPathAsync(string currentPath, string importedPath, CancellationToken ct)
    {
        var normalized = NormalizePath(importedPath);
        if (string.IsNullOrWhiteSpace(normalized))
            return FilesResult<DjeLabFileEntry>.Fail("Import path was empty.");

        if (normalized != "/" && normalized.Contains('*'))
            return FilesResult<DjeLabFileEntry>.Fail($"Wildcard imports are not supported: \"{importedPath}\".");

        var candidates = BuildImportCandidates(currentPath, normalized);
        foreach (var candidate in candidates)
        {
            var resolved = await ResolvePathAsync(candidate, ct);
            if (resolved.Success && resolved.Value is not null && !resolved.Value.IsFolder)
                return resolved;
        }

        return FilesResult<DjeLabFileEntry>.Fail(
            $"Could not resolve import \"{importedPath}\" from \"{currentPath}\". Tried: {string.Join(", ", candidates)}");
    }

    private static IReadOnlyList<string> BuildImportCandidates(string currentPath, string importedPath)
    {
        var candidates = new List<string>();
        var parent = GetParentPath(currentPath);
        var stem = Path.GetFileNameWithoutExtension(importedPath);
        var extension = Path.GetExtension(importedPath);

        if (importedPath.StartsWith("/"))
        {
            candidates.Add(importedPath);
        }
        else
        {
            candidates.Add(CombinePaths(parent, importedPath));
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            foreach (var ext in new[] { ".spi", ".spinoza", ".spz", ".djl" })
            {
                candidates.Add(CombinePaths(parent, importedPath + ext));
                if (importedPath.StartsWith("/"))
                    candidates.Add(importedPath + ext);
            }
        }

        candidates.Add(CombinePaths(parent, stem));
        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetParentPath(string path)
    {
        var normalized = NormalizePath(path);
        if (normalized == "/") return "/";
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : normalized[..lastSlash];
    }

    private static string CombinePaths(string left, string right)
    {
        var normalizedLeft = NormalizePath(left);
        var normalizedRight = (right ?? "").Replace('\\', '/').Trim();

        if (string.IsNullOrWhiteSpace(normalizedRight))
            return normalizedLeft;

        if (normalizedRight.StartsWith('/'))
            return NormalizePath(normalizedRight);

        if (normalizedLeft == "/")
            return NormalizePath("/" + normalizedRight);

        return NormalizePath($"{normalizedLeft}/{normalizedRight}");
    }

    private static bool TryParseImportDirective(string line, out string importedPath)
    {
        importedPath = "";
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return false;

        var keyword =
            trimmed.StartsWith("import ", StringComparison.OrdinalIgnoreCase) ? "import" :
            trimmed.StartsWith("include ", StringComparison.OrdinalIgnoreCase) ? "include" :
            null;

        if (keyword is null)
            return false;

        var remainder = trimmed[keyword.Length..].TrimStart();
        if (remainder.Length == 0)
            return false;

        if (remainder[0] == '"')
        {
            var closing = remainder.IndexOf('"', 1);
            if (closing <= 1)
                return false;

            importedPath = remainder[1..closing];
            return !string.IsNullOrWhiteSpace(importedPath);
        }

        var token = remainder.Split(new[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        importedPath = token;
        return true;
    }

    private IReadOnlyList<string> BuildRootManifestCandidates(DjeLabFileEntry rootFile)
    {
        var directory = rootFile.ParentPath;
        var stem = Path.GetFileNameWithoutExtension(rootFile.Name);
        return new List<string>
        {
            NormalizePath(Path.Combine(directory, $"{rootFile.Name}.manifest.json")),
            NormalizePath(Path.Combine(directory, $"{stem}.root.json")),
            NormalizePath(Path.Combine(directory, $"{stem}.manifest.json")),
            NormalizePath(Path.Combine(directory, $"{stem}.json"))
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    private static IReadOnlyList<CsvColumnProfile> BuildCsvColumnProfiles(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var profiles = new List<CsvColumnProfile>(headers.Count);

        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            var values = rows
                .Select(row => row.ElementAtOrDefault(columnIndex))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToArray();

            var numericValues = new List<double>();
            var booleanCount = 0;
            foreach (var value in values)
            {
                if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var numeric))
                    numericValues.Add(numeric);
                if (bool.TryParse(value, out _))
                    booleanCount++;
            }

            var kind =
                values.Length == 0 ? "empty" :
                numericValues.Count == values.Length ? "number" :
                booleanCount == values.Length ? "boolean" :
                "text";

            profiles.Add(new CsvColumnProfile(
                Name: headers[columnIndex],
                Kind: kind,
                NonEmptyCount: values.Length,
                ParsedNumericCount: numericValues.Count,
                ParsedBooleanCount: booleanCount,
                SampleValues: values.Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToArray(),
                Minimum: numericValues.Count > 0 ? numericValues.Min() : null,
                Maximum: numericValues.Count > 0 ? numericValues.Max() : null,
                Mean: numericValues.Count > 0 ? numericValues.Average() : null));
        }

        return profiles;
    }

}
