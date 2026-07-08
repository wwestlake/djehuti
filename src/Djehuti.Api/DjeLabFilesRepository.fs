module Djehuti.Api.DjeLabFilesRepository

open System
open System.Text.Json
open Amazon.S3
open Amazon.S3.Model
open Npgsql
open Database

// ── Types ────────────────────────────────────────────────────────────────────

type DjeLabFileEntry = {
    Id:          Guid
    IsFolder:    bool
    Path:        string
    ParentPath:  string
    Name:        string
    ContentType: string option
    SizeBytes:   int64 option
    CreatedAt:   DateTime
    UpdatedAt:   DateTime
}

type StorageUsage = {
    UsedBytes:  int64
    QuotaBytes: int64
    TierName:   string
}

// ── S3 config (mirrors MediaService.fs -- each module keeps its own small
//    env-reading helpers rather than sharing one, matching existing
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

// Keyed by the file's own id, not its user-facing name/path -- renaming or
// moving a file is then just a DB update, never an S3 rename (S3 has no
// atomic rename anyway).
let private s3KeyFor (userId: Guid) (fileId: Guid) = $"DjeLab/{userId}/{fileId}"

let private presignedGetUrl (s3Key: string) (expiryMinutes: int) : string =
    use client = makeS3Client ()
    let request = GetPreSignedUrlRequest(
        BucketName = bucket (),
        Key = s3Key,
        Verb = HttpVerb.GET,
        Expires = DateTime.UtcNow.AddMinutes(float expiryMinutes)
    )
    client.GetPreSignedURL(request)

// ── Quota ────────────────────────────────────────────────────────────────────

// Placeholder numbers, deliberately isolated to this one function -- easy to
// retune without touching anything else. Roughly follows the same shape as
// MudRepository's paidSlotsForTier.
let storageQuotaBytesForTier (tierId: string option) : int64 =
    let mb (n: int64) = n * 1024L * 1024L
    let gb (n: int64) = mb n * 1024L
    match tierId with
    | Some "curious-mind"    -> mb 100L
    | Some "lab-assistant"   -> mb 500L
    | Some "research-fellow" -> gb 2L
    | Some "professor"       -> gb 5L
    | Some "dean"            -> gb 20L
    | _                      -> mb 25L // Free

let getUsedBytes (conn: NpgsqlConnection) (userId: Guid) : int64 =
    use cmd = new NpgsqlCommand(
        "SELECT COALESCE(SUM(size_bytes), 0) FROM djelab_files WHERE user_id = @uid AND is_folder = FALSE", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    // Postgres's SUM(bigint) returns numeric, which Npgsql boxes as System.Decimal,
    // not int64 -- a direct `:?> int64` downcast throws on every call. Convert.ToInt64
    // handles the widening/narrowing conversion; file sizes are well within int64 range.
    Convert.ToInt64(cmd.ExecuteScalar())

let getStorageUsage (conn: NpgsqlConnection) (userId: Guid) : StorageUsage =
    let usedBytes = getUsedBytes conn userId
    match PatreonService.getTierLimits userId with
    | Some limits ->
        { UsedBytes = usedBytes; QuotaBytes = storageQuotaBytesForTier limits.tierId; TierName = limits.tierName }
    | None ->
        { UsedBytes = usedBytes; QuotaBytes = storageQuotaBytesForTier None; TierName = "Free" }

// ── Reading ──────────────────────────────────────────────────────────────────

let private readEntry (r: System.Data.Common.DbDataReader) : DjeLabFileEntry =
    {
        Id          = r.GetGuid(r.GetOrdinal("id"))
        IsFolder    = r.GetBoolean(r.GetOrdinal("is_folder"))
        Path        = r.GetString(r.GetOrdinal("path"))
        ParentPath  = r.GetString(r.GetOrdinal("parent_path"))
        Name        = r.GetString(r.GetOrdinal("name"))
        ContentType = let o = r.GetOrdinal("content_type") in if r.IsDBNull(o) then None else Some (r.GetString(o))
        SizeBytes   = let o = r.GetOrdinal("size_bytes") in if r.IsDBNull(o) then None else Some (r.GetInt64(o))
        CreatedAt   = r.GetFieldValue<DateTime>(r.GetOrdinal("created_at"))
        UpdatedAt   = r.GetFieldValue<DateTime>(r.GetOrdinal("updated_at"))
    }

let private entryColumns = "id, is_folder, path, parent_path, name, content_type, size_bytes, created_at, updated_at"

let listFolder (conn: NpgsqlConnection) (userId: Guid) (parentPath: string) : DjeLabFileEntry list =
    use cmd = new NpgsqlCommand(
        $"""SELECT {entryColumns} FROM djelab_files
            WHERE user_id = @uid AND parent_path = @parent
            ORDER BY is_folder DESC, name ASC""", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.Parameters.AddWithValue("parent", parentPath) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readEntry r ]

let getEntry (conn: NpgsqlConnection) (userId: Guid) (fileId: Guid) : DjeLabFileEntry option =
    use cmd = new NpgsqlCommand(
        $"SELECT {entryColumns} FROM djelab_files WHERE user_id = @uid AND id = @id", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.Parameters.AddWithValue("id", fileId) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readEntry r) else None

let private folderExists (conn: NpgsqlConnection) (userId: Guid) (path: string) : bool =
    if path = "/" then true
    else
        use cmd = new NpgsqlCommand(
            "SELECT 1 FROM djelab_files WHERE user_id = @uid AND path = @path AND is_folder = TRUE", conn)
        cmd.Parameters.AddWithValue("uid", userId) |> ignore
        cmd.Parameters.AddWithValue("path", path) |> ignore
        use r = cmd.ExecuteReader()
        r.Read()

let private joinPath (parentPath: string) (name: string) =
    if parentPath = "/" then "/" + name else parentPath + "/" + name

// ── Folders ──────────────────────────────────────────────────────────────────

let createFolder (conn: NpgsqlConnection) (userId: Guid) (parentPath: string) (name: string) : Result<DjeLabFileEntry, string> =
    let name = name.Trim()
    if String.IsNullOrWhiteSpace(name) || name.Contains("/") then
        Error "Folder names can't be empty or contain a slash."
    elif not (folderExists conn userId parentPath) then
        Error "The parent folder no longer exists."
    else
        let path = joinPath parentPath name
        try
            use cmd = new NpgsqlCommand(
                $"""INSERT INTO djelab_files (user_id, is_folder, path, parent_path, name)
                    VALUES (@uid, TRUE, @path, @parent, @name)
                    RETURNING {entryColumns}""", conn)
            cmd.Parameters.AddWithValue("uid", userId) |> ignore
            cmd.Parameters.AddWithValue("path", path) |> ignore
            cmd.Parameters.AddWithValue("parent", parentPath) |> ignore
            cmd.Parameters.AddWithValue("name", name) |> ignore
            use r = cmd.ExecuteReader()
            if r.Read() then Ok (readEntry r) else Error "Could not create the folder."
        with :? PostgresException as ex when ex.SqlState = "23505" ->
            Error "A folder or file with that name already exists here."

// ── Uploads ──────────────────────────────────────────────────────────────────

type UploadUrlResult = {
    FileId:       Guid
    PresignedUrl: string
    S3Key:        string
}

let requestUploadUrl
    (conn: NpgsqlConnection)
    (userId: Guid)
    (parentPath: string)
    (filename: string)
    (contentType: string)
    (declaredSizeBytes: int64)
    : Result<UploadUrlResult, string> =
    let filename = filename.Trim()
    if String.IsNullOrWhiteSpace(filename) || filename.Contains("/") then
        Error "File names can't be empty or contain a slash."
    elif not (folderExists conn userId parentPath) then
        Error "The target folder no longer exists."
    else
        let usage = getStorageUsage conn userId
        if usage.UsedBytes + declaredSizeBytes > usage.QuotaBytes then
            let mb (b: int64) = float b / 1024.0 / 1024.0
            Error $"That would exceed your storage quota ({mb usage.UsedBytes:F1} MB of {mb usage.QuotaBytes:F1} MB used)."
        else
            let fileId = Guid.NewGuid()
            let s3Key = s3KeyFor userId fileId
            let presignedUrl = MediaService.generatePresignedUploadUrl s3Key contentType 15
            Ok { FileId = fileId; PresignedUrl = presignedUrl; S3Key = s3Key }

let confirmUpload
    (conn: NpgsqlConnection)
    (userId: Guid)
    (fileId: Guid)
    (parentPath: string)
    (filename: string)
    (contentType: string)
    : Result<DjeLabFileEntry, string> =
    let s3Key = s3KeyFor userId fileId
    use client = makeS3Client ()
    let realSize =
        try
            let meta = client.GetObjectMetadataAsync(bucket (), s3Key) |> Async.AwaitTask |> Async.RunSynchronously
            Some meta.ContentLength
        with _ -> None

    match realSize with
    | None -> Error "Upload not found -- the file may not have finished uploading."
    | Some realSize ->
        let usage = getStorageUsage conn userId
        if usage.UsedBytes + realSize > usage.QuotaBytes then
            let req = DeleteObjectRequest(BucketName = bucket (), Key = s3Key)
            client.DeleteObjectAsync(req) |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            let mb (b: int64) = float b / 1024.0 / 1024.0
            Error $"That upload ({mb realSize:F1} MB) would exceed your storage quota ({mb usage.UsedBytes:F1} MB of {mb usage.QuotaBytes:F1} MB used). It has been removed."
        else
            let filename = filename.Trim()
            let path = joinPath parentPath filename
            try
                use cmd = new NpgsqlCommand(
                    $"""INSERT INTO djelab_files (id, user_id, is_folder, path, parent_path, name, s3_key, content_type, size_bytes)
                        VALUES (@id, @uid, FALSE, @path, @parent, @name, @key, @ct, @size)
                        RETURNING {entryColumns}""", conn)
                cmd.Parameters.AddWithValue("id", fileId) |> ignore
                cmd.Parameters.AddWithValue("uid", userId) |> ignore
                cmd.Parameters.AddWithValue("path", path) |> ignore
                cmd.Parameters.AddWithValue("parent", parentPath) |> ignore
                cmd.Parameters.AddWithValue("name", filename) |> ignore
                cmd.Parameters.AddWithValue("key", s3Key) |> ignore
                cmd.Parameters.AddWithValue("ct", contentType) |> ignore
                cmd.Parameters.AddWithValue("size", realSize) |> ignore
                use r = cmd.ExecuteReader()
                if r.Read() then Ok (readEntry r) else Error "Could not record the upload."
            with :? PostgresException as ex when ex.SqlState = "23505" ->
                let req = DeleteObjectRequest(BucketName = bucket (), Key = s3Key)
                client.DeleteObjectAsync(req) |> Async.AwaitTask |> Async.RunSynchronously |> ignore
                Error "A folder or file with that name already exists here."

// s3_key is deterministic from (userId, fileId) -- s3KeyFor -- so there's no
// need to store or query it separately just to build a download link.
let getDownloadUrl (userId: Guid) (entry: DjeLabFileEntry) : string option =
    if entry.IsFolder then None
    else Some (presignedGetUrl (s3KeyFor userId entry.Id) 15)

// ── Delete (files delete their S3 object; folders cascade to everything
//    inside them, matching ordinary file-explorer behavior) ──────────────────

let deleteEntry (conn: NpgsqlConnection) (userId: Guid) (fileId: Guid) : Result<unit, string> =
    match getEntry conn userId fileId with
    | None -> Error "Not found."
    | Some entry ->
        use client = makeS3Client ()

        let deleteS3 (s3Key: string) =
            let req = DeleteObjectRequest(BucketName = bucket (), Key = s3Key)
            client.DeleteObjectAsync(req) |> Async.AwaitTask |> Async.RunSynchronously |> ignore

        if entry.IsFolder then
            use descCmd = new NpgsqlCommand(
                """SELECT id FROM djelab_files
                   WHERE user_id = @uid AND is_folder = FALSE
                     AND (path LIKE @prefix)""", conn)
            descCmd.Parameters.AddWithValue("uid", userId) |> ignore
            descCmd.Parameters.AddWithValue("prefix", entry.Path + "/%") |> ignore
            let descendantFileIds =
                use r = descCmd.ExecuteReader()
                [ while r.Read() do yield r.GetGuid(0) ]
            for id in descendantFileIds do
                deleteS3 (s3KeyFor userId id)

            use delCmd = new NpgsqlCommand(
                "DELETE FROM djelab_files WHERE user_id = @uid AND (path = @path OR path LIKE @prefix)", conn)
            delCmd.Parameters.AddWithValue("uid", userId) |> ignore
            delCmd.Parameters.AddWithValue("path", entry.Path) |> ignore
            delCmd.Parameters.AddWithValue("prefix", entry.Path + "/%") |> ignore
            delCmd.ExecuteNonQuery() |> ignore
        else
            deleteS3 (s3KeyFor userId entry.Id)
            use delCmd = new NpgsqlCommand("DELETE FROM djelab_files WHERE user_id = @uid AND id = @id", conn)
            delCmd.Parameters.AddWithValue("uid", userId) |> ignore
            delCmd.Parameters.AddWithValue("id", fileId) |> ignore
            delCmd.ExecuteNonQuery() |> ignore

        Ok ()

// ── Hierarchy snapshots ─────────────────────────────────────────────────────

type HierarchicalDocumentSnapshot = {
    Id:             Guid
    UserId:         Guid
    SourceFileId:   Guid
    SourcePath:     string
    SourceKind:     string
    DocumentName:   string
    TreeJson:       string
    CreatedAt:      DateTime
    UpdatedAt:      DateTime
}

type HierarchicalNodeRecord = {
    Id:           Guid
    SnapshotId:   Guid
    ParentId:     Guid option
    NodePath:     string
    Name:         string
    Kind:         string
    ValueText:    string option
    MetadataJson: string
    SortOrder:    int
    CreatedAt:    DateTime
    UpdatedAt:    DateTime
}

let private readHierarchySnapshot (r: System.Data.Common.DbDataReader) : HierarchicalDocumentSnapshot =
    {
        Id            = r.GetGuid(r.GetOrdinal("id"))
        UserId        = r.GetGuid(r.GetOrdinal("user_id"))
        SourceFileId  = r.GetGuid(r.GetOrdinal("source_file_id"))
        SourcePath    = r.GetString(r.GetOrdinal("source_path"))
        SourceKind    = r.GetString(r.GetOrdinal("source_kind"))
        DocumentName  = r.GetString(r.GetOrdinal("document_name"))
        TreeJson      = r.GetString(r.GetOrdinal("tree_json"))
        CreatedAt     = r.GetFieldValue<DateTime>(r.GetOrdinal("created_at"))
        UpdatedAt     = r.GetFieldValue<DateTime>(r.GetOrdinal("updated_at"))
    }

let private readHierarchyNode (r: System.Data.Common.DbDataReader) : HierarchicalNodeRecord =
    {
        Id           = r.GetGuid(r.GetOrdinal("id"))
        SnapshotId   = r.GetGuid(r.GetOrdinal("snapshot_id"))
        ParentId     = let ordinal = r.GetOrdinal("parent_id") in if r.IsDBNull(ordinal) then None else Some (r.GetGuid(ordinal))
        NodePath     = r.GetString(r.GetOrdinal("node_path"))
        Name         = r.GetString(r.GetOrdinal("name"))
        Kind         = r.GetString(r.GetOrdinal("kind"))
        ValueText    = let ordinal = r.GetOrdinal("value_text") in if r.IsDBNull(ordinal) then None else Some (r.GetString(ordinal))
        MetadataJson = r.GetString(r.GetOrdinal("metadata_json"))
        SortOrder    = r.GetInt32(r.GetOrdinal("sort_order"))
        CreatedAt    = r.GetFieldValue<DateTime>(r.GetOrdinal("created_at"))
        UpdatedAt    = r.GetFieldValue<DateTime>(r.GetOrdinal("updated_at"))
    }

let private tryGetTextProperty (name: string) (element: JsonElement) : string option =
    let mutable property = Unchecked.defaultof<JsonElement>
    if element.ValueKind = JsonValueKind.Object && element.TryGetProperty(name, &property) then
        match property.ValueKind with
        | JsonValueKind.String -> Some (property.GetString())
        | JsonValueKind.Null
        | JsonValueKind.Undefined -> None
        | _ -> Some (property.GetRawText())
    else
        None

let private tryGetChildArray (element: JsonElement) : JsonElement option =
    let mutable children = Unchecked.defaultof<JsonElement>
    if element.ValueKind = JsonValueKind.Object && element.TryGetProperty("children", &children) && children.ValueKind = JsonValueKind.Array then
        Some children
    else
        None

let private tryGetMetadataJson (element: JsonElement) : string =
    let mutable metadata = Unchecked.defaultof<JsonElement>
    if element.ValueKind = JsonValueKind.Object && element.TryGetProperty("metadata", &metadata) then
        metadata.GetRawText()
    else
        "{}"

let private deleteHierarchyNodes (conn: NpgsqlConnection) (snapshotId: Guid) =
    use cmd = new NpgsqlCommand("DELETE FROM djelab_hierarchical_nodes WHERE snapshot_id = @sid", conn)
    cmd.Parameters.AddWithValue("sid", snapshotId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let private insertHierarchyNode
    (conn: NpgsqlConnection)
    (snapshotId: Guid)
    (parentId: Guid option)
    (nodePath: string)
    (element: JsonElement)
    (sortOrder: int)
    : HierarchicalNodeRecord =
    let name = tryGetTextProperty "name" element |> Option.defaultValue "node"
    let kind = tryGetTextProperty "kind" element |> Option.defaultValue "unknown"
    let valueText = tryGetTextProperty "value" element
    let metadataJson = tryGetMetadataJson element

    use cmd = new NpgsqlCommand(
        """INSERT INTO djelab_hierarchical_nodes
               (snapshot_id, parent_id, node_path, name, kind, value_text, metadata_json, sort_order)
           VALUES
               (@sid, @pid, @path, @name, @kind, @value, @meta::jsonb, @sort)
           RETURNING id, snapshot_id, parent_id, node_path, name, kind, value_text, metadata_json::text AS metadata_json, sort_order, created_at, updated_at""",
        conn)
    cmd.Parameters.AddWithValue("sid", snapshotId) |> ignore
    match parentId with
    | Some value -> cmd.Parameters.AddWithValue("pid", value) |> ignore
    | None -> cmd.Parameters.AddWithValue("pid", DBNull.Value) |> ignore
    cmd.Parameters.AddWithValue("path", nodePath) |> ignore
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("kind", kind) |> ignore
    match valueText with
    | Some value -> cmd.Parameters.AddWithValue("value", value) |> ignore
    | None -> cmd.Parameters.AddWithValue("value", DBNull.Value) |> ignore
    cmd.Parameters.AddWithValue("meta", metadataJson) |> ignore
    cmd.Parameters.AddWithValue("sort", sortOrder) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then readHierarchyNode reader else failwith "Could not store hierarchy node."

let private rebuildHierarchyNodes
    (conn: NpgsqlConnection)
    (snapshotId: Guid)
    (treeJson: string)
    =
    deleteHierarchyNodes conn snapshotId
    use document = JsonDocument.Parse(treeJson)

    let rec insertChildren (parentId: Guid option) (basePath: string) (element: JsonElement) =
        let name = tryGetTextProperty "name" element |> Option.defaultValue "node"
        let kind = tryGetTextProperty "kind" element |> Option.defaultValue "unknown"
        let currentPath =
            if String.IsNullOrWhiteSpace basePath then name
            else $"{basePath}/{name}"

        let record = insertHierarchyNode conn snapshotId parentId currentPath element 0

        match tryGetChildArray element with
        | None -> record
        | Some children ->
            let mutable index = 0
            for child in children.EnumerateArray() do
                let childName =
                    tryGetTextProperty "name" child
                    |> Option.defaultValue $"item-{index}"
                let childPath = $"{currentPath}/{childName}"
                let childRecord = insertHierarchyNode conn snapshotId (Some record.Id) childPath child index
                match tryGetChildArray child with
                | Some grandChildren ->
                    let rec walkChildren (ancestorId: Guid) (ancestorPath: string) (arrayElement: JsonElement) =
                        let mutable nestedIndex = 0
                        for nestedChild in arrayElement.EnumerateArray() do
                            let nestedName =
                                tryGetTextProperty "name" nestedChild
                                |> Option.defaultValue $"item-{nestedIndex}"
                            let nestedPath = $"{ancestorPath}/{nestedName}"
                            let nestedRecord = insertHierarchyNode conn snapshotId (Some ancestorId) nestedPath nestedChild nestedIndex
                            match tryGetChildArray nestedChild with
                            | Some deeper -> walkChildren nestedRecord.Id nestedPath deeper
                            | None -> ()
                            nestedIndex <- nestedIndex + 1
                    walkChildren childRecord.Id childPath grandChildren
                | None -> ()
                index <- index + 1
            record

    let root = document.RootElement
    let rootRecord = insertChildren None "" root
    rootRecord |> ignore

let upsertHierarchySnapshot
    (conn: NpgsqlConnection)
    (userId: Guid)
    (sourceFileId: Guid)
    (sourcePath: string)
    (sourceKind: string)
    (documentName: string)
    (treeJson: string)
    : Result<HierarchicalDocumentSnapshot, string> =
    try
        use cmd = new NpgsqlCommand(
            $"""INSERT INTO djelab_hierarchical_documents
                    (user_id, source_file_id, source_path, source_kind, document_name, tree_json)
                VALUES
                    (@uid, @fid, @path, @kind, @name, @tree::jsonb)
                ON CONFLICT (user_id, source_file_id)
                DO UPDATE SET
                    source_path = EXCLUDED.source_path,
                    source_kind = EXCLUDED.source_kind,
                    document_name = EXCLUDED.document_name,
                    tree_json = EXCLUDED.tree_json,
                    updated_at = NOW()
                RETURNING id, user_id, source_file_id, source_path, source_kind, document_name, tree_json::text AS tree_json, created_at, updated_at""",
            conn)
        cmd.Parameters.AddWithValue("uid", userId) |> ignore
        cmd.Parameters.AddWithValue("fid", sourceFileId) |> ignore
        cmd.Parameters.AddWithValue("path", sourcePath) |> ignore
        cmd.Parameters.AddWithValue("kind", sourceKind) |> ignore
        cmd.Parameters.AddWithValue("name", documentName) |> ignore
        cmd.Parameters.AddWithValue("tree", treeJson) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then
            let snapshot = readHierarchySnapshot reader
            reader.Close()
            rebuildHierarchyNodes conn snapshot.Id treeJson
            Ok snapshot
        else Error "Could not store the hierarchy snapshot."
    with ex ->
        Error ex.Message

let getHierarchySnapshot
    (conn: NpgsqlConnection)
    (userId: Guid)
    (sourceFileId: Guid)
    : HierarchicalDocumentSnapshot option =
    use cmd = new NpgsqlCommand(
        """SELECT id, user_id, source_file_id, source_path, source_kind, document_name, tree_json::text AS tree_json, created_at, updated_at
           FROM djelab_hierarchical_documents
           WHERE user_id = @uid AND source_file_id = @fid""",
        conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.Parameters.AddWithValue("fid", sourceFileId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readHierarchySnapshot reader) else None

let getHierarchyNodes
    (conn: NpgsqlConnection)
    (userId: Guid)
    (sourceFileId: Guid)
    : HierarchicalNodeRecord list =
    use cmd = new NpgsqlCommand(
        """SELECT n.id, n.snapshot_id, n.parent_id, n.node_path, n.name, n.kind, n.value_text, n.metadata_json::text AS metadata_json, n.sort_order, n.created_at, n.updated_at
           FROM djelab_hierarchical_nodes n
           INNER JOIN djelab_hierarchical_documents d ON d.id = n.snapshot_id
           WHERE d.user_id = @uid AND d.source_file_id = @fid
           ORDER BY n.node_path, n.sort_order""",
        conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.Parameters.AddWithValue("fid", sourceFileId) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do yield readHierarchyNode reader ]
