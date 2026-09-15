module Djehuti.Api.FratePodRepository

open System
open System.Text.Json
open Amazon.S3
open Amazon.S3.Model
open Npgsql
open Database

// ── Types ────────────────────────────────────────────────────────────────────

type PodDependency = {
    name:    string
    version: string
}

type PodSummary = {
    name:          string
    description:   string option
    latestVersion: string
    license:       string
    exports:       string list
}

type PodVersionDetail = {
    Id:           Guid
    PodId:        Guid
    PodName:      string
    Version:      string
    Description:  string option
    Exports:      string list
    Dependencies: PodDependency list
    License:      string
    S3Key:        string
    SizeBytes:    int64
    PublisherId:  Guid
    CreatedAt:    DateTime
    Yanked:       bool
}

// Public pod-detail view (browse page): every published version, newest
// first, yanked ones included but flagged -- matches crates.io, where a
// yanked version stays visible on the pod page (so people can see it
// happened and why) even though it drops out of fresh dependency
// resolution.
type PodVersionSummary = {
    version:     string
    description: string option
    sizeBytes:   int64
    createdAt:   DateTime
    yanked:      bool
}

type PodDetail = {
    name:        string
    description: string option
    license:     string
    versions:    PodVersionSummary list
}

// Admin table row: one version, plus who published it by display name --
// never email, per this repo's no-email-in-UI rule (same fallback chain as
// PatreonService.getSupporters).
type PodVersionAdmin = {
    podName:            string
    version:            string
    description:        string option
    license:            string
    sizeBytes:          int64
    createdAt:          DateTime
    publisherDisplayName: string
    yanked:             bool
    yankedAt:           DateTime option
}

// ── License allow-list ──────────────────────────────────────────────────────
// Placeholder set of common OSS licenses. The user wants a real allow-list
// discussion separately (not yet had) -- adjust this set when that happens
// instead of re-deriving it.

let allowedLicenses =
    Set.ofList [
        "MIT"; "Apache-2.0"; "BSD-2-Clause"; "BSD-3-Clause"; "ISC"
        "MPL-2.0"; "LGPL-3.0-only"; "LGPL-3.0-or-later"
        "GPL-3.0-only"; "GPL-3.0-or-later"; "Unlicense"; "CC0-1.0"
    ]

let isAllowedLicense (license: string) =
    allowedLicenses.Contains(license.Trim())

// ── S3 config (mirrors MediaService.fs/DjeLabFilesRepository.fs -- each
//    module keeps its own small env-reading helpers, matching existing
//    convention in this codebase) ────────────────────────────────────────────

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

let private s3KeyFor (name: string) (version: string) = $"Frate/{name}/{version}.frpod"

let private presignedUploadUrl (s3Key: string) (expiryMinutes: int) : string =
    use client = makeS3Client ()
    let request = GetPreSignedUrlRequest(
        BucketName = bucket (),
        Key = s3Key,
        Verb = HttpVerb.PUT,
        ContentType = "application/zip",
        Expires = DateTime.UtcNow.AddMinutes(float expiryMinutes)
    )
    client.GetPreSignedURL(request)

let presignedDownloadUrl (s3Key: string) (expiryMinutes: int) : string =
    use client = makeS3Client ()
    let request = GetPreSignedUrlRequest(
        BucketName = bucket (),
        Key = s3Key,
        Verb = HttpVerb.GET,
        Expires = DateTime.UtcNow.AddMinutes(float expiryMinutes)
    )
    client.GetPreSignedURL(request)

// A pod name/version must be a single path segment -- both end up in the S3
// key (Frate/{name}/{version}.frpod) and in a local cache directory path
// (<cache>/{name}/{version}/ per FRATE_SPEC.md section 4), so '/' or '..'
// would either break that layout or let a key escape its own pod's prefix.
let private isValidSegment (s: string) =
    not (String.IsNullOrWhiteSpace s) && not (s.Contains("/")) && not (s.Contains(".."))

// ── DB reads ─────────────────────────────────────────────────────────────────

let private versionColumns =
    "v.id, v.pod_id, p.name, v.version, v.description, v.exports_json, v.dependencies_json, v.license, v.s3_key, v.size_bytes, v.publisher_id, v.created_at, v.yanked"

let private readVersion (r: System.Data.Common.DbDataReader) : PodVersionDetail =
    {
        Id           = r.GetGuid(0)
        PodId        = r.GetGuid(1)
        PodName      = r.GetString(2)
        Version      = r.GetString(3)
        Description  = if r.IsDBNull(4) then None else Some (r.GetString(4))
        Exports      = try JsonSerializer.Deserialize<string list>(r.GetString(5)) with _ -> []
        Dependencies = try JsonSerializer.Deserialize<PodDependency list>(r.GetString(6)) with _ -> []
        License      = r.GetString(7)
        S3Key        = r.GetString(8)
        SizeBytes    = r.GetInt64(9)
        PublisherId  = r.GetGuid(10)
        CreatedAt    = r.GetFieldValue<DateTime>(11)
        Yanked       = r.GetBoolean(12)
    }

// Every pod's most recently published NON-YANKED version, optionally
// filtered by a case-insensitive substring match on the pod name. Matches
// crates.io semantics: a yanked version never surfaces as "the" version to
// resolve against, but direct name+version lookup (getVersion below) still
// works for a build already pinned to it. A pod with every version yanked
// simply has no results here -- it still exists, just nothing fresh should
// depend on it.
let searchPods (query: string option) : PodSummary list =
    use conn = Database.openConnection()
    let trimmed = query |> Option.map (fun s -> s.Trim()) |> Option.filter (String.IsNullOrWhiteSpace >> not)
    let whereClause = if trimmed.IsSome then "WHERE p.name ILIKE @q AND v.yanked = false" else "WHERE v.yanked = false"
    use cmd = new NpgsqlCommand(
        $"""SELECT DISTINCT ON (p.id) {versionColumns}
            FROM frate_pod_versions v
            JOIN frate_pods p ON p.id = v.pod_id
            {whereClause}
            ORDER BY p.id, v.created_at DESC""", conn)
    if trimmed.IsSome then cmd.Parameters.AddWithValue("q", "%" + trimmed.Value + "%") |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        let v = readVersion reader
        results <- { name = v.PodName; description = v.Description; latestVersion = v.Version; license = v.License; exports = v.Exports } :: results
    List.rev results

// Public pod detail page: the pod's own description/license (from its
// latest version) plus every published version, yanked ones included and
// flagged -- so a browsing human can see the history, even though
// searchPods above hides yanked versions from resolution.
let getPodDetail (name: string) : PodDetail option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        $"""SELECT {versionColumns}
            FROM frate_pod_versions v
            JOIN frate_pods p ON p.id = v.pod_id
            WHERE lower(p.name) = lower(@name)
            ORDER BY v.created_at DESC""", conn)
    cmd.Parameters.AddWithValue("name", name) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable versions = []
    while reader.Read() do versions <- readVersion reader :: versions
    let versions = List.rev versions
    match versions with
    | [] -> None
    | latest :: _ ->
        Some {
            name        = latest.PodName
            description = latest.Description
            license     = latest.License
            versions    =
                versions
                |> List.map (fun v -> { version = v.Version; description = v.Description; sizeBytes = v.SizeBytes; createdAt = v.CreatedAt; yanked = v.Yanked })
        }

// Admin table: every version of every pod, newest first, publisher shown by
// display name only (never email -- see AGENTS.md).
let listAllPodsAdmin () : PodVersionAdmin list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT p.name, v.version, v.description, v.license, v.size_bytes, v.created_at,
                  COALESCE(up.display_name, u.display_name, 'Anonymous'), v.yanked, v.yanked_at
           FROM frate_pod_versions v
           JOIN frate_pods p ON p.id = v.pod_id
           JOIN users u ON u.id = v.publisher_id
           LEFT JOIN user_profiles up ON up.user_id = u.id
           ORDER BY p.name ASC, v.created_at DESC""", conn)
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        results <- {
            podName              = reader.GetString(0)
            version              = reader.GetString(1)
            description          = if reader.IsDBNull(2) then None else Some (reader.GetString(2))
            license              = reader.GetString(3)
            sizeBytes            = reader.GetInt64(4)
            createdAt            = reader.GetFieldValue<DateTime>(5)
            publisherDisplayName = reader.GetString(6)
            yanked               = reader.GetBoolean(7)
            yankedAt             = if reader.IsDBNull(8) then None else Some (reader.GetFieldValue<DateTime>(8))
        } :: results
    List.rev results

let setYanked (name: string) (version: string) (yanked: bool) (actorId: Guid) : Result<unit, string> =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        """UPDATE frate_pod_versions v
           SET yanked = @yanked,
               yanked_at = CASE WHEN @yanked THEN now() ELSE NULL END,
               yanked_by = CASE WHEN @yanked THEN @actorId ELSE NULL END
           FROM frate_pods p
           WHERE v.pod_id = p.id AND lower(p.name) = lower(@name) AND v.version = @version""", conn)
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("version", version) |> ignore
    cmd.Parameters.AddWithValue("yanked", yanked) |> ignore
    cmd.Parameters.AddWithValue("actorId", actorId) |> ignore
    if cmd.ExecuteNonQuery() > 0 then Ok () else Error "No such pod/version"

let getVersion (name: string) (version: string) : PodVersionDetail option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        $"""SELECT {versionColumns}
            FROM frate_pod_versions v
            JOIN frate_pods p ON p.id = v.pod_id
            WHERE lower(p.name) = lower(@name) AND v.version = @version""", conn)
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("version", version) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readVersion reader) else None

type private PodOwnership = {
    PodId:   Guid
    OwnerId: Guid
}

let private findPod (conn: NpgsqlConnection) (name: string) : PodOwnership option =
    use cmd = new NpgsqlCommand("SELECT id, owner_id FROM frate_pods WHERE lower(name) = lower(@name)", conn)
    cmd.Parameters.AddWithValue("name", name) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some { PodId = reader.GetGuid(0); OwnerId = reader.GetGuid(1) } else None

// ── Upload URL (no DB writes -- mirrors MediaService.generatePresignedUploadUrl's
//    pattern of handing back a key before anything is recorded) ─────────────

type UploadUrlRequest = {
    Name:        string
    Version:     string
    RequesterId: Guid
    IsAdmin:     bool
}

let requestUploadUrl (req: UploadUrlRequest) : Result<{| presignedUrl: string; s3Key: string |}, string> =
    if not (isValidSegment req.Name) || not (isValidSegment req.Version) then
        Error "Pod name and version must be non-empty and contain no '/' or '..'."
    else
        use conn = Database.openConnection()
        match findPod conn req.Name with
        | Some pod when pod.OwnerId <> req.RequesterId && not req.IsAdmin ->
            Error "This pod name is already owned by another publisher."
        | _ ->
            let s3Key = s3KeyFor req.Name req.Version
            Ok {| presignedUrl = presignedUploadUrl s3Key 15; s3Key = s3Key |}

// ── Publish ──────────────────────────────────────────────────────────────────

type PublishRequest = {
    Name:         string
    Version:      string
    Description:  string option
    Exports:      string list
    Dependencies: PodDependency list
    License:      string
    S3Key:        string
    SizeBytes:    int64
    PublisherId:  Guid
    IsAdmin:      bool
}

let publish (req: PublishRequest) : Result<PodVersionDetail, string> =
    if not (isValidSegment req.Name) || not (isValidSegment req.Version) then
        Error "Pod name and version must be non-empty and contain no '/' or '..'."
    elif not (isAllowedLicense req.License) then
        Error $"License '{req.License}' is not on the accepted list."
    else
        use conn = Database.openConnection()
        let existing = findPod conn req.Name
        match existing with
        | Some pod when pod.OwnerId <> req.PublisherId && not req.IsAdmin ->
            Error "This pod name is already owned by another publisher."
        | _ ->
            let podId =
                match existing with
                | Some pod -> pod.PodId
                | None ->
                    use cmd = new NpgsqlCommand(
                        "INSERT INTO frate_pods (name, description, owner_id) VALUES (@name, @desc, @owner) RETURNING id", conn)
                    cmd.Parameters.AddWithValue("name", req.Name) |> ignore
                    cmd.Parameters.AddWithValue("desc", (req.Description |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
                    cmd.Parameters.AddWithValue("owner", req.PublisherId) |> ignore
                    use reader = cmd.ExecuteReader()
                    reader.Read() |> ignore
                    reader.GetGuid(0)
            try
                use cmd = new NpgsqlCommand(
                    """INSERT INTO frate_pod_versions
                           (pod_id, version, description, exports_json, dependencies_json, license, s3_key, size_bytes, publisher_id)
                       VALUES (@podId, @version, @desc, @exports, @deps, @license, @key, @size, @publisher)
                       RETURNING id, created_at""", conn)
                cmd.Parameters.AddWithValue("podId", podId) |> ignore
                cmd.Parameters.AddWithValue("version", req.Version) |> ignore
                cmd.Parameters.AddWithValue("desc", (req.Description |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
                cmd.Parameters.AddWithValue("exports", JsonSerializer.Serialize(req.Exports)) |> ignore
                cmd.Parameters.AddWithValue("deps", JsonSerializer.Serialize(req.Dependencies)) |> ignore
                cmd.Parameters.AddWithValue("license", req.License) |> ignore
                cmd.Parameters.AddWithValue("key", req.S3Key) |> ignore
                cmd.Parameters.AddWithValue("size", req.SizeBytes) |> ignore
                cmd.Parameters.AddWithValue("publisher", req.PublisherId) |> ignore
                use reader = cmd.ExecuteReader()
                if reader.Read() then
                    Ok {
                        Id           = reader.GetGuid(0)
                        PodId        = podId
                        PodName      = req.Name
                        Version      = req.Version
                        Description  = req.Description
                        Exports      = req.Exports
                        Dependencies = req.Dependencies
                        License      = req.License
                        S3Key        = req.S3Key
                        SizeBytes    = req.SizeBytes
                        PublisherId  = req.PublisherId
                        CreatedAt    = reader.GetFieldValue<DateTime>(1)
                        Yanked       = false
                    }
                else
                    Error "Could not record the published version."
            with :? PostgresException as ex when ex.SqlState = "23505" ->
                Error $"Version {req.Version} of '{req.Name}' has already been published."
