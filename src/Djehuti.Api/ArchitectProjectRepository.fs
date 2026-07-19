module Djehuti.Api.ArchitectProjectRepository

open System
open Npgsql
open Amazon.S3
open Amazon.S3.Model

// ── Types ────────────────────────────────────────────────────────────────────

type ProjectSummary = {
    Id:        Guid
    Name:      string
    UpdatedAt: DateTime
}

type ProjectRecord = {
    Id:          Guid
    UserId:      Guid
    Name:        string
    Description: string option
    CreatedAt:   DateTime
    UpdatedAt:   DateTime
}

type FileEntry = {
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

// ── Tier gate ────────────────────────────────────────────────────────────────

// Cloud projects (server-persisted, S3-backed) are a paid-tier feature -- any
// Patreon tier at all qualifies. Free-tier users work entirely client-side
// (in the browser) and download/reopen a local file instead; they never call
// this API's write endpoints, so there's no "reduced quota" case here like
// DjeLabFilesRepository's storage quota -- it's paid or nothing.
let isPaidTier (userId: Guid) : bool =
    match PatreonService.getTierLimits userId with
    | Some limits -> limits.tierId.IsSome
    | None -> false

// ── S3 config (mirrors DjeLabFilesRepository/MediaService -- each module
//    keeps its own small env-reading helpers, matching existing convention
//    in this codebase) ─────────────────────────────────────────────────────

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

// Keyed by the file's own id, not its user-facing path -- renaming or moving
// a file is then just a DB update, never an S3 rename.
let private s3KeyFor (userId: Guid) (projectId: Guid) (fileId: Guid) = $"Architect/{userId}/{projectId}/{fileId}"

// Architect files are small hand-edited JSON/text documents, not arbitrary
// binary uploads -- a direct server-side PutObject is simpler and sufficient
// here, unlike DjeLabFilesRepository's presigned-URL-then-confirm dance
// (which exists for large binary files where routing bytes through the API
// server would be wasteful).
let private putTextObject (s3Key: string) (contentType: string) (content: string) : int64 =
    use client = makeS3Client ()
    let bytes = System.Text.Encoding.UTF8.GetBytes(content)
    use stream = new System.IO.MemoryStream(bytes)
    let req = PutObjectRequest(BucketName = bucket (), Key = s3Key, InputStream = stream, ContentType = contentType)
    client.PutObjectAsync(req) |> Async.AwaitTask |> Async.RunSynchronously |> ignore
    int64 bytes.Length

let private getTextObject (s3Key: string) : string option =
    use client = makeS3Client ()
    try
        let resp = client.GetObjectAsync(bucket (), s3Key) |> Async.AwaitTask |> Async.RunSynchronously
        use reader = new System.IO.StreamReader(resp.ResponseStream)
        Some (reader.ReadToEnd())
    with _ -> None

let private deleteObject (s3Key: string) =
    use client = makeS3Client ()
    let req = DeleteObjectRequest(BucketName = bucket (), Key = s3Key)
    client.DeleteObjectAsync(req) |> Async.AwaitTask |> Async.RunSynchronously |> ignore

// ── Projects ─────────────────────────────────────────────────────────────────

let private readProjectSummary (r: System.Data.Common.DbDataReader) : ProjectSummary =
    { Id = r.GetGuid(0); Name = r.GetString(1); UpdatedAt = r.GetFieldValue<DateTime>(2) }

let private readProjectRecord (r: System.Data.Common.DbDataReader) : ProjectRecord =
    {
        Id          = r.GetGuid(0)
        UserId      = r.GetGuid(1)
        Name        = r.GetString(2)
        Description = if r.IsDBNull(3) then None else Some (r.GetString(3))
        CreatedAt   = r.GetFieldValue<DateTime>(4)
        UpdatedAt   = r.GetFieldValue<DateTime>(5)
    }

let listProjects (conn: NpgsqlConnection) (userId: Guid) : ProjectSummary list =
    use cmd = new NpgsqlCommand(
        "SELECT id, name, updated_at FROM architect_projects WHERE user_id = @uid ORDER BY updated_at DESC", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readProjectSummary r ]

let getProject (conn: NpgsqlConnection) (userId: Guid) (projectId: Guid) : ProjectRecord option =
    use cmd = new NpgsqlCommand(
        "SELECT id, user_id, name, description, created_at, updated_at FROM architect_projects WHERE user_id = @uid AND id = @id", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.Parameters.AddWithValue("id", projectId) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readProjectRecord r) else None

let createProject (conn: NpgsqlConnection) (userId: Guid) (name: string) (description: string option) : Result<ProjectRecord, string> =
    let name = name.Trim()
    if not (isPaidTier userId) then
        Error "Cloud projects need a paid Patreon tier -- work locally instead and use Download Project to save your progress."
    elif String.IsNullOrWhiteSpace(name) then
        Error "Give the project a name."
    else
        use cmd = new NpgsqlCommand(
            """INSERT INTO architect_projects (user_id, name, description)
               VALUES (@uid, @name, @desc)
               RETURNING id, user_id, name, description, created_at, updated_at""", conn)
        cmd.Parameters.AddWithValue("uid", userId) |> ignore
        cmd.Parameters.AddWithValue("name", name) |> ignore
        cmd.Parameters.AddWithValue("desc", (description |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
        use r = cmd.ExecuteReader()
        if r.Read() then Ok (readProjectRecord r) else Error "Could not create the project."

let deleteProject (conn: NpgsqlConnection) (userId: Guid) (projectId: Guid) : bool =
    match getProject conn userId projectId with
    | None -> false
    | Some _ ->
        use listCmd = new NpgsqlCommand(
            "SELECT s3_key FROM architect_files WHERE project_id = @pid AND s3_key IS NOT NULL", conn)
        listCmd.Parameters.AddWithValue("pid", projectId) |> ignore
        let keys =
            use r = listCmd.ExecuteReader()
            [ while r.Read() do yield r.GetString(0) ]
        for key in keys do deleteObject key

        use cmd = new NpgsqlCommand("DELETE FROM architect_projects WHERE user_id = @uid AND id = @id", conn)
        cmd.Parameters.AddWithValue("uid", userId) |> ignore
        cmd.Parameters.AddWithValue("id", projectId) |> ignore
        cmd.ExecuteNonQuery() > 0

// ── File tree (scoped to an owned project; ownership already implies paid
//    tier since createProject only succeeds for paid users) ────────────────

let private readEntry (r: System.Data.Common.DbDataReader) : FileEntry =
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

let private joinPath (parentPath: string) (name: string) =
    if parentPath = "/" then "/" + name else parentPath + "/" + name

let private folderExists (conn: NpgsqlConnection) (projectId: Guid) (path: string) : bool =
    if path = "/" then true
    else
        use cmd = new NpgsqlCommand(
            "SELECT 1 FROM architect_files WHERE project_id = @pid AND path = @path AND is_folder = TRUE", conn)
        cmd.Parameters.AddWithValue("pid", projectId) |> ignore
        cmd.Parameters.AddWithValue("path", path) |> ignore
        use r = cmd.ExecuteReader()
        r.Read()

let listFolder (conn: NpgsqlConnection) (userId: Guid) (projectId: Guid) (parentPath: string) : FileEntry list option =
    match getProject conn userId projectId with
    | None -> None
    | Some _ ->
        use cmd = new NpgsqlCommand(
            $"""SELECT {entryColumns} FROM architect_files
                WHERE project_id = @pid AND parent_path = @parent
                ORDER BY is_folder DESC, name ASC""", conn)
        cmd.Parameters.AddWithValue("pid", projectId) |> ignore
        cmd.Parameters.AddWithValue("parent", parentPath) |> ignore
        use r = cmd.ExecuteReader()
        Some [ while r.Read() do yield readEntry r ]

let getEntry (conn: NpgsqlConnection) (userId: Guid) (projectId: Guid) (fileId: Guid) : FileEntry option =
    match getProject conn userId projectId with
    | None -> None
    | Some _ ->
        use cmd = new NpgsqlCommand(
            $"SELECT {entryColumns} FROM architect_files WHERE project_id = @pid AND id = @id", conn)
        cmd.Parameters.AddWithValue("pid", projectId) |> ignore
        cmd.Parameters.AddWithValue("id", fileId) |> ignore
        use r = cmd.ExecuteReader()
        if r.Read() then Some (readEntry r) else None

let createFolder (conn: NpgsqlConnection) (userId: Guid) (projectId: Guid) (parentPath: string) (name: string) : Result<FileEntry, string> =
    let name = name.Trim()
    match getProject conn userId projectId with
    | None -> Error "Project not found."
    | Some _ ->
        if String.IsNullOrWhiteSpace(name) || name.Contains("/") then
            Error "Folder names can't be empty or contain a slash."
        elif not (folderExists conn projectId parentPath) then
            Error "The parent folder no longer exists."
        else
            let path = joinPath parentPath name
            try
                use cmd = new NpgsqlCommand(
                    $"""INSERT INTO architect_files (project_id, is_folder, path, parent_path, name)
                        VALUES (@pid, TRUE, @path, @parent, @name)
                        RETURNING {entryColumns}""", conn)
                cmd.Parameters.AddWithValue("pid", projectId) |> ignore
                cmd.Parameters.AddWithValue("path", path) |> ignore
                cmd.Parameters.AddWithValue("parent", parentPath) |> ignore
                cmd.Parameters.AddWithValue("name", name) |> ignore
                use r = cmd.ExecuteReader()
                if r.Read() then Ok (readEntry r) else Error "Could not create the folder."
            with :? PostgresException as ex when ex.SqlState = "23505" ->
                Error "A folder or file with that name already exists here."

// Creates the file if its (project, path) is new, otherwise overwrites its
// content in place (same id, same S3 key) -- "Save" always upserts rather
// than needing a separate rename/move step for this first version.
let saveFile (conn: NpgsqlConnection) (userId: Guid) (projectId: Guid) (parentPath: string) (name: string) (contentType: string) (content: string) : Result<FileEntry, string> =
    let name = name.Trim()
    match getProject conn userId projectId with
    | None -> Error "Project not found."
    | Some _ ->
        if String.IsNullOrWhiteSpace(name) || name.Contains("/") then
            Error "File names can't be empty or contain a slash."
        elif not (folderExists conn projectId parentPath) then
            Error "The target folder no longer exists."
        else
            let path = joinPath parentPath name
            use existingCmd = new NpgsqlCommand(
                "SELECT id FROM architect_files WHERE project_id = @pid AND path = @path AND is_folder = FALSE", conn)
            existingCmd.Parameters.AddWithValue("pid", projectId) |> ignore
            existingCmd.Parameters.AddWithValue("path", path) |> ignore
            let existingId =
                use r = existingCmd.ExecuteReader()
                if r.Read() then Some (r.GetGuid(0)) else None

            let fileId = existingId |> Option.defaultWith Guid.NewGuid
            let s3Key = s3KeyFor userId projectId fileId
            let sizeBytes = putTextObject s3Key contentType content

            try
                use cmd = new NpgsqlCommand(
                    $"""INSERT INTO architect_files (id, project_id, is_folder, path, parent_path, name, content_type, s3_key, size_bytes)
                        VALUES (@id, @pid, FALSE, @path, @parent, @name, @ct, @key, @size)
                        ON CONFLICT (project_id, path) DO UPDATE
                            SET content_type = EXCLUDED.content_type, size_bytes = EXCLUDED.size_bytes, updated_at = NOW()
                        RETURNING {entryColumns}""", conn)
                cmd.Parameters.AddWithValue("id", fileId) |> ignore
                cmd.Parameters.AddWithValue("pid", projectId) |> ignore
                cmd.Parameters.AddWithValue("path", path) |> ignore
                cmd.Parameters.AddWithValue("parent", parentPath) |> ignore
                cmd.Parameters.AddWithValue("name", name) |> ignore
                cmd.Parameters.AddWithValue("ct", contentType) |> ignore
                cmd.Parameters.AddWithValue("key", s3Key) |> ignore
                cmd.Parameters.AddWithValue("size", sizeBytes) |> ignore
                use r = cmd.ExecuteReader()
                if r.Read() then Ok (readEntry r) else Error "Could not save the file."
            with :? PostgresException as ex when ex.SqlState = "23505" ->
                Error "A folder with that name already exists here."

let getFileContent (conn: NpgsqlConnection) (userId: Guid) (projectId: Guid) (fileId: Guid) : (FileEntry * string) option =
    match getEntry conn userId projectId fileId with
    | Some entry when not entry.IsFolder ->
        getTextObject (s3KeyFor userId projectId fileId) |> Option.map (fun content -> entry, content)
    | _ -> None

// Files delete their S3 object; folders cascade to everything inside them.
let deleteEntry (conn: NpgsqlConnection) (userId: Guid) (projectId: Guid) (fileId: Guid) : Result<unit, string> =
    match getEntry conn userId projectId fileId with
    | None -> Error "Not found."
    | Some entry ->
        if entry.IsFolder then
            use descCmd = new NpgsqlCommand(
                """SELECT id FROM architect_files
                   WHERE project_id = @pid AND is_folder = FALSE AND path LIKE @prefix""", conn)
            descCmd.Parameters.AddWithValue("pid", projectId) |> ignore
            descCmd.Parameters.AddWithValue("prefix", entry.Path + "/%") |> ignore
            let descendantIds =
                use r = descCmd.ExecuteReader()
                [ while r.Read() do yield r.GetGuid(0) ]
            for id in descendantIds do deleteObject (s3KeyFor userId projectId id)

            use delCmd = new NpgsqlCommand(
                "DELETE FROM architect_files WHERE project_id = @pid AND (path = @path OR path LIKE @prefix)", conn)
            delCmd.Parameters.AddWithValue("pid", projectId) |> ignore
            delCmd.Parameters.AddWithValue("path", entry.Path) |> ignore
            delCmd.Parameters.AddWithValue("prefix", entry.Path + "/%") |> ignore
            delCmd.ExecuteNonQuery() |> ignore
        else
            deleteObject (s3KeyFor userId projectId entry.Id)
            use delCmd = new NpgsqlCommand("DELETE FROM architect_files WHERE project_id = @pid AND id = @id", conn)
            delCmd.Parameters.AddWithValue("pid", projectId) |> ignore
            delCmd.Parameters.AddWithValue("id", fileId) |> ignore
            delCmd.ExecuteNonQuery() |> ignore
        Ok ()
