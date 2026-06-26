module Djehuti.Api.ForumRepository

open System
open System.Data.Common
open Npgsql
open Database

// ── Types ─────────────────────────────────────────────────────────────────────

type ForumCategory = {
    Id:          Guid
    Name:        string
    Description: string option
    Position:    int
    CreatedAt:   DateTime
}

type ForumForum = {
    Id:          Guid
    CategoryId:  Guid
    Name:        string
    Description: string option
    Position:    int
    ThreadCount: int
    PostCount:   int
    LastPostAt:  DateTime option
    LastPostBy:  Guid option
    CreatedAt:   DateTime
}

type ForumThread = {
    Id:         Guid
    ForumId:    Guid
    AuthorId:   Guid
    Title:      string
    IsPinned:   bool
    IsLocked:   bool
    PostCount:  int
    ViewCount:  int
    LastPostAt: DateTime option
    LastPostBy: Guid option
    CreatedAt:  DateTime
    UpdatedAt:  DateTime
}

type ForumPost = {
    Id:        Guid
    ThreadId:  Guid
    AuthorId:  Guid
    Content:   string
    IsAnswer:  bool
    VoteCount: int
    CreatedAt: DateTime
    UpdatedAt: DateTime
    DeletedAt: DateTime option
}

// ── Helpers ───────────────────────────────────────────────────────────────────

let private readCategory (r: DbDataReader) = {
    Id          = r.GetGuid(0)
    Name        = r.GetString(1)
    Description = if r.IsDBNull(2) then None else Some (r.GetString(2))
    Position    = r.GetInt32(3)
    CreatedAt   = r.GetFieldValue<DateTime>(4)
}

let private readForum (r: DbDataReader) = {
    Id          = r.GetGuid(0)
    CategoryId  = r.GetGuid(1)
    Name        = r.GetString(2)
    Description = if r.IsDBNull(3) then None else Some (r.GetString(3))
    Position    = r.GetInt32(4)
    ThreadCount = r.GetInt32(5)
    PostCount   = r.GetInt32(6)
    LastPostAt  = if r.IsDBNull(7) then None else Some (r.GetFieldValue<DateTime>(7))
    LastPostBy  = if r.IsDBNull(8) then None else Some (r.GetGuid(8))
    CreatedAt   = r.GetFieldValue<DateTime>(9)
}

let private readThread (r: DbDataReader) = {
    Id         = r.GetGuid(0)
    ForumId    = r.GetGuid(1)
    AuthorId   = r.GetGuid(2)
    Title      = r.GetString(3)
    IsPinned   = r.GetBoolean(4)
    IsLocked   = r.GetBoolean(5)
    PostCount  = r.GetInt32(6)
    ViewCount  = r.GetInt32(7)
    LastPostAt = if r.IsDBNull(8) then None else Some (r.GetFieldValue<DateTime>(8))
    LastPostBy = if r.IsDBNull(9) then None else Some (r.GetGuid(9))
    CreatedAt  = r.GetFieldValue<DateTime>(10)
    UpdatedAt  = r.GetFieldValue<DateTime>(11)
}

let private readPost (r: DbDataReader) = {
    Id        = r.GetGuid(0)
    ThreadId  = r.GetGuid(1)
    AuthorId  = r.GetGuid(2)
    Content   = r.GetString(3)
    IsAnswer  = r.GetBoolean(4)
    VoteCount = r.GetInt32(5)
    CreatedAt = r.GetFieldValue<DateTime>(6)
    UpdatedAt = r.GetFieldValue<DateTime>(7)
    DeletedAt = if r.IsDBNull(8) then None else Some (r.GetFieldValue<DateTime>(8))
}

// ── Categories ────────────────────────────────────────────────────────────────

let getCategories () =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT id, name, description, position, created_at FROM forum_categories ORDER BY position, name", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readCategory r ]

let createCategory (name: string) (description: string option) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO forum_categories (name, description)
           VALUES (@name, @desc)
           RETURNING id, name, description, position, created_at""", conn)
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("desc", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readCategory r) else None

// ── Forums ────────────────────────────────────────────────────────────────────

let getForumsByCategory (categoryId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, category_id, name, description, position, thread_count, post_count,
                  last_post_at, last_post_by, created_at
           FROM forum_forums WHERE category_id = @cid ORDER BY position, name""", conn)
    cmd.Parameters.AddWithValue("cid", categoryId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readForum r ]

let getForumById (forumId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, category_id, name, description, position, thread_count, post_count,
                  last_post_at, last_post_by, created_at
           FROM forum_forums WHERE id = @id""", conn)
    cmd.Parameters.AddWithValue("id", forumId) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readForum r) else None

let createForum (categoryId: Guid) (name: string) (description: string option) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO forum_forums (category_id, name, description)
           VALUES (@cid, @name, @desc)
           RETURNING id, category_id, name, description, position, thread_count, post_count,
                     last_post_at, last_post_by, created_at""", conn)
    cmd.Parameters.AddWithValue("cid", categoryId) |> ignore
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("desc", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readForum r) else None

// ── Threads ───────────────────────────────────────────────────────────────────

let getThreadsByForum (forumId: Guid) (page: int) (pageSize: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, forum_id, author_id, title, is_pinned, is_locked, post_count, view_count,
                  last_post_at, last_post_by, created_at, updated_at
           FROM forum_threads
           WHERE forum_id = @fid
           ORDER BY is_pinned DESC, last_post_at DESC NULLS LAST, created_at DESC
           LIMIT @limit OFFSET @offset""", conn)
    cmd.Parameters.AddWithValue("fid", forumId) |> ignore
    cmd.Parameters.AddWithValue("limit", pageSize) |> ignore
    cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readThread r ]

let getThreadById (threadId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, forum_id, author_id, title, is_pinned, is_locked, post_count, view_count,
                  last_post_at, last_post_by, created_at, updated_at
           FROM forum_threads WHERE id = @id""", conn)
    cmd.Parameters.AddWithValue("id", threadId) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readThread r) else None

let createThread (forumId: Guid) (authorId: Guid) (title: string) (content: string) =
    use conn = openConnection ()
    use txn = conn.BeginTransaction()
    try
        use threadCmd = new NpgsqlCommand(
            """INSERT INTO forum_threads (forum_id, author_id, title, post_count, last_post_at, last_post_by)
               VALUES (@fid, @aid, @title, 1, now(), @aid)
               RETURNING id, forum_id, author_id, title, is_pinned, is_locked, post_count, view_count,
                         last_post_at, last_post_by, created_at, updated_at""", conn, txn)
        threadCmd.Parameters.AddWithValue("fid", forumId) |> ignore
        threadCmd.Parameters.AddWithValue("aid", authorId) |> ignore
        threadCmd.Parameters.AddWithValue("title", title) |> ignore
        use r = threadCmd.ExecuteReader()
        if r.Read() then
            let thread = readThread r
            r.Close()
            use postCmd = new NpgsqlCommand(
                """INSERT INTO forum_posts (thread_id, author_id, content)
                   VALUES (@tid, @aid, @content)""", conn, txn)
            postCmd.Parameters.AddWithValue("tid", thread.Id) |> ignore
            postCmd.Parameters.AddWithValue("aid", authorId) |> ignore
            postCmd.Parameters.AddWithValue("content", content) |> ignore
            postCmd.ExecuteNonQuery() |> ignore
            use statsCmd = new NpgsqlCommand(
                """UPDATE forum_forums
                   SET thread_count = thread_count + 1,
                       post_count   = post_count + 1,
                       last_post_at = now(),
                       last_post_by = @aid
                   WHERE id = @fid""", conn, txn)
            statsCmd.Parameters.AddWithValue("aid", authorId) |> ignore
            statsCmd.Parameters.AddWithValue("fid", forumId) |> ignore
            statsCmd.ExecuteNonQuery() |> ignore
            txn.Commit()
            Some thread
        else
            txn.Rollback()
            None
    with ex ->
        txn.Rollback()
        raise ex

let incrementViewCount (threadId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "UPDATE forum_threads SET view_count = view_count + 1 WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", threadId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let setThreadPinned (threadId: Guid) (pinned: bool) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "UPDATE forum_threads SET is_pinned = @pinned WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("pinned", pinned) |> ignore
    cmd.Parameters.AddWithValue("id", threadId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let setThreadLocked (threadId: Guid) (locked: bool) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "UPDATE forum_threads SET is_locked = @locked WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("locked", locked) |> ignore
    cmd.Parameters.AddWithValue("id", threadId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

// ── Posts ─────────────────────────────────────────────────────────────────────

let getPostsByThread (threadId: Guid) (page: int) (pageSize: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, thread_id, author_id, content, is_answer, vote_count,
                  created_at, updated_at, deleted_at
           FROM forum_posts
           WHERE thread_id = @tid AND deleted_at IS NULL
           ORDER BY created_at ASC
           LIMIT @limit OFFSET @offset""", conn)
    cmd.Parameters.AddWithValue("tid", threadId) |> ignore
    cmd.Parameters.AddWithValue("limit", pageSize) |> ignore
    cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readPost r ]

let createPost (threadId: Guid) (authorId: Guid) (content: string) =
    use conn = openConnection ()
    use txn = conn.BeginTransaction()
    try
        use postCmd = new NpgsqlCommand(
            """INSERT INTO forum_posts (thread_id, author_id, content)
               VALUES (@tid, @aid, @content)
               RETURNING id, thread_id, author_id, content, is_answer, vote_count,
                         created_at, updated_at, deleted_at""", conn, txn)
        postCmd.Parameters.AddWithValue("tid", threadId) |> ignore
        postCmd.Parameters.AddWithValue("aid", authorId) |> ignore
        postCmd.Parameters.AddWithValue("content", content) |> ignore
        use r = postCmd.ExecuteReader()
        if r.Read() then
            let post = readPost r
            r.Close()
            use statsCmd = new NpgsqlCommand(
                """UPDATE forum_threads
                   SET post_count  = post_count + 1,
                       last_post_at = now(),
                       last_post_by = @aid,
                       updated_at   = now()
                   WHERE id = @tid""", conn, txn)
            statsCmd.Parameters.AddWithValue("aid", authorId) |> ignore
            statsCmd.Parameters.AddWithValue("tid", threadId) |> ignore
            statsCmd.ExecuteNonQuery() |> ignore
            txn.Commit()
            Some post
        else
            txn.Rollback()
            None
    with ex ->
        txn.Rollback()
        raise ex

let updatePost (postId: Guid) (authorId: Guid) (content: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE forum_posts SET content = @content, updated_at = now()
           WHERE id = @id AND author_id = @aid AND deleted_at IS NULL
           RETURNING id, thread_id, author_id, content, is_answer, vote_count,
                     created_at, updated_at, deleted_at""", conn)
    cmd.Parameters.AddWithValue("content", content) |> ignore
    cmd.Parameters.AddWithValue("id", postId) |> ignore
    cmd.Parameters.AddWithValue("aid", authorId) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readPost r) else None

let deletePost (postId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "UPDATE forum_posts SET deleted_at = now() WHERE id = @id AND deleted_at IS NULL", conn)
    cmd.Parameters.AddWithValue("id", postId) |> ignore
    cmd.ExecuteNonQuery() > 0

let markAsAnswer (postId: Guid) (threadId: Guid) =
    use conn = openConnection ()
    use txn = conn.BeginTransaction()
    use clearCmd = new NpgsqlCommand(
        "UPDATE forum_posts SET is_answer = FALSE WHERE thread_id = @tid", conn, txn)
    clearCmd.Parameters.AddWithValue("tid", threadId) |> ignore
    clearCmd.ExecuteNonQuery() |> ignore
    use setCmd = new NpgsqlCommand(
        "UPDATE forum_posts SET is_answer = TRUE WHERE id = @id", conn, txn)
    setCmd.Parameters.AddWithValue("id", postId) |> ignore
    setCmd.ExecuteNonQuery() |> ignore
    txn.Commit()

let votePost (postId: Guid) (userId: Guid) =
    use conn = openConnection ()
    use txn = conn.BeginTransaction()
    use voteCmd = new NpgsqlCommand(
        """INSERT INTO forum_post_votes (post_id, user_id)
           VALUES (@pid, @uid)
           ON CONFLICT (post_id, user_id) DO NOTHING""", conn, txn)
    voteCmd.Parameters.AddWithValue("pid", postId) |> ignore
    voteCmd.Parameters.AddWithValue("uid", userId) |> ignore
    let inserted = voteCmd.ExecuteNonQuery()
    if inserted > 0 then
        use countCmd = new NpgsqlCommand(
            "UPDATE forum_posts SET vote_count = vote_count + 1 WHERE id = @id", conn, txn)
        countCmd.Parameters.AddWithValue("id", postId) |> ignore
        countCmd.ExecuteNonQuery() |> ignore
    txn.Commit()
    inserted > 0

// ── Forum Tags ────────────────────────────────────────────────────────────────

type ForumTag = {
    Id          : Guid
    Name        : string
    Slug        : string
    Description : string option
    CreatedAt   : DateTime
}

let private readTag (r: DbDataReader) : ForumTag = {
    Id          = r.GetGuid(0)
    Name        = r.GetString(1)
    Slug        = r.GetString(2)
    Description = if r.IsDBNull(3) then None else Some(r.GetString(3))
    CreatedAt   = r.GetDateTime(4)
}

let getTags () : ForumTag list =
    use conn = openConnection ()
    use cmd  = new NpgsqlCommand(
        "SELECT id, name, slug, description, created_at FROM forum_tags ORDER BY name", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readTag r ]

let createTag (name: string) (slug: string) (description: string option) : ForumTag =
    use conn = openConnection ()
    use cmd  = new NpgsqlCommand(
        """INSERT INTO forum_tags (name, slug, description)
           VALUES (@name, @slug, @desc)
           RETURNING id, name, slug, description, created_at""", conn)
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    cmd.Parameters.AddWithValue("desc", if description.IsSome then box description.Value else box DBNull.Value) |> ignore
    use r = cmd.ExecuteReader()
    r.Read() |> ignore
    readTag r

let deleteTag (tagId: Guid) : bool =
    use conn = openConnection ()
    use cmd  = new NpgsqlCommand("DELETE FROM forum_tags WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", tagId) |> ignore
    cmd.ExecuteNonQuery() > 0

let getTagsForThread (threadId: Guid) : ForumTag list =
    use conn = openConnection ()
    use cmd  = new NpgsqlCommand(
        """SELECT t.id, t.name, t.slug, t.description, t.created_at
           FROM forum_tags t
           JOIN forum_thread_tags tt ON tt.tag_id = t.id
           WHERE tt.thread_id = @tid
           ORDER BY t.name""", conn)
    cmd.Parameters.AddWithValue("tid", threadId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readTag r ]

let setTagsForThread (threadId: Guid) (tagIds: Guid list) : unit =
    use conn = openConnection ()
    use txn  = conn.BeginTransaction()
    use delCmd = new NpgsqlCommand(
        "DELETE FROM forum_thread_tags WHERE thread_id = @tid", conn, txn)
    delCmd.Parameters.AddWithValue("tid", threadId) |> ignore
    delCmd.ExecuteNonQuery() |> ignore
    for tagId in tagIds do
        use insCmd = new NpgsqlCommand(
            "INSERT INTO forum_thread_tags (thread_id, tag_id) VALUES (@tid, @gid) ON CONFLICT DO NOTHING",
            conn, txn)
        insCmd.Parameters.AddWithValue("tid", threadId) |> ignore
        insCmd.Parameters.AddWithValue("gid", tagId)    |> ignore
        insCmd.ExecuteNonQuery() |> ignore
    txn.Commit()

// ── Post Reactions ────────────────────────────────────────────────────────────

type PostReaction = { Emoji: string; Count: int; UserReacted: bool }

let getReactions (postId: Guid) (userId: Guid option) : PostReaction list =
    use conn = openConnection ()
    use cmd  = new NpgsqlCommand(
        """SELECT emoji, COUNT(*) as count,
                  BOOL_OR(user_id = @uid) as user_reacted
           FROM forum_post_reactions
           WHERE post_id = @pid
           GROUP BY emoji
           ORDER BY count DESC""", conn)
    cmd.Parameters.AddWithValue("pid", postId) |> ignore
    cmd.Parameters.AddWithValue("uid", userId |> Option.defaultValue Guid.Empty |> box) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do
        yield { Emoji = r.GetString(0); Count = int (r.GetInt64(1)); UserReacted = r.GetBoolean(2) } ]

let toggleReaction (postId: Guid) (userId: Guid) (emoji: string) : bool =
    use conn = openConnection ()
    use txn  = conn.BeginTransaction()
    use checkCmd = new NpgsqlCommand(
        "SELECT 1 FROM forum_post_reactions WHERE post_id = @pid AND user_id = @uid AND emoji = @emoji",
        conn, txn)
    checkCmd.Parameters.AddWithValue("pid",   postId) |> ignore
    checkCmd.Parameters.AddWithValue("uid",   userId) |> ignore
    checkCmd.Parameters.AddWithValue("emoji", emoji)  |> ignore
    let exists = checkCmd.ExecuteScalar() <> null
    if exists then
        use delCmd = new NpgsqlCommand(
            "DELETE FROM forum_post_reactions WHERE post_id = @pid AND user_id = @uid AND emoji = @emoji",
            conn, txn)
        delCmd.Parameters.AddWithValue("pid",   postId) |> ignore
        delCmd.Parameters.AddWithValue("uid",   userId) |> ignore
        delCmd.Parameters.AddWithValue("emoji", emoji)  |> ignore
        delCmd.ExecuteNonQuery() |> ignore
    else
        use insCmd = new NpgsqlCommand(
            "INSERT INTO forum_post_reactions (post_id, user_id, emoji) VALUES (@pid, @uid, @emoji) ON CONFLICT DO NOTHING",
            conn, txn)
        insCmd.Parameters.AddWithValue("pid",   postId) |> ignore
        insCmd.Parameters.AddWithValue("uid",   userId) |> ignore
        insCmd.Parameters.AddWithValue("emoji", emoji)  |> ignore
        insCmd.ExecuteNonQuery() |> ignore
    txn.Commit()
    not exists

// ── Reports ───────────────────────────────────────────────────────────────────

type ForumReport = {
    Id:         Guid
    ReporterId: Guid
    TargetType: string
    TargetId:   Guid
    Reason:     string
    Status:     string
    ResolvedBy: Guid option
    ResolvedAt: DateTime option
    CreatedAt:  DateTime
}

let private readReport (r: DbDataReader) : ForumReport = {
    Id         = r.GetGuid(0)
    ReporterId = r.GetGuid(1)
    TargetType = r.GetString(2)
    TargetId   = r.GetGuid(3)
    Reason     = r.GetString(4)
    Status     = r.GetString(5)
    ResolvedBy = if r.IsDBNull(6) then None else Some (r.GetGuid(6))
    ResolvedAt = if r.IsDBNull(7) then None else Some (r.GetFieldValue<DateTime>(7))
    CreatedAt  = r.GetFieldValue<DateTime>(8)
}

let createReport (reporterId: Guid) (targetType: string) (targetId: Guid) (reason: string) : ForumReport option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO forum_reports (reporter_id, target_type, target_id, reason)
           VALUES (@rid, @tt, @tid, @reason)
           RETURNING id, reporter_id, target_type, target_id, reason, status, resolved_by, resolved_at, created_at""", conn)
    cmd.Parameters.AddWithValue("rid",    reporterId)  |> ignore
    cmd.Parameters.AddWithValue("tt",     targetType)  |> ignore
    cmd.Parameters.AddWithValue("tid",    targetId)    |> ignore
    cmd.Parameters.AddWithValue("reason", reason)      |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readReport r) else None

let getReports (status: string option) (page: int) (pageSize: int) : ForumReport list =
    use conn = openConnection ()
    let where = match status with Some s -> "WHERE status = @status" | None -> "WHERE status = 'open'"
    use cmd = new NpgsqlCommand(
        $"""SELECT id, reporter_id, target_type, target_id, reason, status, resolved_by, resolved_at, created_at
            FROM forum_reports {where}
            ORDER BY created_at DESC
            LIMIT @ps OFFSET @off""", conn)
    match status with Some s -> cmd.Parameters.AddWithValue("status", s) |> ignore | None -> ()
    cmd.Parameters.AddWithValue("ps",  pageSize)          |> ignore
    cmd.Parameters.AddWithValue("off", (page - 1) * pageSize) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readReport r ]

let resolveReport (reportId: Guid) (resolverId: Guid) (newStatus: string) : bool =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE forum_reports
           SET status = @status, resolved_by = @resolver, resolved_at = now()
           WHERE id = @id""", conn)
    cmd.Parameters.AddWithValue("status",   newStatus)  |> ignore
    cmd.Parameters.AddWithValue("resolver", resolverId) |> ignore
    cmd.Parameters.AddWithValue("id",       reportId)   |> ignore
    cmd.ExecuteNonQuery() > 0
