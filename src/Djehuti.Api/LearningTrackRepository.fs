module Djehuti.Api.LearningTrackRepository

open System
open Npgsql

type LearningTrack = {
    Id:              Guid
    Title:           string
    Slug:            string
    Description:     string option
    Subject:         string option
    Difficulty:      string
    EstimatedHours:  int option
    Position:        int
    Published:       bool
    CreatedAt:       DateTimeOffset
    UpdatedAt:       DateTimeOffset
}

type LearningTrackWithLessons = {
    Track:    LearningTrack
    Lessons:  (Guid * int) list // (lesson_id, sequence_order)
}

type UserTrackProgress = {
    Id:                 Guid
    UserId:             Guid
    TrackId:            Guid
    LessonsCompleted:   int
    TotalLessons:       int
    CompletionPercent:  int
    StartedAt:          DateTimeOffset
    LastAccessedAt:     DateTimeOffset option
    CompletedAt:        DateTimeOffset option
    CreatedAt:          DateTimeOffset
    UpdatedAt:          DateTimeOffset
}

let private readTrack (r: System.Data.Common.DbDataReader) : LearningTrack =
    {
        Id              = r.GetGuid(0)
        Title           = r.GetString(1)
        Slug            = r.GetString(2)
        Description     = if r.IsDBNull(3) then None else Some (r.GetString(3))
        Subject         = if r.IsDBNull(4) then None else Some (r.GetString(4))
        Difficulty      = r.GetString(5)
        EstimatedHours  = if r.IsDBNull(6) then None else Some (r.GetInt32(6))
        Position        = r.GetInt32(7)
        Published       = r.GetBoolean(8)
        CreatedAt       = r.GetFieldValue<DateTimeOffset>(9)
        UpdatedAt       = r.GetFieldValue<DateTimeOffset>(10)
    }

let private readTrackProgress (r: System.Data.Common.DbDataReader) : UserTrackProgress =
    {
        Id                = r.GetGuid(0)
        UserId            = r.GetGuid(1)
        TrackId           = r.GetGuid(2)
        LessonsCompleted  = r.GetInt32(3)
        TotalLessons      = r.GetInt32(4)
        CompletionPercent = r.GetInt32(5)
        StartedAt         = r.GetFieldValue<DateTimeOffset>(6)
        LastAccessedAt    = if r.IsDBNull(7) then None else Some (r.GetFieldValue<DateTimeOffset>(7))
        CompletedAt       = if r.IsDBNull(8) then None else Some (r.GetFieldValue<DateTimeOffset>(9))
        CreatedAt         = r.GetFieldValue<DateTimeOffset>(10)
        UpdatedAt         = r.GetFieldValue<DateTimeOffset>(11)
    }

let private selectTrackColumns = "id, title, slug, description, subject, difficulty, estimated_hours, position, published, created_at, updated_at"

// Get all published tracks
let listPublished () : LearningTrack list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectTrackColumns} FROM learning_tracks WHERE published = TRUE ORDER BY position, created_at DESC", conn)
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readTrack reader :: results
    List.rev results

// Get track by ID with lessons
let tryGetWithLessons (trackId: Guid) : LearningTrackWithLessons option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectTrackColumns} FROM learning_tracks WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", trackId) |> ignore
    use reader = cmd.ExecuteReader()
    if not (reader.Read()) then None
    else
        let track = readTrack reader
        // Get lessons for this track
        use cmd2 = new NpgsqlCommand("SELECT lesson_id, sequence FROM learning_track_lessons WHERE track_id = @trackId ORDER BY sequence", conn)
        cmd2.Parameters.AddWithValue("trackId", trackId) |> ignore
        use reader2 = cmd2.ExecuteReader()
        let mutable lessons = []
        while reader2.Read() do
            lessons <- (reader2.GetGuid(0), reader2.GetInt32(1)) :: lessons
        let lessons = List.rev lessons
        Some { Track = track; Lessons = lessons }

// Get track by slug
let tryGetBySlug (slug: string) : LearningTrack option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectTrackColumns} FROM learning_tracks WHERE slug = @slug", conn)
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readTrack reader) else None

// Get user's progress on a track
let tryGetUserProgress (userId: Guid) (trackId: Guid) : UserTrackProgress option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT id, user_id, track_id, lessons_completed, total_lessons, completion_percent,
               started_at, last_accessed_at, completed_at, created_at, updated_at
        FROM user_track_progress
        WHERE user_id = @userId AND track_id = @trackId
    """, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("trackId", trackId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readTrackProgress reader) else None

// Get all tracks user has started
let listUserProgress (userId: Guid) : UserTrackProgress list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT id, user_id, track_id, lessons_completed, total_lessons, completion_percent,
               started_at, last_accessed_at, completed_at, created_at, updated_at
        FROM user_track_progress
        WHERE user_id = @userId
        ORDER BY last_accessed_at DESC NULLS LAST
    """, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readTrackProgress reader :: results
    List.rev results

// Create or get user progress on track
let getOrCreateProgress (userId: Guid) (trackId: Guid) (totalLessons: int) : UserTrackProgress =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        INSERT INTO user_track_progress (user_id, track_id, total_lessons)
        VALUES (@userId, @trackId, @totalLessons)
        ON CONFLICT (user_id, track_id) DO UPDATE SET last_accessed_at = NOW()
        RETURNING id, user_id, track_id, lessons_completed, total_lessons, completion_percent,
                  started_at, last_accessed_at, completed_at, created_at, updated_at
    """, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("trackId", trackId) |> ignore
    cmd.Parameters.AddWithValue("totalLessons", totalLessons) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    readTrackProgress reader

// Mark a lesson as completed in a track
let markLessonCompleted (userId: Guid) (trackId: Guid) (lessonId: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        INSERT INTO user_track_lesson_progress (user_id, track_id, lesson_id, completed)
        VALUES (@userId, @trackId, @lessonId, TRUE)
        ON CONFLICT (user_id, track_id, lesson_id) DO UPDATE SET completed = TRUE, completed_at = NOW()
    """, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("trackId", trackId) |> ignore
    cmd.Parameters.AddWithValue("lessonId", lessonId) |> ignore
    cmd.ExecuteNonQuery() > 0

// Update progress metrics
let updateProgress (userId: Guid) (trackId: Guid) (lessonId: Guid) : unit =
    use conn = Database.openConnection()
    // Mark the lesson as completed
    use cmd = new NpgsqlCommand("""
        INSERT INTO user_track_lesson_progress (user_id, track_id, lesson_id, completed, completed_at)
        VALUES (@userId, @trackId, @lessonId, TRUE, NOW())
        ON CONFLICT (user_id, track_id, lesson_id) DO UPDATE SET completed = TRUE, completed_at = NOW()
    """, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("trackId", trackId) |> ignore
    cmd.Parameters.AddWithValue("lessonId", lessonId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

    // Recalculate progress
    use cmd2 = new NpgsqlCommand("""
        UPDATE user_track_progress
        SET lessons_completed = (
            SELECT COUNT(*) FROM user_track_lesson_progress
            WHERE user_id = @userId AND track_id = @trackId AND completed = TRUE
        ),
        completion_percent = CASE
            WHEN total_lessons > 0 THEN (
                SELECT (COUNT(*) * 100) / @totalLessons FROM user_track_lesson_progress
                WHERE user_id = @userId AND track_id = @trackId AND completed = TRUE
            )
            ELSE 0
        END,
        last_accessed_at = NOW()
        WHERE user_id = @userId AND track_id = @trackId
    """, conn)
    cmd2.Parameters.AddWithValue("userId", userId) |> ignore
    cmd2.Parameters.AddWithValue("trackId", trackId) |> ignore
    cmd2.Parameters.AddWithValue("totalLessons", 1) |> ignore
    cmd2.ExecuteNonQuery() |> ignore
