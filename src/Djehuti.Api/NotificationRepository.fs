module Djehuti.Api.NotificationRepository

open System
open System.Data.Common
open Npgsql
open Database

// ── Types ─────────────────────────────────────────────────────────────────────

type Notification = {
    Id:        Guid
    UserId:    Guid
    Type:      string
    Body:      string
    Link:      string option
    ReadAt:    DateTime option
    CreatedAt: DateTime
}

type Subscription = {
    Id:         Guid
    UserId:     Guid
    TargetType: string
    TargetId:   Guid
    Level:      string
    CreatedAt:  DateTime
}

// ── Helpers ───────────────────────────────────────────────────────────────────

let private readNotification (r: DbDataReader) : Notification = {
    Id        = r.GetGuid(0)
    UserId    = r.GetGuid(1)
    Type      = r.GetString(2)
    Body      = r.GetString(3)
    Link      = if r.IsDBNull(4) then None else Some (r.GetString(4))
    ReadAt    = if r.IsDBNull(5) then None else Some (r.GetFieldValue<DateTime>(5))
    CreatedAt = r.GetFieldValue<DateTime>(6)
}

let private readSubscription (r: DbDataReader) : Subscription = {
    Id         = r.GetGuid(0)
    UserId     = r.GetGuid(1)
    TargetType = r.GetString(2)
    TargetId   = r.GetGuid(3)
    Level      = r.GetString(4)
    CreatedAt  = r.GetFieldValue<DateTime>(5)
}

// ── Notifications ─────────────────────────────────────────────────────────────

let createNotification (userId: Guid) (notifType: string) (body: string) (link: string option) : Notification option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO notifications (user_id, type, body, link)
           VALUES (@uid, @type, @body, @link)
           RETURNING id, user_id, type, body, link, read_at, created_at""", conn)
    cmd.Parameters.AddWithValue("uid",  userId)                                                          |> ignore
    cmd.Parameters.AddWithValue("type", notifType)                                                       |> ignore
    cmd.Parameters.AddWithValue("body", body)                                                            |> ignore
    cmd.Parameters.AddWithValue("link", link |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readNotification r) else None

let getNotifications (userId: Guid) (page: int) (pageSize: int) : Notification list =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, user_id, type, body, link, read_at, created_at
           FROM notifications WHERE user_id = @uid
           ORDER BY created_at DESC
           LIMIT @ps OFFSET @off""", conn)
    cmd.Parameters.AddWithValue("uid", userId)                   |> ignore
    cmd.Parameters.AddWithValue("ps",  pageSize)                 |> ignore
    cmd.Parameters.AddWithValue("off", (page - 1) * pageSize)   |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readNotification r ]

let getUnreadCount (userId: Guid) : int =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT COUNT(*)::int FROM notifications WHERE user_id = @uid AND read_at IS NULL", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.ExecuteScalar() :?> int

let markRead (notifId: Guid) (userId: Guid) : bool =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "UPDATE notifications SET read_at = now() WHERE id = @id AND user_id = @uid AND read_at IS NULL", conn)
    cmd.Parameters.AddWithValue("id",  notifId) |> ignore
    cmd.Parameters.AddWithValue("uid", userId)  |> ignore
    cmd.ExecuteNonQuery() > 0

let markAllRead (userId: Guid) : unit =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "UPDATE notifications SET read_at = now() WHERE user_id = @uid AND read_at IS NULL", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

// ── Subscriptions ─────────────────────────────────────────────────────────────

let upsertSubscription (userId: Guid) (targetType: string) (targetId: Guid) (level: string) : Subscription option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO forum_subscriptions (user_id, target_type, target_id, level)
           VALUES (@uid, @tt, @tid, @level)
           ON CONFLICT (user_id, target_type, target_id)
           DO UPDATE SET level = EXCLUDED.level
           RETURNING id, user_id, target_type, target_id, level, created_at""", conn)
    cmd.Parameters.AddWithValue("uid",   userId)     |> ignore
    cmd.Parameters.AddWithValue("tt",    targetType) |> ignore
    cmd.Parameters.AddWithValue("tid",   targetId)   |> ignore
    cmd.Parameters.AddWithValue("level", level)      |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readSubscription r) else None

let getSubscription (userId: Guid) (targetType: string) (targetId: Guid) : Subscription option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, user_id, target_type, target_id, level, created_at
           FROM forum_subscriptions
           WHERE user_id = @uid AND target_type = @tt AND target_id = @tid""", conn)
    cmd.Parameters.AddWithValue("uid", userId)     |> ignore
    cmd.Parameters.AddWithValue("tt",  targetType) |> ignore
    cmd.Parameters.AddWithValue("tid", targetId)   |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readSubscription r) else None

let getSubscribersForThread (threadId: Guid) (minLevel: string) : Guid list =
    use conn = openConnection ()
    let levels = if minLevel = "watching" then "('watching')" else "('watching','tracking')"
    use cmd = new NpgsqlCommand(
        $"""SELECT user_id FROM forum_subscriptions
            WHERE target_type = 'thread' AND target_id = @tid AND level IN {levels}""", conn)
    cmd.Parameters.AddWithValue("tid", threadId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield r.GetGuid(0) ]

let notifySubscribers (threadId: Guid) (authorId: Guid) (body: string) (link: string) : unit =
    let subscribers = getSubscribersForThread threadId "tracking"
    for userId in subscribers do
        if userId <> authorId then
            createNotification userId "thread_reply" body (Some link) |> ignore
