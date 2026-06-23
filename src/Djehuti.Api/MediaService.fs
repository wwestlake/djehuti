module Djehuti.Api.MediaService

open System
open System.Net
open Amazon.S3
open Amazon.S3.Model
open Npgsql
open Database

// ── Types ─────────────────────────────────────────────────────────────────────

type MediaRecord = {
    Id:          Guid
    UploaderId:  Guid
    Module:      string
    ContextId:   Guid option
    S3Key:       string
    Url:         string
    Filename:    string
    ContentType: string
    SizeBytes:   int64 option
    CreatedAt:   DateTime
}

// ── S3 Config ────────────────────────────────────────────────────────────────

let private bucket () =
    let b = Environment.GetEnvironmentVariable("S3_BUCKET")
    if String.IsNullOrWhiteSpace(b) then failwith "S3_BUCKET not set"
    b

let private region () =
    let r = Environment.GetEnvironmentVariable("S3_REGION")
    if String.IsNullOrWhiteSpace(r) then "us-east-1" else r

let private s3BaseUrl () =
    let b = bucket ()
    let r = region ()
    $"https://{b}.s3.{r}.amazonaws.com"

let private makeS3Client () =
    let r = Amazon.RegionEndpoint.GetBySystemName(region ())
    new AmazonS3Client(r)

// ── Presigned Upload URL ──────────────────────────────────────────────────────

let generatePresignedUploadUrl (s3Key: string) (contentType: string) (expiryMinutes: int) : string =
    use client = makeS3Client ()
    let request = GetPreSignedUrlRequest(
        BucketName = bucket (),
        Key = s3Key,
        Verb = HttpVerb.PUT,
        ContentType = contentType,
        Expires = DateTime.UtcNow.AddMinutes(float expiryMinutes)
    )
    client.GetPreSignedURL(request)

// ── DB: record uploaded media ────────────────────────────────────────────────

let recordMedia (conn: NpgsqlConnection) (uploaderId: Guid) (mdl: string) (contextId: Guid option) (s3Key: string) (filename: string) (contentType: string) (sizeBytes: int64 option) : MediaRecord option =
    let url = $"{s3BaseUrl ()}/{s3Key}"
    use cmd = new NpgsqlCommand(
        """INSERT INTO media (uploader_id, module, context_id, s3_key, url, filename, content_type, size_bytes)
           VALUES (@uid, @module, @ctx, @key, @url, @filename, @ct, @size)
           RETURNING id, uploader_id, module, context_id, s3_key, url, filename, content_type, size_bytes, created_at""", conn)
    cmd.Parameters.AddWithValue("uid", uploaderId) |> ignore
    cmd.Parameters.AddWithValue("module", mdl) |> ignore
    cmd.Parameters.AddWithValue("ctx", contextId |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("key", s3Key) |> ignore
    cmd.Parameters.AddWithValue("url", url) |> ignore
    cmd.Parameters.AddWithValue("filename", filename) |> ignore
    cmd.Parameters.AddWithValue("ct", contentType) |> ignore
    cmd.Parameters.AddWithValue("size", sizeBytes |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then
        Some {
            Id          = r.GetGuid(0)
            UploaderId  = r.GetGuid(1)
            Module      = r.GetString(2)
            ContextId   = if r.IsDBNull(3) then None else Some (r.GetGuid(3))
            S3Key       = r.GetString(4)
            Url         = r.GetString(5)
            Filename    = r.GetString(6)
            ContentType = r.GetString(7)
            SizeBytes   = if r.IsDBNull(8) then None else Some (r.GetInt64(8))
            CreatedAt   = r.GetFieldValue<DateTime>(9)
        }
    else None

let getMediaByContext (conn: NpgsqlConnection) (mdl: string) (contextId: Guid) : MediaRecord list =
    use cmd = new NpgsqlCommand(
        """SELECT id, uploader_id, module, context_id, s3_key, url, filename, content_type, size_bytes, created_at
           FROM media WHERE module = @module AND context_id = @ctx ORDER BY created_at DESC""", conn)
    cmd.Parameters.AddWithValue("module", mdl) |> ignore
    cmd.Parameters.AddWithValue("ctx", contextId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do
        yield {
            Id          = r.GetGuid(0)
            UploaderId  = r.GetGuid(1)
            Module      = r.GetString(2)
            ContextId   = if r.IsDBNull(3) then None else Some (r.GetGuid(3))
            S3Key       = r.GetString(4)
            Url         = r.GetString(5)
            Filename    = r.GetString(6)
            ContentType = r.GetString(7)
            SizeBytes   = if r.IsDBNull(8) then None else Some (r.GetInt64(8))
            CreatedAt   = r.GetFieldValue<DateTime>(9)
        } ]

// ── Delete from S3 + DB ───────────────────────────────────────────────────────

let deleteMedia (conn: NpgsqlConnection) (mediaId: Guid) (requesterId: Guid) (isAdmin: bool) =
    use getCmd = new NpgsqlCommand(
        "SELECT s3_key, uploader_id FROM media WHERE id = @id", conn)
    getCmd.Parameters.AddWithValue("id", mediaId) |> ignore
    use r = getCmd.ExecuteReader()
    if r.Read() then
        let s3Key = r.GetString(0)
        let ownerId = r.GetGuid(1)
        r.Close()
        if ownerId = requesterId || isAdmin then
            use client = makeS3Client ()
            let req = DeleteObjectRequest(BucketName = bucket (), Key = s3Key)
            client.DeleteObjectAsync(req) |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            use delCmd = new NpgsqlCommand("DELETE FROM media WHERE id = @id", conn)
            delCmd.Parameters.AddWithValue("id", mediaId) |> ignore
            delCmd.ExecuteNonQuery() > 0
        else false
    else false

// ── Allowed content types ────────────────────────────────────────────────────

let allowedContentTypes = Set.ofList [
    "image/jpeg"; "image/png"; "image/gif"; "image/webp"; "image/svg+xml"
    "application/pdf"
]

let isAllowedContentType (ct: string) = Set.contains ct allowedContentTypes
