module Djehuti.Api.ArchitectRepository

open System
open Npgsql

// ── Types ────────────────────────────────────────────────────────────────────

type ArchitectModelSummary = {
    Id:        Guid
    Name:      string
    UpdatedAt: DateTime
}

type ArchitectModelRecord = {
    Id:        Guid
    UserId:    Guid
    Name:      string
    ModelJson: string
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

// ── Tier limit (placeholder numbers, deliberately isolated to this one
//    function -- easy to retune without touching anything else, same
//    convention as DjeLabFilesRepository.storageQuotaBytesForTier) ──────────

let maxSavedModelsForTier (tierId: string option) : int =
    match tierId with
    | Some "curious-mind"    -> 5
    | Some "lab-assistant"   -> 15
    | Some "research-fellow" -> 50
    | Some "professor"       -> 500
    | Some "dean"            -> 500
    | _                      -> 2 // Free (signed in, no Patreon tier)

// ── Reading ──────────────────────────────────────────────────────────────────

let private readSummary (r: System.Data.Common.DbDataReader) : ArchitectModelSummary =
    {
        Id        = r.GetGuid(r.GetOrdinal("id"))
        Name      = r.GetString(r.GetOrdinal("name"))
        UpdatedAt = r.GetFieldValue<DateTime>(r.GetOrdinal("updated_at"))
    }

let private readRecord (r: System.Data.Common.DbDataReader) : ArchitectModelRecord =
    {
        Id        = r.GetGuid(r.GetOrdinal("id"))
        UserId    = r.GetGuid(r.GetOrdinal("user_id"))
        Name      = r.GetString(r.GetOrdinal("name"))
        ModelJson = r.GetString(r.GetOrdinal("model_json"))
        CreatedAt = r.GetFieldValue<DateTime>(r.GetOrdinal("created_at"))
        UpdatedAt = r.GetFieldValue<DateTime>(r.GetOrdinal("updated_at"))
    }

let listForUser (conn: NpgsqlConnection) (userId: Guid) : ArchitectModelSummary list =
    use cmd = new NpgsqlCommand(
        "SELECT id, name, updated_at FROM architect_models WHERE user_id = @uid ORDER BY updated_at DESC", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readSummary r ]

let countForUser (conn: NpgsqlConnection) (userId: Guid) : int =
    use cmd = new NpgsqlCommand("SELECT COUNT(*) FROM architect_models WHERE user_id = @uid", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    Convert.ToInt32(cmd.ExecuteScalar())

let getById (conn: NpgsqlConnection) (userId: Guid) (id: Guid) : ArchitectModelRecord option =
    use cmd = new NpgsqlCommand(
        """SELECT id, user_id, name, model_json::text AS model_json, created_at, updated_at
           FROM architect_models WHERE user_id = @uid AND id = @id""", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readRecord r) else None

// ── Writing ──────────────────────────────────────────────────────────────────

// Enforces the tier limit here (not just in the UI) since the UI check is
// only a convenience -- nothing stops a request hitting this endpoint
// directly.
let create (conn: NpgsqlConnection) (userId: Guid) (tierId: string option) (name: string) (modelJson: string) : Result<ArchitectModelRecord, string> =
    let name = name.Trim()
    if String.IsNullOrWhiteSpace(name) then
        Error "Give the model a name."
    else
        let current = countForUser conn userId
        let max = maxSavedModelsForTier tierId
        if current >= max then
            Error $"You've reached your saved-model limit ({current} of {max}). Upgrade your Patreon tier for more."
        else
            use cmd = new NpgsqlCommand(
                """INSERT INTO architect_models (user_id, name, model_json)
                   VALUES (@uid, @name, @json::jsonb)
                   RETURNING id, user_id, name, model_json::text AS model_json, created_at, updated_at""", conn)
            cmd.Parameters.AddWithValue("uid", userId) |> ignore
            cmd.Parameters.AddWithValue("name", name) |> ignore
            cmd.Parameters.AddWithValue("json", modelJson) |> ignore
            use r = cmd.ExecuteReader()
            if r.Read() then Ok (readRecord r) else Error "Could not create the model."

let update (conn: NpgsqlConnection) (userId: Guid) (id: Guid) (name: string) (modelJson: string) : Result<ArchitectModelRecord, string> =
    let name = name.Trim()
    if String.IsNullOrWhiteSpace(name) then
        Error "Give the model a name."
    else
        use cmd = new NpgsqlCommand(
            """UPDATE architect_models
               SET name = @name, model_json = @json::jsonb, updated_at = NOW()
               WHERE user_id = @uid AND id = @id
               RETURNING id, user_id, name, model_json::text AS model_json, created_at, updated_at""", conn)
        cmd.Parameters.AddWithValue("uid", userId) |> ignore
        cmd.Parameters.AddWithValue("id", id) |> ignore
        cmd.Parameters.AddWithValue("name", name) |> ignore
        cmd.Parameters.AddWithValue("json", modelJson) |> ignore
        use r = cmd.ExecuteReader()
        if r.Read() then Ok (readRecord r) else Error "Model not found."

let delete (conn: NpgsqlConnection) (userId: Guid) (id: Guid) : bool =
    use cmd = new NpgsqlCommand("DELETE FROM architect_models WHERE user_id = @uid AND id = @id", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0
