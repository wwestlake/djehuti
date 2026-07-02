module Djehuti.Api.MudCompanionRepository

open System
open Npgsql
open Database
open SecretProtector

type MudCompanionSettings =
    { CharacterId: Guid
      Enabled: bool
      Mode: string
      Model: string
      Disclosure: string
      AllowOnlineConcurrency: bool
      UseByoOpenAiKey: bool
      HasByoOpenAiKey: bool
      KeyLastSetAt: DateTime option
      LastStatus: string option
      LastError: string option
      Eligible: bool
      EligibilityReason: string
      UpdatedAt: DateTime option }

let private defaultSettings characterId eligible reason =
    { CharacterId = characterId
      Enabled = false
      Mode = "solitary"
      Model = "gpt-4.1-mini"
      Disclosure = "tagged"
      AllowOnlineConcurrency = false
      UseByoOpenAiKey = false
      HasByoOpenAiKey = false
      KeyLastSetAt = None
      LastStatus = None
      LastError = None
      Eligible = eligible
      EligibilityReason = reason
      UpdatedAt = None }

let private isEligible (conn: NpgsqlConnection) (userId: Guid) =
    use cmd = new NpgsqlCommand(
        """SELECT role, patreon_tier_id
           FROM users
           WHERE id = @uid""", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        let role = reader.GetString(0)
        let hasTier = not (reader.IsDBNull(1))
        if role = "admin" || hasTier then true, ""
        else false, "AI companions require a paid tier."
    else
        false, "User not found."

let private ownedCharacterExists (conn: NpgsqlConnection) (userId: Guid) (characterId: Guid) =
    use cmd = new NpgsqlCommand(
        """SELECT 1
           FROM mud_characters
           WHERE id = @character_id
             AND user_id = @uid
             AND deleted_at IS NULL""", conn)
    cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    let scalar = cmd.ExecuteScalar()
    not (isNull scalar || scalar = box DBNull.Value)

let getSettings (userId: Guid) (characterId: Guid) =
    use conn = openConnection ()
    if not (ownedCharacterExists conn userId characterId) then
        None
    else
        let eligible, reason = isEligible conn userId
        use cmd = new NpgsqlCommand(
            """SELECT enabled,
                      mode,
                      model,
                      disclosure,
                      allow_online_concurrency,
                      use_byo_openai_key,
                      byo_openai_key_protected,
                      key_last_set_at,
                      last_status,
                      last_error,
                      updated_at
               FROM mud_companion_profiles
               WHERE character_id = @character_id""", conn)
        cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then
            Some
                { CharacterId = characterId
                  Enabled = reader.GetBoolean(0)
                  Mode = reader.GetString(1)
                  Model = reader.GetString(2)
                  Disclosure = reader.GetString(3)
                  AllowOnlineConcurrency = reader.GetBoolean(4)
                  UseByoOpenAiKey = reader.GetBoolean(5)
                  HasByoOpenAiKey = not (reader.IsDBNull(6))
                  KeyLastSetAt = if reader.IsDBNull(7) then None else Some (reader.GetFieldValue<DateTime>(7))
                  LastStatus = if reader.IsDBNull(8) then None else Some (reader.GetString(8))
                  LastError = if reader.IsDBNull(9) then None else Some (reader.GetString(9))
                  Eligible = eligible
                  EligibilityReason = reason
                  UpdatedAt = if reader.IsDBNull(10) then None else Some (reader.GetFieldValue<DateTime>(10)) }
        else
            Some (defaultSettings characterId eligible reason)

let upsertSettings
    (userId: Guid)
    (characterId: Guid)
    (enabled: bool)
    (mode: string)
    (model: string)
    (disclosure: string)
    (allowOnlineConcurrency: bool)
    (useByoOpenAiKey: bool)
    (openAiApiKey: string option) =
    use conn = openConnection ()
    if not (ownedCharacterExists conn userId characterId) then
        Error "Character not found."
    else
        let eligible, reason = isEligible conn userId
        if not eligible then
            Error reason
        else
            let normalizedMode =
                match mode.Trim().ToLowerInvariant() with
                | "social" -> "social"
                | _ -> "solitary"
            let normalizedDisclosure =
                match disclosure.Trim().ToLowerInvariant() with
                | "hidden" -> "hidden"
                | "contextual" -> "contextual"
                | _ -> "tagged"
            let normalizedModel =
                if String.IsNullOrWhiteSpace model then "gpt-4.1-mini" else model.Trim()
            let trimmedKey = openAiApiKey |> Option.bind (fun value -> if String.IsNullOrWhiteSpace value then None else Some (value.Trim()))
            let existingSettings = getSettings userId characterId
            let hasExistingKey =
                existingSettings
                |> Option.map _.HasByoOpenAiKey
                |> Option.defaultValue false

            if useByoOpenAiKey && trimmedKey.IsNone && not hasExistingKey then
                Error "Paste an OpenAI API key before turning on bring-your-own companion mode."
            else
                let protectedKey = trimmedKey |> Option.map protect

                use cmd = new NpgsqlCommand(
                    """INSERT INTO mud_companion_profiles (
                           character_id,
                           enabled,
                           mode,
                           model,
                           disclosure,
                           allow_online_concurrency,
                           use_byo_openai_key,
                           byo_openai_key_protected,
                           key_last_set_at,
                           last_status,
                           last_error
                       )
                       VALUES (
                           @character_id,
                           @enabled,
                           @mode,
                           @model,
                           @disclosure,
                           @allow_online_concurrency,
                           @use_byo_openai_key,
                           @byo_openai_key_protected,
                           CASE WHEN @key_changed THEN now() ELSE NULL END,
                           NULL,
                           NULL
                       )
                       ON CONFLICT (character_id) DO UPDATE
                       SET enabled = EXCLUDED.enabled,
                           mode = EXCLUDED.mode,
                           model = EXCLUDED.model,
                           disclosure = EXCLUDED.disclosure,
                           allow_online_concurrency = EXCLUDED.allow_online_concurrency,
                           use_byo_openai_key = EXCLUDED.use_byo_openai_key,
                           byo_openai_key_protected = CASE
                               WHEN @key_changed THEN EXCLUDED.byo_openai_key_protected
                               ELSE mud_companion_profiles.byo_openai_key_protected
                           END,
                           key_last_set_at = CASE
                               WHEN @key_changed THEN now()
                               ELSE mud_companion_profiles.key_last_set_at
                           END,
                           last_status = NULL,
                           last_error = NULL,
                           updated_at = now()""", conn)
                cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
                cmd.Parameters.AddWithValue("enabled", enabled) |> ignore
                cmd.Parameters.AddWithValue("mode", normalizedMode) |> ignore
                cmd.Parameters.AddWithValue("model", normalizedModel) |> ignore
                cmd.Parameters.AddWithValue("disclosure", normalizedDisclosure) |> ignore
                cmd.Parameters.AddWithValue("allow_online_concurrency", allowOnlineConcurrency) |> ignore
                cmd.Parameters.AddWithValue("use_byo_openai_key", useByoOpenAiKey) |> ignore
                cmd.Parameters.AddWithValue("byo_openai_key_protected", protectedKey |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
                cmd.Parameters.AddWithValue("key_changed", protectedKey.IsSome) |> ignore
                cmd.ExecuteNonQuery() |> ignore
                match getSettings userId characterId with
                | Some settings -> Ok settings
                | None -> Error "Companion settings unavailable."

let removeByoKey (userId: Guid) (characterId: Guid) =
    use conn = openConnection ()
    if not (ownedCharacterExists conn userId characterId) then
        Error "Character not found."
    else
        use cmd = new NpgsqlCommand(
            """UPDATE mud_companion_profiles
               SET byo_openai_key_protected = NULL,
                   use_byo_openai_key = FALSE,
                   last_status = NULL,
                   last_error = NULL,
                   updated_at = now()
               WHERE character_id = @character_id""", conn)
        cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        match getSettings userId characterId with
        | Some settings -> Ok settings
        | None -> Error "Companion settings unavailable."
