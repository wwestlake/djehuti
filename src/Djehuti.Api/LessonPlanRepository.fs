module Djehuti.Api.LessonPlanRepository

open System
open Npgsql

// Explicit and case-insensitive rather than trusting JsonSerializer's
// default (case-sensitive, matches declared F# property names verbatim --
// "Title", not "title"). Confirmed live: a manually-inserted lesson whose
// topics_json used lowercase/camelCase keys silently deserialized every
// field to null instead of throwing, because case-sensitive matching just
// treats an unmatched key as "not present" rather than an error.
let private topicsJsonOptions =
    System.Text.Json.JsonSerializerOptions(PropertyNameCaseInsensitive = true)

type LessonTopic = {
    Title:           string
    ContentMarkdown: string
    VideoUrl:        string option
    // "markdown" (default) or "notation" -- a notation topic's board renders
    // NotationJson (a MusicScore, see Djehuti.Teacher's MusicScore.cs) instead
    // of ContentMarkdown. Old topics predate this field and deserialize with
    // Kind = null, normalized to "markdown" in readLessonPlan below.
    Kind:            string
    NotationJson:    string option
}

let private normalizeTopic (t: LessonTopic) : LessonTopic =
    if String.IsNullOrWhiteSpace t.Kind then { t with Kind = "markdown" } else t

type LessonPlanRecord = {
    Id:          Guid
    AuthorId:    Guid
    Slug:        string
    Title:       string
    Subject:     string option
    Description: string option
    Topics:      LessonTopic list
    Published:   bool
    CreatedAt:   DateTimeOffset
    UpdatedAt:   DateTimeOffset
}

let private readLessonPlan (r: System.Data.Common.DbDataReader) : LessonPlanRecord =
    {
        Id          = r.GetGuid(0)
        AuthorId    = r.GetGuid(1)
        Slug        = r.GetString(2)
        Title       = r.GetString(3)
        Subject     = if r.IsDBNull(4) then None else Some (r.GetString(4))
        Description = if r.IsDBNull(5) then None else Some (r.GetString(5))
        Topics      =
            try
                System.Text.Json.JsonSerializer.Deserialize<LessonTopic list>(r.GetString(6), topicsJsonOptions)
                |> List.map normalizeTopic
            with _ -> []
        Published   = r.GetBoolean(7)
        CreatedAt   = r.GetFieldValue<DateTimeOffset>(8)
        UpdatedAt   = r.GetFieldValue<DateTimeOffset>(9)
    }

let private selectColumns = "id, author_id, slug, title, subject, description, topics_json, published, created_at, updated_at"

let listPublished () : LessonPlanRecord list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectColumns} FROM lesson_plans WHERE published = TRUE ORDER BY created_at DESC", conn)
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readLessonPlan reader :: results
    List.rev results

let listByAuthor (authorId: Guid) : LessonPlanRecord list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectColumns} FROM lesson_plans WHERE author_id = @authorId ORDER BY updated_at DESC", conn)
    cmd.Parameters.AddWithValue("authorId", authorId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readLessonPlan reader :: results
    List.rev results

let tryGetById (id: Guid) : LessonPlanRecord option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectColumns} FROM lesson_plans WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readLessonPlan reader) else None

let tryGetBySlug (slug: string) : LessonPlanRecord option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectColumns} FROM lesson_plans WHERE slug = @slug", conn)
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readLessonPlan reader) else None

let private slugify (title: string) : string =
    let lowered = title.Trim().ToLowerInvariant()
    let cleaned = System.Text.RegularExpressions.Regex.Replace(lowered, @"[^a-z0-9]+", "-").Trim('-')
    if String.IsNullOrWhiteSpace cleaned then "lesson" else cleaned

let create (authorId: Guid) (title: string) (subject: string option) (description: string option) : LessonPlanRecord =
    let baseSlug = slugify title
    let suffix = Guid.NewGuid().ToString("N").Substring(0, 6)
    let slug = $"{baseSlug}-{suffix}"
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        INSERT INTO lesson_plans (author_id, slug, title, subject, description)
        VALUES (@authorId, @slug, @title, @subject, @description)
        RETURNING {selectColumns}
    """, conn)
    cmd.Parameters.AddWithValue("authorId", authorId) |> ignore
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    cmd.Parameters.AddWithValue("title", title) |> ignore
    cmd.Parameters.AddWithValue("subject", (subject |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("description", (description |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    readLessonPlan reader

let update (id: Guid) (title: string) (subject: string option) (description: string option) (topics: LessonTopic list) (published: bool) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE lesson_plans
        SET title = @title, subject = @subject, description = @description,
            topics_json = @topics, published = @published, updated_at = now()
        WHERE id = @id
    """, conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.Parameters.AddWithValue("title", title) |> ignore
    cmd.Parameters.AddWithValue("subject", (subject |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("description", (description |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("topics", System.Text.Json.JsonSerializer.Serialize(topics)) |> ignore
    cmd.Parameters.AddWithValue("published", published) |> ignore
    cmd.ExecuteNonQuery() > 0

let delete (id: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("DELETE FROM lesson_plans WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0
