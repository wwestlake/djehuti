using System.Net.Http.Json;

namespace Djehuti.Teacher.Services;

public sealed class LessonTopic
{
    public string Title { get; set; } = "";
    public string ContentMarkdown { get; set; } = "";
    public string? VideoUrl { get; set; }
    // "markdown" (default) or "notation". A notation topic's board renders
    // NotationJson (see MusicScore.cs) instead of ContentMarkdown.
    public string Kind { get; set; } = "markdown";
    public string? NotationJson { get; set; }
}

public sealed class LessonPlan
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public List<LessonTopic> Topics { get; set; } = new();
    public bool Published { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// Calls Djehuti.Api's /api/teacher/* endpoints (Program.fs, backed by
// LessonPlanRepository.fs). Auth rides the same djehuti_auth cookie every
// other first-party app on the domain uses -- no separate login here.
//
// Every method swallows request failures (network error, API down, CORS in
// local dev without a proxy) and returns a safe empty/null/false result
// instead of throwing -- an unhandled exception inside a Razor page's
// OnInitializedAsync takes down the whole render, not just that page, so
// this is not optional defensiveness. Confirmed live: GetFromJsonAsync
// throwing on a failed catalog fetch crashed the entire Catalog page with
// Blazor's global error banner before this was added.
public sealed class TeacherApiClient
{
    private const string Base = "/djehuti/api/teacher";
    private readonly HttpClient _http;

    public TeacherApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<LessonPlan>> GetCatalogAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/catalog", ct);
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<LessonPlan>>(cancellationToken: ct) ?? new();
        }
        catch { return new(); }
    }

    public async Task<List<LessonPlan>> GetMineAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/lesson-plans/mine", ct);
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<LessonPlan>>(cancellationToken: ct) ?? new();
        }
        catch { return new(); }
    }

    public async Task<LessonPlan?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/lesson-plans/by-slug/{Uri.EscapeDataString(slug)}", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<LessonPlan>(cancellationToken: ct);
        }
        catch { return null; }
    }

    public async Task<LessonPlan?> CreateAsync(string title, string? subject, string? description, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{Base}/lesson-plans", new { title, subject = subject ?? "", description = description ?? "" }, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<LessonPlan>(cancellationToken: ct);
        }
        catch { return null; }
    }

    public async Task<bool> UpdateAsync(Guid id, string title, string? subject, string? description, List<LessonTopic> topics, bool published, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"{Base}/lesson-plans/{id}", new
            {
                title,
                subject = subject ?? "",
                description = description ?? "",
                topics = topics.Select(t => new { title = t.Title, contentMarkdown = t.ContentMarkdown, videoUrl = t.VideoUrl ?? "", kind = t.Kind, notationJson = t.NotationJson ?? "" }),
                published,
            }, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.DeleteAsync($"{Base}/lesson-plans/{id}", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<LearningTrack>> GetLearningTracksAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/learning-tracks", ct);
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<LearningTrack>>(cancellationToken: ct) ?? new();
        }
        catch { return new(); }
    }

    public async Task<LearningTrackDto?> GetLearningTrackBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/learning-tracks/by-slug/{Uri.EscapeDataString(slug)}", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<LearningTrackDto>(cancellationToken: ct);
        }
        catch { return null; }
    }

    public async Task<LearningTrackWithLessons?> GetLearningTrackWithLessonsAsync(Guid trackId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/learning-tracks/{trackId}", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<LearningTrackWithLessons>(cancellationToken: ct);
        }
        catch { return null; }
    }

    public async Task<UserTrackProgressDto?> TryGetUserTrackProgressAsync(Guid trackId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/learning-tracks/{trackId}/progress", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<UserTrackProgressDto>(cancellationToken: ct);
        }
        catch { return null; }
    }

    public async Task<bool> MarkLessonCompleteAsync(Guid trackId, Guid lessonId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsync($"{Base}/learning-tracks/{trackId}/lesson/{lessonId}/complete", null, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<LessonPlan?> GetLessonByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/lesson-plans/{id}", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<LessonPlan>(cancellationToken: ct);
        }
        catch { return null; }
    }

    public async Task<dynamic?> GetClassroomAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/classrooms/{id}", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken: ct);
        }
        catch { return null; }
    }

    public async Task<List<dynamic>> GetMyClassroomsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/classrooms/mine", ct);
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<dynamic>>(cancellationToken: ct) ?? new();
        }
        catch { return new(); }
    }

    public async Task<dynamic?> CreateClassroomAsync(string name, Guid? lessonPlanId = null, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{Base}/classrooms", new { name, lessonPlanId }, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken: ct);
        }
        catch { return null; }
    }

    public async Task<bool> JoinClassroomAsync(Guid classroomId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{Base}/classrooms/{classroomId}/members", new { }, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<dynamic>> GetClassroomMembersAsync(Guid classroomId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Base}/classrooms/{classroomId}/members", ct);
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<dynamic>>(cancellationToken: ct) ?? new();
        }
        catch { return new(); }
    }

    public async Task<bool> StartClassroomAsync(Guid classroomId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsync($"{Base}/classrooms/{classroomId}/start", null, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> EndClassroomAsync(Guid classroomId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsync($"{Base}/classrooms/{classroomId}/end", null, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> UpdateClassroomModeAsync(Guid classroomId, string mode, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"{Base}/classrooms/{classroomId}/mode", new { mode }, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public sealed class LearningTrack
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? Description { get; set; }
        public string? Subject { get; set; }
        public string Difficulty { get; set; } = "beginner";
        public int? EstimatedHours { get; set; }
        public int Position { get; set; }
        public bool Published { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public sealed class LearningTrackDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? Description { get; set; }
        public string? Subject { get; set; }
        public string Difficulty { get; set; } = "beginner";
        public int? EstimatedHours { get; set; }
        public bool Published { get; set; }
    }

    public sealed class LearningTrackWithLessons
    {
        public LearningTrackDto Track { get; set; } = new();
        public List<(Guid LessonId, int Sequence)> Lessons { get; set; } = new();
    }

    public sealed class UserTrackProgressDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid TrackId { get; set; }
        public int LessonsCompleted { get; set; }
        public int TotalLessons { get; set; }
        public int CompletionPercent { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? LastAccessedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }
}
