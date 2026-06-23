module PaperRepository

open System
open System.Data.Common
open Npgsql

type Paper = {
    Id:        Guid
    OwnerId:   Guid
    Title:     string
    Abstract:  string option
    Status:    string
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

type PaperCollaborator = {
    PaperId:    Guid
    UserId:     Guid option
    Name:       string
    Email:      string option
    Role:       string
    IsExternal: bool
    AddedAt:    DateTime
}

type PaperSection = {
    Id:        Guid
    PaperId:   Guid
    Title:     string
    Content:   string
    Position:  int
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

let private readPaper (r: DbDataReader) : Paper = {
    Id       = r.GetGuid(r.GetOrdinal("id"))
    OwnerId  = r.GetGuid(r.GetOrdinal("owner_id"))
    Title    = r.GetString(r.GetOrdinal("title"))
    Abstract = if r.IsDBNull(r.GetOrdinal("abstract")) then None else Some(r.GetString(r.GetOrdinal("abstract")))
    Status   = r.GetString(r.GetOrdinal("status"))
    CreatedAt= r.GetDateTime(r.GetOrdinal("created_at"))
    UpdatedAt= r.GetDateTime(r.GetOrdinal("updated_at"))
}

let private readCollaborator (r: DbDataReader) : PaperCollaborator = {
    PaperId    = r.GetGuid(r.GetOrdinal("paper_id"))
    UserId     = if r.IsDBNull(r.GetOrdinal("user_id")) then None else Some(r.GetGuid(r.GetOrdinal("user_id")))
    Name       = r.GetString(r.GetOrdinal("name"))
    Email      = if r.IsDBNull(r.GetOrdinal("email")) then None else Some(r.GetString(r.GetOrdinal("email")))
    Role       = r.GetString(r.GetOrdinal("role"))
    IsExternal = r.GetBoolean(r.GetOrdinal("is_external"))
    AddedAt    = r.GetDateTime(r.GetOrdinal("added_at"))
}

let private readSection (r: DbDataReader) : PaperSection = {
    Id        = r.GetGuid(r.GetOrdinal("id"))
    PaperId   = r.GetGuid(r.GetOrdinal("paper_id"))
    Title     = r.GetString(r.GetOrdinal("title"))
    Content   = r.GetString(r.GetOrdinal("content"))
    Position  = r.GetInt32(r.GetOrdinal("position"))
    CreatedAt = r.GetDateTime(r.GetOrdinal("created_at"))
    UpdatedAt = r.GetDateTime(r.GetOrdinal("updated_at"))
}

let getPapersByOwner (conn: NpgsqlConnection) (ownerId: Guid) : Paper list =
    use cmd = new NpgsqlCommand(
        "SELECT * FROM papers WHERE owner_id = @oid ORDER BY updated_at DESC", conn)
    cmd.Parameters.AddWithValue("oid", ownerId) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do yield readPaper reader ]

let getPaperById (conn: NpgsqlConnection) (paperId: Guid) : Paper option =
    use cmd = new NpgsqlCommand("SELECT * FROM papers WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", paperId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some(readPaper reader) else None

let createPaper (conn: NpgsqlConnection) (ownerId: Guid) (title: string) (abstract_: string option) : Paper option =
    use cmd = new NpgsqlCommand("""
        INSERT INTO papers (owner_id, title, abstract)
        VALUES (@oid, @title, @abs)
        RETURNING *""", conn)
    cmd.Parameters.AddWithValue("oid",   ownerId) |> ignore
    cmd.Parameters.AddWithValue("title", title)   |> ignore
    cmd.Parameters.AddWithValue("abs",   abstract_ |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some(readPaper reader) else None

let updatePaper (conn: NpgsqlConnection) (paperId: Guid) (title: string) (abstract_: string option) : Paper option =
    use cmd = new NpgsqlCommand("""
        UPDATE papers SET title = @title, abstract = @abs, updated_at = now()
        WHERE id = @id RETURNING *""", conn)
    cmd.Parameters.AddWithValue("id",    paperId)  |> ignore
    cmd.Parameters.AddWithValue("title", title)    |> ignore
    cmd.Parameters.AddWithValue("abs",   abstract_ |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some(readPaper reader) else None

let setStatus (conn: NpgsqlConnection) (paperId: Guid) (status: string) : Paper option =
    use cmd = new NpgsqlCommand("""
        UPDATE papers SET status = @status, updated_at = now()
        WHERE id = @id RETURNING *""", conn)
    cmd.Parameters.AddWithValue("id",     paperId) |> ignore
    cmd.Parameters.AddWithValue("status", status)  |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some(readPaper reader) else None

let deletePaper (conn: NpgsqlConnection) (paperId: Guid) (requesterId: Guid) (isAdmin: bool) : bool =
    let paper = getPaperById conn paperId
    match paper with
    | None -> false
    | Some p when not isAdmin && p.OwnerId <> requesterId -> false
    | _ ->
        use cmd = new NpgsqlCommand("DELETE FROM papers WHERE id = @id", conn)
        cmd.Parameters.AddWithValue("id", paperId) |> ignore
        cmd.ExecuteNonQuery() > 0

// Collaborators

let getCollaborators (conn: NpgsqlConnection) (paperId: Guid) : PaperCollaborator list =
    use cmd = new NpgsqlCommand("SELECT * FROM paper_collaborators WHERE paper_id = @pid ORDER BY added_at", conn)
    cmd.Parameters.AddWithValue("pid", paperId) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do yield readCollaborator reader ]

let addCollaborator (conn: NpgsqlConnection) (paperId: Guid) (name: string) (email: string option) (role: string) (userId: Guid option) (isExternal: bool) : bool =
    use cmd = new NpgsqlCommand("""
        INSERT INTO paper_collaborators (paper_id, user_id, name, email, role, is_external)
        VALUES (@pid, @uid, @name, @email, @role, @ext)
        ON CONFLICT (paper_id, name) DO UPDATE SET role = EXCLUDED.role, email = EXCLUDED.email""", conn)
    cmd.Parameters.AddWithValue("pid",  paperId) |> ignore
    cmd.Parameters.AddWithValue("uid",  userId |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("name", name)   |> ignore
    cmd.Parameters.AddWithValue("email",email |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("role", role)   |> ignore
    cmd.Parameters.AddWithValue("ext",  isExternal) |> ignore
    cmd.ExecuteNonQuery() > 0

let removeCollaborator (conn: NpgsqlConnection) (paperId: Guid) (name: string) : bool =
    use cmd = new NpgsqlCommand("DELETE FROM paper_collaborators WHERE paper_id = @pid AND name = @name", conn)
    cmd.Parameters.AddWithValue("pid",  paperId) |> ignore
    cmd.Parameters.AddWithValue("name", name)    |> ignore
    cmd.ExecuteNonQuery() > 0

// Sections

let getSections (conn: NpgsqlConnection) (paperId: Guid) : PaperSection list =
    use cmd = new NpgsqlCommand("SELECT * FROM paper_sections WHERE paper_id = @pid ORDER BY position, created_at", conn)
    cmd.Parameters.AddWithValue("pid", paperId) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do yield readSection reader ]

let createSection (conn: NpgsqlConnection) (paperId: Guid) (title: string) (position: int) : PaperSection option =
    use cmd = new NpgsqlCommand("""
        INSERT INTO paper_sections (paper_id, title, position)
        VALUES (@pid, @title, @pos)
        RETURNING *""", conn)
    cmd.Parameters.AddWithValue("pid",   paperId) |> ignore
    cmd.Parameters.AddWithValue("title", title)   |> ignore
    cmd.Parameters.AddWithValue("pos",   position) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some(readSection reader) else None

let updateSection (conn: NpgsqlConnection) (sectionId: Guid) (title: string) (content: string) : PaperSection option =
    use cmd = new NpgsqlCommand("""
        UPDATE paper_sections SET title = @title, content = @content, updated_at = now()
        WHERE id = @id RETURNING *""", conn)
    cmd.Parameters.AddWithValue("id",      sectionId) |> ignore
    cmd.Parameters.AddWithValue("title",   title)     |> ignore
    cmd.Parameters.AddWithValue("content", content)   |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some(readSection reader) else None

let deleteSection (conn: NpgsqlConnection) (sectionId: Guid) : bool =
    use cmd = new NpgsqlCommand("DELETE FROM paper_sections WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", sectionId) |> ignore
    cmd.ExecuteNonQuery() > 0
