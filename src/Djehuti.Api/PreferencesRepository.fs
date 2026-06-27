module Djehuti.Api.PreferencesRepository

open System
open Npgsql
open NpgsqlTypes
open System.Text.Json

// ── Defaults ────────────────────────────────────────────────────────────────

let private defaults =
    Map.ofList [
        // Notifications - email
        "email_notify_replies",              box false
        "email_notify_mentions",             box true
        "email_notify_achievements",         box true
        "email_notify_announcements",        box false
        "email_notify_blog_comments",        box true
        "email_notify_paper_collaborators",  box true
        // Notifications - in-app
        "inapp_notify_replies",              box true
        "inapp_notify_mentions",             box true
        "inapp_notify_achievements",         box true
        "inapp_notify_announcements",        box true
        // Forum
        "forum_default_subscription",        box "tracking"
        "forum_show_bot_posts",              box true
        "forum_show_achievements_on_profile",box true
        "forum_compact_view",               box false
        "forum_signature",                   box ""
        // Blog
        "blog_default_visibility",           box "public"
        "blog_autosave_interval",            box "1min"
        "blog_allow_comments",               box true
        "blog_comment_notify",               box true
        "blog_editor_mode",                  box "rich"
        // Papers
        "papers_default_visibility",         box "private"
        "papers_collab_notify",              box true
        "papers_comment_notify",             box true
        "papers_show_word_count",            box true
        "papers_citation_style",             box "APA"
        // Privacy
        "privacy_show_online_status",        box true
        "privacy_show_profile_public",       box true
        "privacy_index_posts",               box true
    ]

// ── Helpers ─────────────────────────────────────────────────────────────────

let private openConn () =
    let conn = new NpgsqlConnection(Database.connectionString())
    conn.Open()
    conn

let private parsePrefs (json: string) : Map<string, obj> =
    if String.IsNullOrWhiteSpace json then Map.empty
    else
        try
            let doc = JsonDocument.Parse(json)
            doc.RootElement.EnumerateObject()
            |> Seq.map (fun p ->
                let v : obj =
                    match p.Value.ValueKind with
                    | JsonValueKind.True  -> box true
                    | JsonValueKind.False -> box false
                    | JsonValueKind.Number -> box (p.Value.GetDouble())
                    | _ -> box (p.Value.GetString())
                p.Name, v)
            |> Map.ofSeq
        with _ -> Map.empty

let private mergeWithDefaults (stored: Map<string, obj>) : Map<string, obj> =
    defaults |> Map.map (fun k defaultVal ->
        stored |> Map.tryFind k |> Option.defaultValue defaultVal)
    |> fun m ->
        stored |> Map.fold (fun acc k v -> Map.add k v acc) m  // include any extra stored keys

// ── Public API ───────────────────────────────────────────────────────────────

let getPreferences (userId: Guid) : Map<string, obj> =
    use conn = openConn()
    use cmd = new NpgsqlCommand(
        "SELECT prefs FROM user_preferences WHERE user_id = @uid", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use r = cmd.ExecuteReader()
    let stored =
        if r.Read() then parsePrefs (r.GetString(0))
        else Map.empty
    mergeWithDefaults stored

let patchPreferences (userId: Guid) (patch: Map<string, obj>) : Map<string, obj> =
    use conn = openConn()
    // Build JSON string for the patch
    let patchJson =
        let entries =
            patch |> Map.toSeq |> Seq.map (fun (k, v) ->
                match v with
                | :? bool as b   -> sprintf "\"%s\":%s" k (if b then "true" else "false")
                | :? string as s -> sprintf "\"%s\":\"%s\"" k (s.Replace("\"", "\\\""))
                | :? double as d -> sprintf "\"%s\":%g" k d
                | _ -> sprintf "\"%s\":\"%s\"" k (v.ToString()))
        "{" + String.concat "," entries + "}"
    use cmd = new NpgsqlCommand(
        """INSERT INTO user_preferences (user_id, prefs, updated_at)
           VALUES (@uid, @patch::jsonb, now())
           ON CONFLICT (user_id) DO UPDATE
               SET prefs = user_preferences.prefs || @patch::jsonb,
                   updated_at = now()""", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.Parameters.AddWithValue("patch", patchJson) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    getPreferences userId

let getBoolPref (userId: Guid) (key: string) : bool =
    let prefs = getPreferences userId
    match prefs |> Map.tryFind key with
    | Some (:? bool as b) -> b
    | _ ->
        match defaults |> Map.tryFind key with
        | Some (:? bool as b) -> b
        | _ -> false

let getStringPref (userId: Guid) (key: string) : string =
    let prefs = getPreferences userId
    match prefs |> Map.tryFind key with
    | Some (:? string as s) -> s
    | _ ->
        match defaults |> Map.tryFind key with
        | Some (:? string as s) -> s
        | _ -> ""
