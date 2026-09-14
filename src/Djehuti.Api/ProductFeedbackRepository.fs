module Djehuti.Api.ProductFeedbackRepository

open System
open Npgsql
open Database

// ── Types ────────────────────────────────────────────────────────────────────

type FeedbackSubmission = {
    Message:    string
    Category:   string option
    InstallId:  string
    AppVersion: string option
    OsInfo:     string option
}

type MetricEvent = {
    EventType:   string
    EventName:   string
    PayloadJson: string option
}

// A batch shares one install/user/app-version/os context across all its
// events -- the client sends one context plus a list of events, not a
// context per event, matching how a session's metrics actually accumulate.
type MetricsBatch = {
    InstallId:  string
    AppVersion: string option
    OsInfo:     string option
    Events:     MetricEvent list
}

type FeedbackEntry = {
    Id:         Guid
    UserEmail:  string option
    InstallId:  string
    Message:    string
    Category:   string
    AppVersion: string
    OsInfo:     string
    CreatedAt:  DateTime
}

type MetricEventEntry = {
    Id:         Guid
    UserEmail:  string option
    InstallId:  string
    EventType:  string
    EventName:  string
    PayloadJson: string
    AppVersion: string
    OsInfo:     string
    CreatedAt:  DateTime
}

// ── Writes ───────────────────────────────────────────────────────────────────

let insertFeedback (productId: Guid) (userId: Guid option) (submission: FeedbackSubmission) : Guid =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        INSERT INTO product_feedback (product_id, user_id, install_id, message, category, app_version, os_info)
        VALUES (@productId, @userId, @installId, @message, @category, @appVersion, @osInfo)
        RETURNING id
    """, conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.Parameters.AddWithValue("userId", (userId |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("installId", submission.InstallId) |> ignore
    cmd.Parameters.AddWithValue("message", submission.Message) |> ignore
    cmd.Parameters.AddWithValue("category", (submission.Category |> Option.filter (String.IsNullOrWhiteSpace >> not) |> Option.defaultValue "general")) |> ignore
    cmd.Parameters.AddWithValue("appVersion", (submission.AppVersion |> Option.defaultValue "")) |> ignore
    cmd.Parameters.AddWithValue("osInfo", (submission.OsInfo |> Option.defaultValue "")) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    reader.GetGuid(0)

// Inserts every event in the batch under one connection. Not wrapped in an
// explicit transaction -- these are independent, append-only telemetry rows;
// a partial write on a mid-batch failure is acceptable (the caller reports
// how many actually landed) rather than discarding an entire valid batch
// over one bad event.
let insertMetricsBatch (productId: Guid) (userId: Guid option) (batch: MetricsBatch) : int =
    use conn = Database.openConnection()
    let mutable inserted = 0
    for ev in batch.Events do
        use cmd = new NpgsqlCommand("""
            INSERT INTO product_metrics_events
                (product_id, user_id, install_id, event_type, event_name, app_version, os_info, payload)
            VALUES
                (@productId, @userId, @installId, @eventType, @eventName, @appVersion, @osInfo, @payload::jsonb)
        """, conn)
        cmd.Parameters.AddWithValue("productId", productId) |> ignore
        cmd.Parameters.AddWithValue("userId", (userId |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
        cmd.Parameters.AddWithValue("installId", batch.InstallId) |> ignore
        cmd.Parameters.AddWithValue("eventType", ev.EventType) |> ignore
        cmd.Parameters.AddWithValue("eventName", ev.EventName) |> ignore
        cmd.Parameters.AddWithValue("appVersion", (batch.AppVersion |> Option.defaultValue "")) |> ignore
        cmd.Parameters.AddWithValue("osInfo", (batch.OsInfo |> Option.defaultValue "")) |> ignore
        cmd.Parameters.AddWithValue("payload", (ev.PayloadJson |> Option.filter (String.IsNullOrWhiteSpace >> not) |> Option.defaultValue "{}")) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        inserted <- inserted + 1
    inserted

// ── Admin review ─────────────────────────────────────────────────────────────

let listFeedback (productId: Guid) (limit: int) : FeedbackEntry list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT pf.id, u.email, pf.install_id, pf.message, pf.category, pf.app_version, pf.os_info, pf.created_at
        FROM product_feedback pf
        LEFT JOIN users u ON u.id = pf.user_id
        WHERE pf.product_id = @productId
        ORDER BY pf.created_at DESC
        LIMIT @limit
    """, conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.Parameters.AddWithValue("limit", limit) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        results <- {
            Id         = reader.GetGuid(0)
            UserEmail  = if reader.IsDBNull(1) then None else Some (reader.GetString(1))
            InstallId  = reader.GetString(2)
            Message    = reader.GetString(3)
            Category   = reader.GetString(4)
            AppVersion = reader.GetString(5)
            OsInfo     = reader.GetString(6)
            CreatedAt  = reader.GetFieldValue<DateTime>(7)
        } :: results
    List.rev results

let listMetrics (productId: Guid) (limit: int) : MetricEventEntry list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT me.id, u.email, me.install_id, me.event_type, me.event_name, me.payload::text, me.app_version, me.os_info, me.occurred_at
        FROM product_metrics_events me
        LEFT JOIN users u ON u.id = me.user_id
        WHERE me.product_id = @productId
        ORDER BY me.occurred_at DESC
        LIMIT @limit
    """, conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.Parameters.AddWithValue("limit", limit) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        results <- {
            Id          = reader.GetGuid(0)
            UserEmail   = if reader.IsDBNull(1) then None else Some (reader.GetString(1))
            InstallId   = reader.GetString(2)
            EventType   = reader.GetString(3)
            EventName   = reader.GetString(4)
            PayloadJson = reader.GetString(5)
            AppVersion  = reader.GetString(6)
            OsInfo      = reader.GetString(7)
            CreatedAt   = reader.GetFieldValue<DateTime>(8)
        } :: results
    List.rev results

// Coarse counts per event_type over the last 30 days -- enough for an admin
// at-a-glance summary without shipping every raw event to the browser.
let summarizeMetrics (productId: Guid) : (string * int) list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT event_type, COUNT(*)
        FROM product_metrics_events
        WHERE product_id = @productId AND occurred_at > now() - interval '30 days'
        GROUP BY event_type
        ORDER BY COUNT(*) DESC
    """, conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        results <- (reader.GetString(0), int (reader.GetInt64(1))) :: results
    List.rev results
