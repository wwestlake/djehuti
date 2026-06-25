module Djehuti.Api.AnnouncementRepository

open System
open System.Data.Common
open Npgsql
open Database

// ── Types ─────────────────────────────────────────────────────────────────────

type Announcement = {
    Id:          Guid
    Title:       string
    Body:        string
    Priority:    int
    AuthorId:    Guid option
    PublishedAt: DateTime option
    ExpiresAt:   DateTime option
    CreatedAt:   DateTime
    UpdatedAt:   DateTime
}

type AnnouncementSubscription = {
    Id:               Guid
    UserId:           Guid option
    Email:            string
    Confirmed:        bool
    UnsubscribeToken: string
    CreatedAt:        DateTime
}

// ── Helpers ───────────────────────────────────────────────────────────────────

let private opt (v: 'a option) : obj =
    v |> Option.map box |> Option.defaultValue (box DBNull.Value)

let private readAnnouncement (r: DbDataReader) : Announcement = {
    Id          = r.GetGuid(0)
    Title       = r.GetString(1)
    Body        = r.GetString(2)
    Priority    = r.GetInt32(3)
    AuthorId    = if r.IsDBNull(4) then None else Some (r.GetGuid(4))
    PublishedAt = if r.IsDBNull(5) then None else Some (r.GetFieldValue<DateTime>(5))
    ExpiresAt   = if r.IsDBNull(6) then None else Some (r.GetFieldValue<DateTime>(6))
    CreatedAt   = r.GetFieldValue<DateTime>(7)
    UpdatedAt   = r.GetFieldValue<DateTime>(8)
}

let private readSubscription (r: DbDataReader) : AnnouncementSubscription = {
    Id               = r.GetGuid(0)
    UserId           = if r.IsDBNull(1) then None else Some (r.GetGuid(1))
    Email            = r.GetString(2)
    Confirmed        = r.GetBoolean(3)
    UnsubscribeToken = r.GetString(4)
    CreatedAt        = r.GetFieldValue<DateTime>(5)
}

let private cols = "id, title, body, priority, author_id, published_at, expires_at, created_at, updated_at"
let private subCols = "id, user_id, email, confirmed, unsubscribe_token, created_at"

// ── Announcements CRUD ────────────────────────────────────────────────────────

let getPublished (limit: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""SELECT {cols} FROM announcements
            WHERE published_at IS NOT NULL AND published_at <= now()
              AND (expires_at IS NULL OR expires_at > now())
            ORDER BY priority DESC, published_at DESC
            LIMIT @lim""", conn)
    cmd.Parameters.AddWithValue("lim", limit) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readAnnouncement r ]

let getAll () =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"SELECT {cols} FROM announcements ORDER BY created_at DESC", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readAnnouncement r ]

let getById (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"SELECT {cols} FROM announcements WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readAnnouncement r) else None

let create (authorId: Guid) (title: string) (body: string) (priority: int) (expiresAt: DateTime option) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""INSERT INTO announcements (title, body, priority, author_id, expires_at)
            VALUES (@title, @body, @priority, @authorId, @expiresAt)
            RETURNING {cols}""", conn)
    cmd.Parameters.AddWithValue("title",    title)          |> ignore
    cmd.Parameters.AddWithValue("body",     body)           |> ignore
    cmd.Parameters.AddWithValue("priority", priority)       |> ignore
    cmd.Parameters.AddWithValue("authorId", authorId)       |> ignore
    cmd.Parameters.AddWithValue("expiresAt", opt expiresAt) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readAnnouncement r) else None

let update (id: Guid) (title: string) (body: string) (priority: int) (expiresAt: DateTime option) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""UPDATE announcements
            SET title = @title, body = @body, priority = @priority,
                expires_at = @expiresAt, updated_at = now()
            WHERE id = @id
            RETURNING {cols}""", conn)
    cmd.Parameters.AddWithValue("id",       id)             |> ignore
    cmd.Parameters.AddWithValue("title",    title)          |> ignore
    cmd.Parameters.AddWithValue("body",     body)           |> ignore
    cmd.Parameters.AddWithValue("priority", priority)       |> ignore
    cmd.Parameters.AddWithValue("expiresAt", opt expiresAt) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readAnnouncement r) else None

let publish (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""UPDATE announcements
            SET published_at = now(), updated_at = now()
            WHERE id = @id AND published_at IS NULL
            RETURNING {cols}""", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readAnnouncement r) else None

let unpublish (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""UPDATE announcements
            SET published_at = NULL, updated_at = now()
            WHERE id = @id
            RETURNING {cols}""", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readAnnouncement r) else None

let delete (id: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("DELETE FROM announcements WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0

// ── Subscriptions ─────────────────────────────────────────────────────────────

let subscribe (userId: Guid option) (email: string) (autoConfirm: bool) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""INSERT INTO announcement_subscriptions (user_id, email, confirmed)
            VALUES (@uid, @email, @confirmed)
            ON CONFLICT (email) DO UPDATE
                SET user_id = COALESCE(EXCLUDED.user_id, announcement_subscriptions.user_id),
                    confirmed = announcement_subscriptions.confirmed OR EXCLUDED.confirmed
            RETURNING {subCols}""", conn)
    cmd.Parameters.AddWithValue("uid",       opt userId)     |> ignore
    cmd.Parameters.AddWithValue("email",     email)          |> ignore
    cmd.Parameters.AddWithValue("confirmed", autoConfirm)    |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readSubscription r) else None

let confirmSubscription (token: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""UPDATE announcement_subscriptions
            SET confirmed = true
            WHERE confirm_token = @token
            RETURNING {subCols}""", conn)
    cmd.Parameters.AddWithValue("token", token) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readSubscription r) else None

let unsubscribeByToken (token: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "DELETE FROM announcement_subscriptions WHERE unsubscribe_token = @token", conn)
    cmd.Parameters.AddWithValue("token", token) |> ignore
    cmd.ExecuteNonQuery() > 0

let unsubscribeByEmail (email: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "DELETE FROM announcement_subscriptions WHERE email = @email", conn)
    cmd.Parameters.AddWithValue("email", email) |> ignore
    cmd.ExecuteNonQuery() > 0

let isSubscribed (email: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT confirmed FROM announcement_subscriptions WHERE email = @email", conn)
    cmd.Parameters.AddWithValue("email", email) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (r.GetBoolean(0)) else None

let getConfirmedSubscribers () =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"SELECT {subCols} FROM announcement_subscriptions WHERE confirmed = true", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readSubscription r ]

let getSubscriptionByEmail (email: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"SELECT {subCols} FROM announcement_subscriptions WHERE email = @email", conn)
    cmd.Parameters.AddWithValue("email", email) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readSubscription r) else None
