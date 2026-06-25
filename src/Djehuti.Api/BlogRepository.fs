module Djehuti.Api.BlogRepository

open System
open System.Data.Common
open System.Text
open System.Text.RegularExpressions
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
    Id:              Guid
    SectionId:       Guid
    AuthorId:        Guid
    Title:           string
    Subtitle:        string option
    Slug:            string
    Content:         string
    BodyJson:        string option
    Excerpt:         string option
    CoverUrl:        string option
    Status:          string
    Visibility:      string
    Featured:        bool
    FeaturedPosition: int option
    Pinned:          bool
    PublishedAt:     DateTime option
    CreatedAt:       DateTime
    UpdatedAt:       DateTime
    DeletedAt:       DateTime option
}

type BlogTag = {
    Id:          Guid
    Name:        string
    Slug:        string
    Description: string option
}

type BlogAuthor = {
    UserId:      Guid
    Bio:         string option
    DisplayName: string option
    AvatarUrl:   string option
    SocialLinks: string
    Trusted:     bool
    CreatedAt:   DateTime
    UpdatedAt:   DateTime
}

type BlogUpload = {
    Id:               Guid
    ArticleId:        Guid option
    UploaderUserId:   Guid
    OriginalFilename: string
    MimeType:         string
    Format:           string
    StorageKey:       string
    SizeBytes:        int64 option
    ConversionStatus: string
    ConversionOption: string
    ConvertedHtml:    string option
    ErrorMessage:     string option
    CreatedAt:        DateTime
    UpdatedAt:        DateTime
}

type BlogModerationEntry = {
    Id:               Guid
    ArticleId:        Guid
    ModeratorUserId:  Guid option
    Action:           string
    Note:             string option
    CreatedAt:        DateTime
}

type SiteConfigEntry = {
    Scope:     string
    Key:       string
    Value:     string
    UpdatedAt: DateTime
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

// ── Markdown → HTML (lightweight, covers common blog markup) ─────────────────

let convertMarkdownToHtml (md: string) : string =
    let lines = md.Replace("\r\n", "\n").Split('\n')
    let sb = StringBuilder()
    let mutable i = 0
    let mutable inCodeBlock = false
    let mutable inList = false

    let inline applyInline (s: string) =
        let s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<strong>$1</strong>")
        let s = Regex.Replace(s, @"\*(.+?)\*",     "<em>$1</em>")
        let s = Regex.Replace(s, @"`(.+?)`",        "<code>$1</code>")
        let s = Regex.Replace(s, @"\[(.+?)\]\((.+?)\)", """<a href="$2">$1</a>""")
        s

    while i < lines.Length do
        let line = lines[i]
        if line.StartsWith("```") then
            if inCodeBlock then
                sb.AppendLine("</code></pre>") |> ignore
                inCodeBlock <- false
            else
                let lang = line.Substring(3).Trim()
                let cls = if lang <> "" then $" class=\"language-{lang}\"" else ""
                sb.AppendLine($"<pre><code{cls}>") |> ignore
                inCodeBlock <- true
        elif inCodeBlock then
            sb.AppendLine(System.Net.WebUtility.HtmlEncode(line)) |> ignore
        elif line.StartsWith("### ") then
            if inList then sb.AppendLine("</ul>") |> ignore; inList <- false
            sb.AppendLine($"<h3>{applyInline (line.Substring(4))}</h3>") |> ignore
        elif line.StartsWith("## ") then
            if inList then sb.AppendLine("</ul>") |> ignore; inList <- false
            sb.AppendLine($"<h2>{applyInline (line.Substring(3))}</h2>") |> ignore
        elif line.StartsWith("# ") then
            if inList then sb.AppendLine("</ul>") |> ignore; inList <- false
            sb.AppendLine($"<h1>{applyInline (line.Substring(2))}</h1>") |> ignore
        elif line.StartsWith("- ") || line.StartsWith("* ") then
            if not inList then sb.AppendLine("<ul>") |> ignore; inList <- true
            sb.AppendLine($"<li>{applyInline (line.Substring(2))}</li>") |> ignore
        elif Regex.IsMatch(line, @"^\d+\. ") then
            if inList then sb.AppendLine("</ul>") |> ignore; inList <- false
            let stripped = Regex.Replace(line, @"^\d+\. ", "")
            sb.AppendLine($"<ol><li>{applyInline stripped}</li></ol>") |> ignore
        elif line.StartsWith("---") || line.StartsWith("***") then
            if inList then sb.AppendLine("</ul>") |> ignore; inList <- false
            sb.AppendLine("<hr/>") |> ignore
        elif line.Trim() = "" then
            if inList then sb.AppendLine("</ul>") |> ignore; inList <- false
        else
            if inList then sb.AppendLine("</ul>") |> ignore; inList <- false
            sb.AppendLine($"<p>{applyInline line}</p>") |> ignore
        i <- i + 1

    if inList then sb.AppendLine("</ul>") |> ignore
    sb.ToString()

let convertTextToHtml (txt: string) : string =
    txt.Replace("\r\n", "\n").Split([|"\n\n"|], StringSplitOptions.RemoveEmptyEntries)
    |> Array.map (fun para ->
        let escaped = System.Net.WebUtility.HtmlEncode(para.Trim())
        $"<p>{escaped}</p>")
    |> String.concat "\n"

// ── Readers ───────────────────────────────────────────────────────────────────

let private readSection (r: DbDataReader) : BlogSection = {
    Id          = r.GetGuid(0)
    Name        = r.GetString(1)
    Slug        = r.GetString(2)
    Description = if r.IsDBNull(3) then None else Some (r.GetString(3))
    Position    = r.GetInt32(4)
    CreatedAt   = r.GetFieldValue<DateTime>(5)
}

let private articleCols =
    "id, section_id, author_id, title, slug, content, excerpt, cover_url, status, published_at, " +
    "created_at, updated_at, subtitle, body_json, visibility, featured, featured_position, pinned, deleted_at"

let private readArticle (r: DbDataReader) : BlogArticle = {
    Id               = r.GetGuid(0)
    SectionId        = r.GetGuid(1)
    AuthorId         = r.GetGuid(2)
    Title            = r.GetString(3)
    Slug             = r.GetString(4)
    Content          = r.GetString(5)
    Excerpt          = if r.IsDBNull(6)  then None else Some (r.GetString(6))
    CoverUrl         = if r.IsDBNull(7)  then None else Some (r.GetString(7))
    Status           = r.GetString(8)
    PublishedAt      = if r.IsDBNull(9)  then None else Some (r.GetFieldValue<DateTime>(9))
    CreatedAt        = r.GetFieldValue<DateTime>(10)
    UpdatedAt        = r.GetFieldValue<DateTime>(11)
    Subtitle         = if r.IsDBNull(12) then None else Some (r.GetString(12))
    BodyJson         = if r.IsDBNull(13) then None else Some (r.GetString(13))
    Visibility       = if r.IsDBNull(14) then "public" else r.GetString(14)
    Featured         = if r.IsDBNull(15) then false else r.GetBoolean(15)
    FeaturedPosition = if r.IsDBNull(16) then None else Some (r.GetInt32(16))
    Pinned           = if r.IsDBNull(17) then false else r.GetBoolean(17)
    DeletedAt        = if r.IsDBNull(18) then None else Some (r.GetFieldValue<DateTime>(18))
}

let private readTag (r: DbDataReader) : BlogTag = {
    Id          = r.GetGuid(0)
    Name        = r.GetString(1)
    Slug        = r.GetString(2)
    Description = if r.IsDBNull(3) then None else Some (r.GetString(3))
}

let private readAuthor (r: DbDataReader) : BlogAuthor = {
    UserId      = r.GetGuid(0)
    Bio         = if r.IsDBNull(1) then None else Some (r.GetString(1))
    DisplayName = if r.IsDBNull(2) then None else Some (r.GetString(2))
    AvatarUrl   = if r.IsDBNull(3) then None else Some (r.GetString(3))
    SocialLinks = if r.IsDBNull(4) then "[]" else r.GetString(4)
    Trusted     = if r.IsDBNull(5) then false else r.GetBoolean(5)
    CreatedAt   = r.GetFieldValue<DateTime>(6)
    UpdatedAt   = if r.IsDBNull(7) then r.GetFieldValue<DateTime>(6) else r.GetFieldValue<DateTime>(7)
}

let private readUpload (r: DbDataReader) : BlogUpload = {
    Id               = r.GetGuid(0)
    ArticleId        = if r.IsDBNull(1)  then None else Some (r.GetGuid(1))
    UploaderUserId   = r.GetGuid(2)
    OriginalFilename = r.GetString(3)
    MimeType         = r.GetString(4)
    Format           = r.GetString(5)
    StorageKey       = r.GetString(6)
    SizeBytes        = if r.IsDBNull(7)  then None else Some (r.GetInt64(7))
    ConversionStatus = r.GetString(8)
    ConversionOption = r.GetString(9)
    ConvertedHtml    = if r.IsDBNull(10) then None else Some (r.GetString(10))
    ErrorMessage     = if r.IsDBNull(11) then None else Some (r.GetString(11))
    CreatedAt        = r.GetFieldValue<DateTime>(12)
    UpdatedAt        = r.GetFieldValue<DateTime>(13)
}

let private readModerationEntry (r: DbDataReader) : BlogModerationEntry = {
    Id              = r.GetGuid(0)
    ArticleId       = r.GetGuid(1)
    ModeratorUserId = if r.IsDBNull(2) then None else Some (r.GetGuid(2))
    Action          = r.GetString(3)
    Note            = if r.IsDBNull(4) then None else Some (r.GetString(4))
    CreatedAt       = r.GetFieldValue<DateTime>(5)
}

let private readComment (r: DbDataReader) : BlogComment = {
    Id        = r.GetGuid(0)
    ArticleId = r.GetGuid(1)
    AuthorId  = r.GetGuid(2)
    Content   = r.GetString(3)
    CreatedAt = r.GetFieldValue<DateTime>(4)
    DeletedAt = if r.IsDBNull(5) then None else Some (r.GetFieldValue<DateTime>(5))
}

let private opt (v: 'a option) : obj =
    v |> Option.map box |> Option.defaultValue (box DBNull.Value)

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
    cmd.Parameters.AddWithValue("name", name)        |> ignore
    cmd.Parameters.AddWithValue("slug", slug)        |> ignore
    cmd.Parameters.AddWithValue("desc", opt description) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readSection r) else None

let getOrCreateDefaultSection () =
    match getSections () with
    | s :: _ -> s
    | [] ->
        createSection "General" (Some "General articles")
        |> Option.defaultWith (fun () -> failwith "Could not create default blog section")


let getTags () =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT id, name, slug, description FROM blog_tags ORDER BY name", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readTag r ]

let getTagById (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("SELECT id, name, slug, description FROM blog_tags WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readTag r) else None

let createTag (name: string) (description: string option) =
    use conn = openConnection ()
    let slug = slugify name
    use cmd = new NpgsqlCommand(
        """INSERT INTO blog_tags (name, slug, description) VALUES (@name, @slug, @desc)
           ON CONFLICT (slug) DO NOTHING
           RETURNING id, name, slug, description""", conn)
    cmd.Parameters.AddWithValue("name", name)        |> ignore
    cmd.Parameters.AddWithValue("slug", slug)        |> ignore
    cmd.Parameters.AddWithValue("desc", opt description) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readTag r) else None

let updateTag (id: Guid) (name: string) (description: string option) =
    use conn = openConnection ()
    let slug = slugify name
    use cmd = new NpgsqlCommand(
        "UPDATE blog_tags SET name = @name, slug = @slug, description = @desc WHERE id = @id RETURNING id, name, slug, description", conn)
    cmd.Parameters.AddWithValue("name", name)        |> ignore
    cmd.Parameters.AddWithValue("slug", slug)        |> ignore
    cmd.Parameters.AddWithValue("desc", opt description) |> ignore
    cmd.Parameters.AddWithValue("id", id)            |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readTag r) else None

let deleteTag (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("DELETE FROM blog_tags WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0

let getTagsForArticle (articleId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT t.id, t.name, t.slug, t.description
           FROM blog_tags t
           JOIN blog_article_tags at ON at.tag_id = t.id
           WHERE at.article_id = @aid
           ORDER BY t.name""", conn)
    cmd.Parameters.AddWithValue("aid", articleId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readTag r ]

let setArticleTags (articleId: Guid) (tagIds: Guid list) =
    use conn = openConnection ()
    use delCmd = new NpgsqlCommand("DELETE FROM blog_article_tags WHERE article_id = @aid", conn)
    delCmd.Parameters.AddWithValue("aid", articleId) |> ignore
    delCmd.ExecuteNonQuery() |> ignore
    for tagId in tagIds do
        use insCmd = new NpgsqlCommand(
            "INSERT INTO blog_article_tags (article_id, tag_id) VALUES (@aid, @tid) ON CONFLICT DO NOTHING", conn)
        insCmd.Parameters.AddWithValue("aid", articleId) |> ignore
        insCmd.Parameters.AddWithValue("tid", tagId)     |> ignore
        insCmd.ExecuteNonQuery() |> ignore

// ── Articles ──────────────────────────────────────────────────────────────────

let getPublishedArticles (sectionId: Guid option) (search: string option) (tagSlug: string option) (page: int) (pageSize: int) =
    use conn = openConnection ()
    let conditions = ResizeArray<string>()
    conditions.Add("a.status = 'published'")
    conditions.Add("a.deleted_at IS NULL")
    conditions.Add("a.visibility = 'public'")
    if sectionId.IsSome then conditions.Add("a.section_id = @sid")
    if search.IsSome   then conditions.Add("to_tsvector('english', a.title || ' ' || coalesce(a.excerpt,'') || ' ' || coalesce(a.content,'')) @@ plainto_tsquery('english', @q)")
    if tagSlug.IsSome  then conditions.Add("EXISTS (SELECT 1 FROM blog_article_tags at JOIN blog_tags t ON t.id = at.tag_id WHERE at.article_id = a.id AND t.slug = @tag)")
    let where = String.concat " AND " conditions
    let sql = $"""
        SELECT {articleCols.Replace("id,", "a.id,").Replace(", ", ", a.").Replace("a.a.", "a.")}
        FROM blog_articles a
        WHERE {where}
        ORDER BY a.pinned DESC, a.published_at DESC
        LIMIT @limit OFFSET @offset"""
    // Build simple non-aliased version since articleCols doesn't use alias
    let sql2 = $"""
        SELECT {articleCols} FROM blog_articles a
        WHERE {where.Replace("a.", "")}
        ORDER BY pinned DESC, published_at DESC
        LIMIT @limit OFFSET @offset"""
    use cmd = new NpgsqlCommand(sql2, conn)
    if sectionId.IsSome then cmd.Parameters.AddWithValue("sid", sectionId.Value) |> ignore
    if search.IsSome    then cmd.Parameters.AddWithValue("q",   search.Value)     |> ignore
    if tagSlug.IsSome   then cmd.Parameters.AddWithValue("tag", tagSlug.Value)    |> ignore
    cmd.Parameters.AddWithValue("limit",  pageSize)              |> ignore
    cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readArticle r ]

let getArticleBySlug (slug: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand($"SELECT {articleCols} FROM blog_articles WHERE slug = @slug AND deleted_at IS NULL", conn)
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let getArticleById (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand($"SELECT {articleCols} FROM blog_articles WHERE id = @id AND deleted_at IS NULL", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let getArticlesByAuthor (authorId: Guid) (page: int) (pageSize: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"SELECT {articleCols} FROM blog_articles WHERE author_id = @aid AND deleted_at IS NULL ORDER BY updated_at DESC LIMIT @limit OFFSET @offset", conn)
    cmd.Parameters.AddWithValue("aid",    authorId)             |> ignore
    cmd.Parameters.AddWithValue("limit",  pageSize)             |> ignore
    cmd.Parameters.AddWithValue("offset", (page-1) * pageSize)  |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readArticle r ]

let getModerationQueue () =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"SELECT {articleCols} FROM blog_articles WHERE status IN ('submitted','under_review','approved') AND deleted_at IS NULL ORDER BY created_at ASC", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readArticle r ]

let createArticle (sectionId: Guid) (authorId: Guid) (title: string) (subtitle: string option) (content: string) (bodyJson: string option) (excerpt: string option) (visibility: string) =
    use conn = openConnection ()
    let shortId = Guid.NewGuid().ToString("N")[..7]
    let slug = $"{slugify title}-{shortId}"
    use cmd = new NpgsqlCommand(
        $"""INSERT INTO blog_articles (section_id, author_id, title, slug, content, excerpt, subtitle, body_json, visibility)
            VALUES (@sid, @aid, @title, @slug, @content, @excerpt, @subtitle, @bodyJson::jsonb, @vis)
            RETURNING {articleCols}""", conn)
    cmd.Parameters.AddWithValue("sid",      sectionId)      |> ignore
    cmd.Parameters.AddWithValue("aid",      authorId)       |> ignore
    cmd.Parameters.AddWithValue("title",    title)          |> ignore
    cmd.Parameters.AddWithValue("slug",     slug)           |> ignore
    cmd.Parameters.AddWithValue("content",  content)        |> ignore
    cmd.Parameters.AddWithValue("excerpt",  opt excerpt)    |> ignore
    cmd.Parameters.AddWithValue("subtitle", opt subtitle)   |> ignore
    cmd.Parameters.AddWithValue("bodyJson", opt bodyJson)   |> ignore
    cmd.Parameters.AddWithValue("vis",      visibility)     |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let updateArticle (id: Guid) (authorId: Guid) (isAdmin: bool) (title: string) (subtitle: string option) (content: string) (bodyJson: string option) (excerpt: string option) (coverUrl: string option) (visibility: string) =
    use conn = openConnection ()
    let ownerClause = if isAdmin then "" else "AND author_id = @aid"
    use cmd = new NpgsqlCommand(
        $"""UPDATE blog_articles
            SET title = @title, subtitle = @subtitle, content = @content, body_json = @bodyJson::jsonb,
                excerpt = @excerpt, cover_url = @cover, visibility = @vis, updated_at = now()
            WHERE id = @id AND deleted_at IS NULL {ownerClause}
            RETURNING {articleCols}""", conn)
    cmd.Parameters.AddWithValue("title",    title)          |> ignore
    cmd.Parameters.AddWithValue("subtitle", opt subtitle)   |> ignore
    cmd.Parameters.AddWithValue("content",  content)        |> ignore
    cmd.Parameters.AddWithValue("bodyJson", opt bodyJson)   |> ignore
    cmd.Parameters.AddWithValue("excerpt",  opt excerpt)    |> ignore
    cmd.Parameters.AddWithValue("cover",    opt coverUrl)   |> ignore
    cmd.Parameters.AddWithValue("vis",      visibility)     |> ignore
    cmd.Parameters.AddWithValue("id",       id)             |> ignore
    if not isAdmin then cmd.Parameters.AddWithValue("aid", authorId) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let setArticleStatus (id: Guid) (status: string) =
    use conn = openConnection ()
    let publishedAt = if status = "published" then "now()" else "published_at"
    use cmd = new NpgsqlCommand(
        $"UPDATE blog_articles SET status = @status, published_at = {publishedAt}, updated_at = now() WHERE id = @id AND deleted_at IS NULL RETURNING {articleCols}", conn)
    cmd.Parameters.AddWithValue("status", status) |> ignore
    cmd.Parameters.AddWithValue("id",     id)     |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let setArticleFeatured (id: Guid) (featured: bool) (position: int option) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"UPDATE blog_articles SET featured = @f, featured_position = @pos, updated_at = now() WHERE id = @id RETURNING {articleCols}", conn)
    cmd.Parameters.AddWithValue("f",   featured)     |> ignore
    cmd.Parameters.AddWithValue("pos", opt position) |> ignore
    cmd.Parameters.AddWithValue("id",  id)           |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let setArticlePinned (id: Guid) (pinned: bool) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"UPDATE blog_articles SET pinned = @p, updated_at = now() WHERE id = @id RETURNING {articleCols}", conn)
    cmd.Parameters.AddWithValue("p",  pinned) |> ignore
    cmd.Parameters.AddWithValue("id", id)     |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readArticle r) else None

let softDeleteArticle (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("UPDATE blog_articles SET deleted_at = now() WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0

let deleteArticle (id: Guid) (authorId: Guid) (isAdmin: bool) =
    if isAdmin then
        softDeleteArticle id
    else
        use conn = openConnection ()
        use cmd = new NpgsqlCommand(
            "UPDATE blog_articles SET deleted_at = now() WHERE id = @id AND author_id = @aid AND status = 'draft'", conn)
        cmd.Parameters.AddWithValue("id",  id)       |> ignore
        cmd.Parameters.AddWithValue("aid", authorId) |> ignore
        cmd.ExecuteNonQuery() > 0

// ── Authors ───────────────────────────────────────────────────────────────────

let private authorCols = "user_id, bio, display_name, avatar_url, social_links, trusted, created_at, updated_at"

let getAuthor (userId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand($"SELECT {authorCols} FROM blog_authors WHERE user_id = @uid", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readAuthor r) else None

let listAuthors () =
    use conn = openConnection ()
    // Return all users, joined with blog_authors for bio/trusted status
    use cmd = new NpgsqlCommand(
        """SELECT u.id, a.bio, COALESCE(a.display_name, u.display_name), a.avatar_url,
                  a.social_links, a.trusted, u.created_at, a.updated_at
           FROM users u
           LEFT JOIN blog_authors a ON a.user_id = u.id
           ORDER BY u.created_at DESC""", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readAuthor r ]

let upsertAuthor (userId: Guid) (bio: string option) (displayName: string option) (avatarUrl: string option) (socialLinks: string) (trusted: bool) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""INSERT INTO blog_authors (user_id, bio, display_name, avatar_url, social_links, trusted)
            VALUES (@uid, @bio, @dn, @av, @sl::jsonb, @trusted)
            ON CONFLICT (user_id) DO UPDATE
              SET bio = EXCLUDED.bio, display_name = EXCLUDED.display_name,
                  avatar_url = EXCLUDED.avatar_url, social_links = EXCLUDED.social_links,
                  trusted = EXCLUDED.trusted, updated_at = now()
            RETURNING {authorCols}""", conn)
    cmd.Parameters.AddWithValue("uid",     userId)          |> ignore
    cmd.Parameters.AddWithValue("bio",     opt bio)         |> ignore
    cmd.Parameters.AddWithValue("dn",      opt displayName) |> ignore
    cmd.Parameters.AddWithValue("av",      opt avatarUrl)   |> ignore
    cmd.Parameters.AddWithValue("sl",      socialLinks)     |> ignore
    cmd.Parameters.AddWithValue("trusted", trusted)         |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readAuthor r) else None

let deleteAuthor (userId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("DELETE FROM blog_authors WHERE user_id = @uid", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.ExecuteNonQuery() > 0

// ── Uploads ───────────────────────────────────────────────────────────────────

let private uploadCols =
    "id, article_id, uploader_user_id, original_filename, mime_type, format, storage_key, " +
    "size_bytes, conversion_status, conversion_option, converted_html, error_message, created_at, updated_at"

let createUpload (uploaderUserId: Guid) (originalFilename: string) (mimeType: string) (format: string) (storageKey: string) (sizeBytes: int64 option) (conversionOption: string) =
    use conn = openConnection ()
    // For txt, md, html: mark as done immediately — conversion happens in caller before this
    let html, status =
        None, "pending"
    use cmd = new NpgsqlCommand(
        $"""INSERT INTO blog_uploads (uploader_user_id, original_filename, mime_type, format, storage_key, size_bytes, conversion_status, conversion_option)
            VALUES (@uid, @fn, @mt, @fmt, @key, @size, @cs, @co)
            RETURNING {uploadCols}""", conn)
    cmd.Parameters.AddWithValue("uid",  uploaderUserId)         |> ignore
    cmd.Parameters.AddWithValue("fn",   originalFilename)       |> ignore
    cmd.Parameters.AddWithValue("mt",   mimeType)               |> ignore
    cmd.Parameters.AddWithValue("fmt",  format)                 |> ignore
    cmd.Parameters.AddWithValue("key",  storageKey)             |> ignore
    cmd.Parameters.AddWithValue("size", opt sizeBytes)          |> ignore
    cmd.Parameters.AddWithValue("cs",   status)                 |> ignore
    cmd.Parameters.AddWithValue("co",   conversionOption)       |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readUpload r) else None

let getUpload (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand($"SELECT {uploadCols} FROM blog_uploads WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readUpload r) else None

let updateUploadConversion (id: Guid) (status: string) (html: string option) (error: string option) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"UPDATE blog_uploads SET conversion_status = @s, converted_html = @html, error_message = @err, updated_at = now() WHERE id = @id RETURNING {uploadCols}", conn)
    cmd.Parameters.AddWithValue("s",    status)    |> ignore
    cmd.Parameters.AddWithValue("html", opt html)  |> ignore
    cmd.Parameters.AddWithValue("err",  opt error) |> ignore
    cmd.Parameters.AddWithValue("id",   id)        |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readUpload r) else None

let attachUploadToArticle (uploadId: Guid) (articleId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"UPDATE blog_uploads SET article_id = @aid, updated_at = now() WHERE id = @id RETURNING {uploadCols}", conn)
    cmd.Parameters.AddWithValue("aid", articleId) |> ignore
    cmd.Parameters.AddWithValue("id",  uploadId)  |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readUpload r) else None

// ── Moderation log ────────────────────────────────────────────────────────────

let logModerationAction (articleId: Guid) (moderatorUserId: Guid option) (action: string) (note: string option) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO blog_moderation_log (article_id, moderator_user_id, action, note)
           VALUES (@aid, @mid, @action, @note)
           RETURNING id, article_id, moderator_user_id, action, note, created_at""", conn)
    cmd.Parameters.AddWithValue("aid",    articleId)              |> ignore
    cmd.Parameters.AddWithValue("mid",    opt moderatorUserId)    |> ignore
    cmd.Parameters.AddWithValue("action", action)                 |> ignore
    cmd.Parameters.AddWithValue("note",   opt note)               |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readModerationEntry r) else None

let getModerationLog (articleId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT id, article_id, moderator_user_id, action, note, created_at FROM blog_moderation_log WHERE article_id = @aid ORDER BY created_at DESC", conn)
    cmd.Parameters.AddWithValue("aid", articleId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readModerationEntry r ]

// ── Site config ───────────────────────────────────────────────────────────────

let getConfig (scope: string option) =
    use conn = openConnection ()
    let where = match scope with Some _ -> "WHERE scope = @scope" | None -> ""
    use cmd = new NpgsqlCommand($"SELECT scope, key, value, updated_at FROM site_config {where} ORDER BY scope, key", conn)
    if scope.IsSome then cmd.Parameters.AddWithValue("scope", scope.Value) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do
        yield {
            Scope     = r.GetString(0)
            Key       = r.GetString(1)
            Value     = r.GetString(2)
            UpdatedAt = r.GetFieldValue<DateTime>(3)
        } ]

let setConfig (scope: string) (key: string) (value: string) (updatedBy: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO site_config (scope, key, value, updated_by)
           VALUES (@scope, @key, @value::jsonb, @by)
           ON CONFLICT (scope, key) DO UPDATE
             SET value = EXCLUDED.value, updated_at = now(), updated_by = EXCLUDED.updated_by
           RETURNING scope, key, value, updated_at""", conn)
    cmd.Parameters.AddWithValue("scope", scope)   |> ignore
    cmd.Parameters.AddWithValue("key",   key)     |> ignore
    cmd.Parameters.AddWithValue("value", value)   |> ignore
    cmd.Parameters.AddWithValue("by",    updatedBy) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then
        Some { Scope = r.GetString(0); Key = r.GetString(1); Value = r.GetString(2); UpdatedAt = r.GetFieldValue<DateTime>(3) }
    else None

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
    cmd.Parameters.AddWithValue("aid",     articleId) |> ignore
    cmd.Parameters.AddWithValue("uid",     authorId)  |> ignore
    cmd.Parameters.AddWithValue("content", content)   |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readComment r) else None

let deleteComment (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("UPDATE blog_comments SET deleted_at = now() WHERE id = @id AND deleted_at IS NULL", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0
