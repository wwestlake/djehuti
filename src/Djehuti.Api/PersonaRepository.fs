module Djehuti.Api.PersonaRepository

open System
open System.Data.Common
open Npgsql
open Database

// ── Types ─────────────────────────────────────────────────────────────────────

type AiPersona = {
    Id:               Guid
    Name:             string
    Slug:             string
    AvatarUrl:        string option
    SystemPrompt:     string
    Model:            string
    TriggerMode:      string
    Active:           bool
    NextScheduledRun: DateTime option
    UserId:           Guid option
    CreatedAt:        DateTime
}

type HeartbeatJob = {
    Id:          Guid
    ActionType:  string
    Payload:     string
    Status:      string
    RetryCount:  int
    MaxRetries:  int
    CreatedAt:   DateTime
    LockedAt:    DateTime option
    CompletedAt: DateTime option
    Error:       string option
}

// ── Helpers ───────────────────────────────────────────────────────────────────

let private readPersona (r: DbDataReader) : AiPersona = {
    Id               = r.GetGuid(0)
    Name             = r.GetString(1)
    Slug             = r.GetString(2)
    AvatarUrl        = if r.IsDBNull(3) then None else Some (r.GetString(3))
    SystemPrompt     = r.GetString(4)
    Model            = r.GetString(5)
    TriggerMode      = r.GetString(6)
    Active           = r.GetBoolean(7)
    NextScheduledRun = if r.IsDBNull(8) then None else Some (r.GetFieldValue<DateTime>(8))
    UserId           = if r.IsDBNull(9) then None else Some (r.GetGuid(9))
    CreatedAt        = r.GetFieldValue<DateTime>(10)
}

let private readJob (r: DbDataReader) : HeartbeatJob = {
    Id          = r.GetGuid(0)
    ActionType  = r.GetString(1)
    Payload     = r.GetString(2)
    Status      = r.GetString(3)
    RetryCount  = r.GetInt32(4)
    MaxRetries  = r.GetInt32(5)
    CreatedAt   = r.GetFieldValue<DateTime>(6)
    LockedAt    = if r.IsDBNull(7) then None else Some (r.GetFieldValue<DateTime>(7))
    CompletedAt = if r.IsDBNull(8) then None else Some (r.GetFieldValue<DateTime>(8))
    Error       = if r.IsDBNull(9) then None else Some (r.GetString(9))
}

// ── Personas ──────────────────────────────────────────────────────────────────

let getPersonas () : AiPersona list =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, name, slug, avatar_url, system_prompt, model, trigger_mode,
                  active, next_scheduled_run, user_id, created_at
           FROM ai_personas ORDER BY name""", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readPersona r ]

let getPersonaById (id: Guid) : AiPersona option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, name, slug, avatar_url, system_prompt, model, trigger_mode,
                  active, next_scheduled_run, user_id, created_at
           FROM ai_personas WHERE id = @id""", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readPersona r) else None

let createPersona (name: string) (slug: string) (systemPrompt: string) (model: string) (triggerMode: string) (avatarUrl: string option) (botUserId: Guid) : AiPersona option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO ai_personas (name, slug, system_prompt, model, trigger_mode, avatar_url, user_id)
           VALUES (@name, @slug, @sp, @model, @tm, @av, @uid)
           RETURNING id, name, slug, avatar_url, system_prompt, model, trigger_mode,
                     active, next_scheduled_run, user_id, created_at""", conn)
    cmd.Parameters.AddWithValue("name",  name)                                                              |> ignore
    cmd.Parameters.AddWithValue("slug",  slug)                                                              |> ignore
    cmd.Parameters.AddWithValue("sp",    systemPrompt)                                                      |> ignore
    cmd.Parameters.AddWithValue("model", model)                                                             |> ignore
    cmd.Parameters.AddWithValue("tm",    triggerMode)                                                       |> ignore
    cmd.Parameters.AddWithValue("av",    avatarUrl |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("uid",   botUserId)                                                         |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readPersona r) else None

let updatePersona (id: Guid) (name: string) (systemPrompt: string) (model: string) (triggerMode: string) (active: bool) (avatarUrl: string option) : AiPersona option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE ai_personas
           SET name = @name, system_prompt = @sp, model = @model,
               trigger_mode = @tm, active = @active, avatar_url = @av
           WHERE id = @id
           RETURNING id, name, slug, avatar_url, system_prompt, model, trigger_mode,
                     active, next_scheduled_run, user_id, created_at""", conn)
    cmd.Parameters.AddWithValue("id",     id)                                                               |> ignore
    cmd.Parameters.AddWithValue("name",   name)                                                             |> ignore
    cmd.Parameters.AddWithValue("sp",     systemPrompt)                                                     |> ignore
    cmd.Parameters.AddWithValue("model",  model)                                                            |> ignore
    cmd.Parameters.AddWithValue("tm",     triggerMode)                                                      |> ignore
    cmd.Parameters.AddWithValue("active", active)                                                           |> ignore
    cmd.Parameters.AddWithValue("av",     avatarUrl |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readPersona r) else None

let deletePersona (id: Guid) : bool =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("DELETE FROM ai_personas WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0

let getPersonaForums (personaId: Guid) : Guid list =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT forum_id FROM ai_persona_forums WHERE persona_id = @pid", conn)
    cmd.Parameters.AddWithValue("pid", personaId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield r.GetGuid(0) ]

let setPersonaForums (personaId: Guid) (forumIds: Guid list) : unit =
    use conn = openConnection ()
    use txn = conn.BeginTransaction()
    use del = new NpgsqlCommand("DELETE FROM ai_persona_forums WHERE persona_id = @pid", conn, txn)
    del.Parameters.AddWithValue("pid", personaId) |> ignore
    del.ExecuteNonQuery() |> ignore
    for fid in forumIds do
        use ins = new NpgsqlCommand(
            "INSERT INTO ai_persona_forums (persona_id, forum_id) VALUES (@pid, @fid) ON CONFLICT DO NOTHING", conn, txn)
        ins.Parameters.AddWithValue("pid", personaId) |> ignore
        ins.Parameters.AddWithValue("fid", fid)       |> ignore
        ins.ExecuteNonQuery() |> ignore
    txn.Commit()

let getActivePersonasForForum (forumId: Guid) : AiPersona list =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT p.id, p.name, p.slug, p.avatar_url, p.system_prompt, p.model, p.trigger_mode,
                  p.active, p.next_scheduled_run, p.user_id, p.created_at
           FROM ai_personas p
           JOIN ai_persona_forums pf ON pf.persona_id = p.id
           WHERE pf.forum_id = @fid AND p.active = true""", conn)
    cmd.Parameters.AddWithValue("fid", forumId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readPersona r ]

// ── Heartbeat Jobs ─────────────────────────────────────────────────────────────

let enqueueJob (actionType: string) (payload: string) : HeartbeatJob option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO heartbeat_jobs (action_type, payload)
           VALUES (@at, @payload::jsonb)
           RETURNING id, action_type, payload::text, status, retry_count, max_retries,
                     created_at, locked_at, completed_at, error""", conn)
    cmd.Parameters.AddWithValue("at",      actionType) |> ignore
    cmd.Parameters.AddWithValue("payload", payload)    |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readJob r) else None

let fetchAndLockJobs (limit: int) : HeartbeatJob list =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE heartbeat_jobs
           SET status = 'Processing', locked_at = now()
           WHERE id IN (
               SELECT id FROM heartbeat_jobs
               WHERE status = 'Pending'
               ORDER BY created_at
               LIMIT @limit
               FOR UPDATE SKIP LOCKED
           )
           RETURNING id, action_type, payload::text, status, retry_count, max_retries,
                     created_at, locked_at, completed_at, error""", conn)
    cmd.Parameters.AddWithValue("limit", limit) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readJob r ]

let completeJob (id: Guid) : unit =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "UPDATE heartbeat_jobs SET status = 'Completed', completed_at = now() WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let failJob (id: Guid) (error: string) (maxRetries: int) (currentRetry: int) : unit =
    use conn = openConnection ()
    let newStatus = if currentRetry + 1 >= maxRetries then "Failed" else "Pending"
    use cmd = new NpgsqlCommand(
        """UPDATE heartbeat_jobs
           SET status = @status, retry_count = retry_count + 1, error = @err, locked_at = null
           WHERE id = @id""", conn)
    cmd.Parameters.AddWithValue("id",     id)        |> ignore
    cmd.Parameters.AddWithValue("status", newStatus) |> ignore
    cmd.Parameters.AddWithValue("err",    error)     |> ignore
    cmd.ExecuteNonQuery() |> ignore

let getRecentJobs (limit: int) : HeartbeatJob list =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, action_type, payload::text, status, retry_count, max_retries,
                  created_at, locked_at, completed_at, error
           FROM heartbeat_jobs
           ORDER BY created_at DESC LIMIT @limit""", conn)
    cmd.Parameters.AddWithValue("limit", limit) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readJob r ]

let pruneOldFailedJobs () : int =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "DELETE FROM heartbeat_jobs WHERE status = 'Failed' AND created_at < now() - interval '7 days'", conn)
    cmd.ExecuteNonQuery()

// ── Heartbeat Config ───────────────────────────────────────────────────────────

let getConfig () : Map<string, string> =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("SELECT key, value FROM heartbeat_config", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield r.GetString(0), r.GetString(1) ]
    |> Map.ofList

let setConfig (key: string) (value: string) : unit =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "INSERT INTO heartbeat_config (key, value) VALUES (@k, @v) ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value", conn)
    cmd.Parameters.AddWithValue("k", key)   |> ignore
    cmd.Parameters.AddWithValue("v", value) |> ignore
    cmd.ExecuteNonQuery() |> ignore
