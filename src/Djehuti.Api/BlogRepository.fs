module Djehuti.Api.BlogRepository

open System
open System.Data.Common
open Npgsql
open Database

// ── Types ─────────────────────────────────────────────────────────────────────

type BlogSection = {
    Id:          Guid
    Name:        string
    Slug:        string
    Description: string option
    Position:    int
    CreatedAt:   DateTime
}

type BlogArticle = {
    Id:          Guid
    SectionId:   Guid
    AuthorId:    Guid
    Title:       string
    Slug:        string
    Content:     string
    Excerpt:     string option
    CoverUrl:    string option
    Status:      string
    PublishedAt: DateTime option
    CreatedAt:   DateTime
    UpdatedAt:   DateTime
}

type BlogComment = {
    Id:        Guid
    ArticleId: Guid
    AuthorId:  Guid
    Content:   string
    CreatedAt: DateTime
    DeletedAt: DateTime option
}

// ── Slug helpers ──────────────────────────────────────────────────────────────

let private slugify (s: string) =
    s.ToLower()
     .Replace(" ", "-")
     |> Seq.filter (fun c -> Char.IsLetterOrDigit(c) || c = '-')
     |> Array.ofSeq
     |> String

// ── Readers ───────────────────────────────────────────────────────────────────

let private readSection (r: DbDataReader) = {
    Id          = r.GetGuid(0)
    Name        = r.GetString(1)
    Slug        = r.GetString(2)
    Description = if r.IsDBNull(3) then None else Some (r.GetString(3))
    Position    = r.GetInt32(4)
    CreatedAt   = r.GetFieldValue<DateTime>(5)
}

let private readArticle (r: DbDataReader) = {
    Id          = r.GetGuid(0)
    SectionId   = r.GetGuid(1)
    AuthorId    = r.GetGuid(2)
    Title       = r.GetString(3)
    Slug        = r.GetString(4)
    Content     = r.GetString(5)
    Excerpt     = if r.IsDBNull(6) then None else Some (r.GetString(6))
    CoverUrl    = if r.IsDBNull(7) then None else Some (r.GetString(7))
    Status      = r.GetString(8)
    PublishedAt = if r.IsDBNull(9) then None else Some (r.GetFieldValue<DateTime>(9))
    CreatedAt   = r.GetFieldValue<DateTime>(10)
    UpdatedAt   = r.GetFieldValue<DateTime>(11)
}

let private readComment (r: DbDataReader) = {
    Id        = r.GetGuid(0)
    ArticleId = r.GetGuid(1)
    AuthorId  = r.GetGuid(2)
    Content   = r.GetString(3)
    CreatedAt = r.GetFieldValue<DateTime>(4)
    DeletedAt = if r.IsDBNull(5) then None else Some (r.GetFieldValue<DateTime>(5))
}

// ── Sections ──────────────────────────────────────────────────────────────────

let getSections () =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT id, name, slug, description, position, created_at FROM blog_sections ORDER BY position, name", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readSection r ]

let getSectionBySlug (slug: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT id, name, slug, description, position, created_at FROM blog_sections WHERE slug = @slug", conn)
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readSection r) else None

let createSection (name: string) (description: string option) =
    use conn = openConnection ()
    let slug = slugify name
    use cmd = new NpgsqlCommand(
        """INSERT INTO blog_sections (name, slug, description)
           VALUES (@name, @slug, @desc)
           RETURNING id, name, slug, description, position, created_at""", conn)
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    cmd.Parameters.AddWithValue("desc", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readSection r) else None

// ── Articles ──────────────────────────────────────────────────────────────────

let private articleCols = "id, section_id, author_id, title, slug, content, excerpt, cover_url, status, published_at, created_at, updated_at"

let getPublishedArticles (sectionId: Guid option) (page: int) (pageSize: int) =
    use conn = openConnection ()
    let where = match sectionId with | Some _ -> "WHERE status = 'published' AND section_id = @sid" | None -> "WHERE status = 'published'"
    use cmd = new NpgsqlCommand(
        $"SELECT {articleCols} FROM blog_articles {where} ORDER BY published_at DESC LIMIT @limit OFFSET @offset", conn)
    if sectionId.IsSome then cmd.Parameters.AddWithValue("sid", sectionId.Value) |> ignore
    cmd.Parameters.AddWithValue("limit", pageSize) |> ignore
    cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readArticle r ]

let getArticleBySlug (slug: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand($"SELECT {articleCols} FROM blog_articles WHERE slug = @slug", conn)
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let getArticleById (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand($"SELECT {articleCols} FROM blog_articles WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let getArticlesByAuthor (authorId: Guid) (page: int) (pageSize: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"SELECT {articleCols} FROM blog_articles WHERE author_id = @aid ORDER BY updated_at DESC LIMIT @limit OFFSET @offset", conn)
    cmd.Parameters.AddWithValue("aid", authorId) |> ignore
    cmd.Parameters.AddWithValue("limit", pageSize) |> ignore
    cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readArticle r ]

let createArticle (sectionId: Guid) (authorId: Guid) (title: string) (content: string) (excerpt: string option) =
    use conn = openConnection ()
    let shortId = Guid.NewGuid().ToString("N")[..7]
    let slug = $"{slugify title}-{shortId}"
    use cmd = new NpgsqlCommand(
        $"""INSERT INTO blog_articles (section_id, author_id, title, slug, content, excerpt)
            VALUES (@sid, @aid, @title, @slug, @content, @excerpt)
            RETURNING {articleCols}""", conn)
    cmd.Parameters.AddWithValue("sid", sectionId) |> ignore
    cmd.Parameters.AddWithValue("aid", authorId) |> ignore
    cmd.Parameters.AddWithValue("title", title) |> ignore
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    cmd.Parameters.AddWithValue("content", content) |> ignore
    cmd.Parameters.AddWithValue("excerpt", excerpt |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let updateArticle (id: Guid) (authorId: Guid) (title: string) (content: string) (excerpt: string option) (coverUrl: string option) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""UPDATE blog_articles
            SET title = @title, content = @content, excerpt = @excerpt, cover_url = @cover, updated_at = now()
            WHERE id = @id AND author_id = @aid
            RETURNING {articleCols}""", conn)
    cmd.Parameters.AddWithValue("title", title) |> ignore
    cmd.Parameters.AddWithValue("content", content) |> ignore
    cmd.Parameters.AddWithValue("excerpt", excerpt |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("cover", coverUrl |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.Parameters.AddWithValue("aid", authorId) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let setArticleStatus (id: Guid) (status: string) =
    use conn = openConnection ()
    let publishedAt = if status = "published" then "now()" else "published_at"
    use cmd = new NpgsqlCommand(
        $"UPDATE blog_articles SET status = @status, published_at = {publishedAt}, updated_at = now() WHERE id = @id RETURNING {articleCols}", conn)
    cmd.Parameters.AddWithValue("status", status) |> ignore
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let deleteArticle (id: Guid) (authorId: Guid) (isAdmin: bool) =
    use conn = openConnection ()
    let sql = if isAdmin then "DELETE FROM blog_articles WHERE id = @id"
              else "DELETE FROM blog_articles WHERE id = @id AND author_id = @aid AND status = 'draft'"
    use cmd = new NpgsqlCommand(sql, conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    if not isAdmin then cmd.Parameters.AddWithValue("aid", authorId) |> ignore
    cmd.ExecuteNonQuery() > 0

let getSubmittedArticles () =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT * FROM blog_articles WHERE status = 'submitted' ORDER BY created_at ASC", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readArticle r ]

// ── Comments ──────────────────────────────────────────────────────────────────

let getComments (articleId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT id, article_id, author_id, content, created_at, deleted_at FROM blog_comments WHERE article_id = @aid AND deleted_at IS NULL ORDER BY created_at ASC", conn)
    cmd.Parameters.AddWithValue("aid", articleId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readComment r ]

let createComment (articleId: Guid) (authorId: Guid) (content: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "INSERT INTO blog_comments (article_id, author_id, content) VALUES (@aid, @uid, @content) RETURNING id, article_id, author_id, content, created_at, deleted_at", conn)
    cmd.Parameters.AddWithValue("aid", articleId) |> ignore
    cmd.Parameters.AddWithValue("uid", authorId) |> ignore
    cmd.Parameters.AddWithValue("content", content) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readComment r) else None

let deleteComment (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("UPDATE blog_comments SET deleted_at = now() WHERE id = @id AND deleted_at IS NULL", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0
