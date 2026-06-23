module Djehuti.Api.Permissions

open System
open Npgsql

// ── Types ─────────────────────────────────────────────────────────────────────

type SystemRole =
    | User
    | Admin

type ContextRole = {
    Module:  string
    Role:    string
    ScopeId: Guid option
}

// ── System Role ───────────────────────────────────────────────────────────────

let parseSystemRole (s: string) =
    match s.ToLower() with
    | "admin" -> Admin
    | _       -> User

let isAdmin (systemRole: string) =
    parseSystemRole systemRole = Admin

// ── Context Role Queries ──────────────────────────────────────────────────────

let hasContextRole (conn: NpgsqlConnection) (userId: Guid) (mdl: string) (role: string) (scopeId: Guid option) : bool =
    let sql =
        match scopeId with
        | None ->
            // module-wide: no scope required
            """SELECT 1 FROM user_roles
               WHERE user_id = @userId AND module = @module AND role = @role
               LIMIT 1"""
        | Some _ ->
            // specific scope OR module-wide (NULL scope acts as wildcard)
            """SELECT 1 FROM user_roles
               WHERE user_id = @userId AND module = @module AND role = @role
                 AND (scope_id = @scopeId OR scope_id IS NULL)
               LIMIT 1"""

    use cmd = new NpgsqlCommand(sql, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("module", mdl) |> ignore
    cmd.Parameters.AddWithValue("role", role) |> ignore
    if scopeId.IsSome then
        cmd.Parameters.AddWithValue("scopeId", scopeId.Value) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read()

let getUserContextRoles (conn: NpgsqlConnection) (userId: Guid) : ContextRole list =
    use cmd = new NpgsqlCommand(
        "SELECT module, role, scope_id FROM user_roles WHERE user_id = @userId", conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield {
            Module  = reader.GetString(0)
            Role    = reader.GetString(1)
            ScopeId = if reader.IsDBNull(2) then None else Some (reader.GetGuid(2))
        } ]

let grantContextRole (conn: NpgsqlConnection) (userId: Guid) (mdl: string) (role: string) (scopeId: Guid option) (grantedBy: Guid) =
    use cmd = new NpgsqlCommand(
        """INSERT INTO user_roles (user_id, module, role, scope_id, granted_by)
           VALUES (@userId, @module, @role, @scopeId, @grantedBy)
           ON CONFLICT (user_id, module, role, scope_id) DO NOTHING""", conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("module", mdl) |> ignore
    cmd.Parameters.AddWithValue("role", role) |> ignore
    cmd.Parameters.AddWithValue("scopeId", scopeId |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("grantedBy", grantedBy) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let revokeContextRole (conn: NpgsqlConnection) (userId: Guid) (mdl: string) (role: string) (scopeId: Guid option) =
    let sql =
        match scopeId with
        | None ->
            "DELETE FROM user_roles WHERE user_id = @userId AND module = @module AND role = @role AND scope_id IS NULL"
        | Some _ ->
            "DELETE FROM user_roles WHERE user_id = @userId AND module = @module AND role = @role AND scope_id = @scopeId"
    use cmd = new NpgsqlCommand(sql, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("module", mdl) |> ignore
    cmd.Parameters.AddWithValue("role", role) |> ignore
    if scopeId.IsSome then
        cmd.Parameters.AddWithValue("scopeId", scopeId.Value) |> ignore
    cmd.ExecuteNonQuery() |> ignore

type ContextRoleAdmin = {
    Id:        Guid
    UserId:    Guid
    Module:    string
    Role:      string
    ScopeId:   Guid option
    GrantedBy: Guid
    GrantedAt: DateTime
}

let getAllContextRoles (conn: NpgsqlConnection) : ContextRoleAdmin list =
    use cmd = new NpgsqlCommand(
        "SELECT id, user_id, module, role, scope_id, granted_by, granted_at FROM user_roles ORDER BY granted_at DESC", conn)
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield {
            Id        = reader.GetGuid(reader.GetOrdinal("id"))
            UserId    = reader.GetGuid(reader.GetOrdinal("user_id"))
            Module    = reader.GetString(reader.GetOrdinal("module"))
            Role      = reader.GetString(reader.GetOrdinal("role"))
            ScopeId   = if reader.IsDBNull(reader.GetOrdinal("scope_id")) then None else Some(reader.GetGuid(reader.GetOrdinal("scope_id")))
            GrantedBy = reader.GetGuid(reader.GetOrdinal("granted_by"))
            GrantedAt = reader.GetDateTime(reader.GetOrdinal("granted_at"))
        } ]

let revokeContextRoleById (conn: NpgsqlConnection) (id: Guid) : bool =
    use cmd = new NpgsqlCommand("DELETE FROM user_roles WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0

// ── Well-known module/role constants ─────────────────────────────────────────

[<Literal>]
let ModuleForum = "forum"
[<Literal>]
let ModuleBlog = "blog"
[<Literal>]
let ModulePapers = "papers"

[<Literal>]
let RoleModerator = "moderator"
[<Literal>]
let RoleAuthor = "author"
[<Literal>]
let RoleEditor = "editor"
[<Literal>]
let RoleContributor = "contributor"
[<Literal>]
let RoleViewer = "viewer"
