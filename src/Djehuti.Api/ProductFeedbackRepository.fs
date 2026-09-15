module Djehuti.Api.ProductFeedbackRepository

open System
open Amazon.S3
open Amazon.S3.Model
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

type AttachmentInfo = {
    fileName:    string
    contentType: string
    sizeBytes:   int64
    url:         string  // short-lived presigned GET, generated at read time
}

// UserDisplayName, never an email -- per AGENTS.md, email addresses are
// never displayed in the UI. 'Anonymous' covers both an anonymous
// submitter (no user_id) and a signed-in user with no display name set.
type FeedbackEntry = {
    Id:              Guid
    UserDisplayName: string
    InstallId:       string
    Message:         string
    Category:        string
    AppVersion:      string
    OsInfo:          string
    CreatedAt:       DateTime
    Attachments:     AttachmentInfo list
}

type MetricEventEntry = {
    Id:              Guid
    UserDisplayName: string
    InstallId:       string
    EventType:       string
    EventName:       string
    PayloadJson:     string
    AppVersion:      string
    OsInfo:          string
    CreatedAt:       DateTime
}

// ── S3 config (mirrors MediaService.fs/DjeLabFilesRepository.fs -- the same
//    shared S3_BUCKET, not a dedicated one -- this is ordinary
//    user-submitted media, the same kind of thing that bucket already
//    holds) ────────────────────────────────────────────────────────────────

let private bucket () =
    let b = Environment.GetEnvironmentVariable("S3_BUCKET")
    if String.IsNullOrWhiteSpace(b) then failwith "S3_BUCKET not set"
    b

let private region () =
    let r = Environment.GetEnvironmentVariable("S3_REGION")
    if String.IsNullOrWhiteSpace(r) then "us-east-1" else r

let private makeS3Client () =
    let r = Amazon.RegionEndpoint.GetBySystemName(region ())
    new AmazonS3Client(r)

let private s3KeyFor (feedbackId: Guid) (fileName: string) =
    $"feedback-attachments/{feedbackId}/{Guid.NewGuid()}-{fileName}"

// Deliberately does NOT set ContentType on the presigned request -- doing
// so bakes "content-type" into the required SigV4 signed headers, which
// then demands the uploading client send that exact header or S3 rejects
// the PUT with SignatureDoesNotMatch. Proven the hard way in
// FratePodRepository.fs; not repeating it here.
let private presignedUploadUrl (s3Key: string) (expiryMinutes: int) : string =
    use client = makeS3Client ()
    let request = GetPreSignedUrlRequest(
        BucketName = bucket (),
        Key = s3Key,
        Verb = HttpVerb.PUT,
        Expires = DateTime.UtcNow.AddMinutes(float expiryMinutes)
    )
    client.GetPreSignedURL(request)

let private presignedDownloadUrl (s3Key: string) (expiryMinutes: int) : string =
    use client = makeS3Client ()
    let request = GetPreSignedUrlRequest(
        BucketName = bucket (),
        Key = s3Key,
        Verb = HttpVerb.GET,
        Expires = DateTime.UtcNow.AddMinutes(float expiryMinutes)
    )
    client.GetPreSignedURL(request)

// A valid segment (path component of s3KeyFor) -- not attacker-controlled
// bucket-key-injection surface, same reasoning as FratePodRepository's
// isValidSegment for pod name/version.
let private isValidFileName (s: string) =
    not (String.IsNullOrWhiteSpace s) && not (s.Contains("/")) && not (s.Contains(".."))

// ── Attachments ──────────────────────────────────────────────────────────────

// Two-step upload, same shape as Frate's publish flow: get a presigned PUT
// URL first, client uploads directly to S3, then calls recordAttachment to
// register it. feedbackId must already exist (created via insertFeedback)
// so an attachment is never orphaned from real feedback text.
let requestAttachmentUploadUrl (feedbackId: Guid) (fileName: string) : Result<{| presignedUrl: string; s3Key: string |}, string> =
    if not (isValidFileName fileName) then
        Error "File name must be non-empty and contain no '/' or '..'."
    else
        let s3Key = s3KeyFor feedbackId fileName
        Ok {| presignedUrl = presignedUploadUrl s3Key 15; s3Key = s3Key |}

let recordAttachment (feedbackId: Guid) (fileName: string) (contentType: string) (sizeBytes: int64) (s3Key: string) : Guid =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        INSERT INTO product_feedback_attachments (feedback_id, file_name, content_type, size_bytes, s3_key)
        VALUES (@feedbackId, @fileName, @contentType, @sizeBytes, @s3Key)
        RETURNING id
    """, conn)
    cmd.Parameters.AddWithValue("feedbackId", feedbackId) |> ignore
    cmd.Parameters.AddWithValue("fileName", fileName) |> ignore
    cmd.Parameters.AddWithValue("contentType", contentType) |> ignore
    cmd.Parameters.AddWithValue("sizeBytes", sizeBytes) |> ignore
    cmd.Parameters.AddWithValue("s3Key", s3Key) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    reader.GetGuid(0)

// feedbackId is only trusted after confirming it actually belongs to this
// product -- callers pass the product-scoped feedback list they already
// have, not a bare guid from the client.
let feedbackBelongsToProduct (productId: Guid) (feedbackId: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        "SELECT 1 FROM product_feedback WHERE id = @feedbackId AND product_id = @productId", conn)
    cmd.Parameters.AddWithValue("feedbackId", feedbackId) |> ignore
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    let scalar = cmd.ExecuteScalar()
    not (isNull scalar)

let private listAttachments (feedbackId: Guid) : AttachmentInfo list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        "SELECT file_name, content_type, size_bytes, s3_key FROM product_feedback_attachments WHERE feedback_id = @feedbackId ORDER BY created_at ASC", conn)
    cmd.Parameters.AddWithValue("feedbackId", feedbackId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        results <- {
            fileName    = reader.GetString(0)
            contentType = reader.GetString(1)
            sizeBytes   = reader.GetInt64(2)
            url         = presignedDownloadUrl (reader.GetString(3)) 15
        } :: results
    List.rev results

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
        SELECT pf.id, COALESCE(up.display_name, u.display_name, 'Anonymous'), pf.install_id, pf.message, pf.category, pf.app_version, pf.os_info, pf.created_at
        FROM product_feedback pf
        LEFT JOIN users u ON u.id = pf.user_id
        LEFT JOIN user_profiles up ON up.user_id = u.id
        WHERE pf.product_id = @productId
        ORDER BY pf.created_at DESC
        LIMIT @limit
    """, conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.Parameters.AddWithValue("limit", limit) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        let id = reader.GetGuid(0)
        results <- {
            Id              = id
            UserDisplayName = reader.GetString(1)
            InstallId       = reader.GetString(2)
            Message         = reader.GetString(3)
            Category        = reader.GetString(4)
            AppVersion      = reader.GetString(5)
            OsInfo          = reader.GetString(6)
            CreatedAt       = reader.GetFieldValue<DateTime>(7)
            // A separate connection (listAttachments opens its own), so
            // this is safe to call while the outer reader above is still
            // open -- not sharing a connection with it.
            Attachments     = listAttachments id
        } :: results
    List.rev results

let listMetrics (productId: Guid) (limit: int) : MetricEventEntry list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT me.id, COALESCE(up.display_name, u.display_name, 'Anonymous'), me.install_id, me.event_type, me.event_name, me.payload::text, me.app_version, me.os_info, me.occurred_at
        FROM product_metrics_events me
        LEFT JOIN users u ON u.id = me.user_id
        LEFT JOIN user_profiles up ON up.user_id = u.id
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
            Id              = reader.GetGuid(0)
            UserDisplayName = reader.GetString(1)
            InstallId       = reader.GetString(2)
            EventType       = reader.GetString(3)
            EventName       = reader.GetString(4)
            PayloadJson     = reader.GetString(5)
            AppVersion      = reader.GetString(6)
            OsInfo          = reader.GetString(7)
            CreatedAt       = reader.GetFieldValue<DateTime>(8)
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
