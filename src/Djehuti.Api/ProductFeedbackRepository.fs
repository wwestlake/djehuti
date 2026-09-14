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
