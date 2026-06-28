module Djehuti.Api.HeartbeatWorker

open System
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

// ── Claude API types ───────────────────────────────────────────────────────────

[<CLIMutable>]
type ClaudeMessage = { role: string; content: string }

[<CLIMutable>]
type ClaudeRequest = {
    model:      string
    max_tokens: int
    system:     string
    messages:   ClaudeMessage list
}

[<CLIMutable>]
type ClaudeContentBlock = { ``type``: string; text: string }

[<CLIMutable>]
type ClaudeResponse = { content: ClaudeContentBlock list }

// ── Moderation types ───────────────────────────────────────────────────────────

[<CLIMutable>]
type ModerationResult = {
    PostID:          string
    IsViolation:     bool
    Category:        string
    ConfidenceScore: float
}

// ── Job payload types ──────────────────────────────────────────────────────────

[<CLIMutable>]
type GenerateReplyPayload = {
    PersonaId:        string
    ThreadId:         string
    SystemDirectives: string
    Model:            string
    BotUserId:        string
}

[<CLIMutable>]
type SendEmailPayload = {
    template:          string   // "achievement" | "mention" | "thread_reply"
    to_user_id:        string
    // achievement fields
    achievement_slug:  string
    achievement_name:  string
    icon:              string
    // mention / thread_reply fields
    mentioned_by:      string
    replied_by:        string
    thread_title:      string
    thread_link:       string
    preview:           string
}

[<CLIMutable>]
type CreateThreadPayload = {
    PersonaId:        string
    ForumId:          string
    SystemDirectives: string
    Model:            string
    BotUserId:        string
    TopicHint:        string
}

// ── Worker ─────────────────────────────────────────────────────────────────────

type HeartbeatWorker(logger: ILogger<HeartbeatWorker>) =
    inherit BackgroundService()

    let jsonOpts =
        let o = JsonSerializerOptions()
        o.PropertyNameCaseInsensitive <- true
        o

    let anthropicKey () =
        let v = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        if String.IsNullOrWhiteSpace v then None else Some v

    let callClaude (apiKey: string) (model: string) (systemPrompt: string) (userMessage: string) : string Async =
        async {
            use http = new HttpClient()
            http.DefaultRequestHeaders.Add("x-api-key", apiKey)
            http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01")
            let req = {
                model      = model
                max_tokens = 1024
                system     = systemPrompt
                messages   = [{ role = "user"; content = userMessage }]
            }
            let body = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json")
            let! resp = http.PostAsync("https://api.anthropic.com/v1/messages", body) |> Async.AwaitTask
            let! json = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
            let parsed = JsonSerializer.Deserialize<ClaudeResponse>(json, jsonOpts)
            return parsed.content |> List.tryHead |> Option.map _.text |> Option.defaultValue ""
        }

    // ── Phase 1: Pre-run ────────────────────────────────────────────────────────

    let runModerationPhase (apiKey: string) =
        async {
            try
                use conn = Database.openConnection()
                use cmd = new Npgsql.NpgsqlCommand(
                    """SELECT id, content FROM forum_posts
                       WHERE state = 'pending'
                       LIMIT 50""", conn)
                use r = cmd.ExecuteReader()
                let posts = [ while r.Read() do yield r.GetGuid(0), r.GetString(1) ]
                if posts.IsEmpty then ()
                else
                    let batch =
                        posts
                        |> List.map (fun (id, content) ->
                            let stripped = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]+>", "")
                            sprintf """{"PostID":"%O","TextContent":%s}""" id (JsonSerializer.Serialize(stripped)))
                        |> String.concat ","
                    let systemPrompt = """You are an automated content moderation system. Evaluate the provided array of forum posts against the violation categories below. Operate with absolute objectivity. Evaluate the text exactly as written. Do not infer intent.

Violation Categories:
1. Harassment: Direct insults, derogatory language aimed at another user, or targeted harassment campaigns.
2. Hate Speech: Slurs or attacks based on race, religion, ethnicity, sexual orientation, gender identity, or disability.
3. Spam / Malicious Links: Nonsensical repetitive text, unsolicited commercial advertisements, or links to phishing/malware domains.
4. PII (Doxxing): Unredacted private information including phone numbers, physical addresses, or personal financial data.
5. Illegal Content: Text planning, encouraging, or instructing the commission of crimes.

If the text is combative but remains strictly focused on the topic (debate) rather than personal attacks, it is NOT a violation.

Return ONLY a JSON array with this exact schema per item:
{"PostID":"<uuid>","IsViolation":<bool>,"Category":<string|null>,"ConfidenceScore":<0.0-1.0>}"""

                    let userMsg = sprintf "[%s]" batch
                    let! rawResponse = callClaude apiKey "claude-haiku-4-5-20251001" systemPrompt userMsg
                    // Parse just the JSON array portion
                    let start = rawResponse.IndexOf('[')
                    let stop  = rawResponse.LastIndexOf(']')
                    if start >= 0 && stop > start then
                        let jsonSlice = rawResponse.[start..stop]
                        let results = JsonSerializer.Deserialize<ModerationResult list>(jsonSlice, jsonOpts)
                        use conn2 = Database.openConnection()
                        for result in results do
                            let newState =
                                if not result.IsViolation then "published"
                                elif result.ConfidenceScore >= 0.85 then "quarantined"
                                else "flagged"
                            use upd = new Npgsql.NpgsqlCommand(
                                "UPDATE forum_posts SET state = @s WHERE id = @id AND state = 'pending'", conn2)
                            upd.Parameters.AddWithValue("s",  newState)                  |> ignore
                            upd.Parameters.AddWithValue("id", Guid.Parse(result.PostID)) |> ignore
                            upd.ExecuteNonQuery() |> ignore
                        logger.LogInformation("Moderation: processed {Count} posts", results.Length)
            with ex ->
                logger.LogError(ex, "Moderation phase error")
        }

    let runPersonaPhase (apiKey: string) (batchLimit: int) (personaIntervalMin: int) =
        async {
            try
                // Enqueue GenerateReply jobs for due personas
                let personas = PersonaRepository.getPersonas()
                let now = DateTime.UtcNow
                for persona in personas |> List.filter (fun p -> p.Active) do
                    let isDue =
                        match persona.TriggerMode, persona.NextScheduledRun with
                        | "always", Some nsr -> nsr <= now
                        | "always", None     -> true
                        | _                  -> false
                    if isDue then
                        let forums = PersonaRepository.getPersonaForums persona.Id
                        for forumId in forums do
                            match persona.UserId with
                            | None -> ()
                            | Some uid ->
                                // Check for a recent thread (last 48 hours) to reply to
                                use conn = Database.openConnection()
                                use cmd = new Npgsql.NpgsqlCommand(
                                    """SELECT id, last_post_at FROM forum_threads
                                       WHERE forum_id = @fid
                                       ORDER BY last_post_at DESC NULLS LAST LIMIT 1""", conn)
                                cmd.Parameters.AddWithValue("fid", forumId) |> ignore
                                use r = cmd.ExecuteReader()
                                let threadResult =
                                    if r.Read() then
                                        let tid = r.GetGuid(0)
                                        let lastPost = if r.IsDBNull(1) then DateTime.MinValue else r.GetDateTime(1)
                                        Some (tid, lastPost)
                                    else None
                                r.Close()

                                match threadResult with
                                | Some (threadId, lastPost) when (DateTime.UtcNow - lastPost).TotalHours < 48.0 ->
                                    // Recent activity — reply to the thread
                                    let payload = JsonSerializer.Serialize({
                                        PersonaId        = persona.Id.ToString()
                                        ThreadId         = threadId.ToString()
                                        SystemDirectives = persona.SystemPrompt
                                        Model            = persona.Model
                                        BotUserId        = uid.ToString()
                                    })
                                    PersonaRepository.enqueueJob "GenerateReply" payload |> ignore
                                | _ ->
                                    // No recent thread — start a new one
                                    let payload = JsonSerializer.Serialize({
                                        PersonaId        = persona.Id.ToString()
                                        ForumId          = forumId.ToString()
                                        SystemDirectives = persona.SystemPrompt
                                        Model            = persona.Model
                                        BotUserId        = uid.ToString()
                                        TopicHint        = "Choose a topic relevant to this forum that would spark genuine discussion"
                                    })
                                    PersonaRepository.enqueueJob "CreateThread" payload |> ignore

                        // Schedule next run
                        use conn = Database.openConnection()
                        use cmd = new Npgsql.NpgsqlCommand(
                            "UPDATE ai_personas SET next_scheduled_run = now() + (@mins * interval '1 minute') WHERE id = @id", conn)
                        cmd.Parameters.AddWithValue("mins", personaIntervalMin) |> ignore
                        cmd.Parameters.AddWithValue("id", persona.Id) |> ignore
                        cmd.ExecuteNonQuery() |> ignore
            with ex ->
                logger.LogError(ex, "Persona scheduling phase error")
        }

    // ── Phase 2: Job processing ─────────────────────────────────────────────────

    let processGenerateReply (apiKey: string) (job: PersonaRepository.HeartbeatJob) =
        async {
            let payload = JsonSerializer.Deserialize<GenerateReplyPayload>(job.Payload, jsonOpts)
            let threadId = Guid.Parse(payload.ThreadId)
            let botUserId = Guid.Parse(payload.BotUserId)

            // Fetch thread context (last 10 posts)
            use conn = Database.openConnection()
            use cmd = new Npgsql.NpgsqlCommand(
                """SELECT fp.content FROM forum_posts fp
                   WHERE fp.thread_id = @tid AND fp.state = 'published'
                   ORDER BY fp.created_at DESC LIMIT 10""", conn)
            cmd.Parameters.AddWithValue("tid", threadId) |> ignore
            use r = cmd.ExecuteReader()
            let posts = [ while r.Read() do yield r.GetString(0) ] |> List.rev

            let context =
                posts
                |> List.map (fun p -> System.Text.RegularExpressions.Regex.Replace(p, "<[^>]+>", ""))
                |> String.concat "\n\n---\n\n"

            let userMsg = sprintf "Here is the recent forum thread context:\n\n%s\n\nPlease provide a helpful, on-topic reply." context

            let! reply = callClaude apiKey payload.Model payload.SystemDirectives userMsg

            if not (String.IsNullOrWhiteSpace reply) then
                let htmlReply = sprintf "<p>%s</p>" (reply.Replace("\n\n", "</p><p>").Replace("\n", "<br>"))
                match ForumRepository.createPost threadId botUserId htmlReply with
                | Some _ ->
                    PersonaRepository.completeJob job.Id
                    logger.LogInformation("Persona reply posted to thread {ThreadId}", threadId)
                | None ->
                    PersonaRepository.failJob job.Id "createPost returned None" job.MaxRetries job.RetryCount
        }

    let processSendEmail (job: PersonaRepository.HeartbeatJob) =
        async {
            let p = JsonSerializer.Deserialize<SendEmailPayload>(job.Payload, jsonOpts)
            match Guid.TryParse(p.to_user_id) with
            | false, _ ->
                PersonaRepository.failJob job.Id "Invalid to_user_id" job.MaxRetries job.RetryCount
            | true, userId ->
                match UserRepository.getUserById userId with
                | None ->
                    PersonaRepository.failJob job.Id "User not found" job.MaxRetries job.RetryCount
                | Some user ->
                    let displayName = user.DisplayName |> Option.defaultValue user.Email
                    let (subject, html) =
                        match p.template with
                        | "achievement" ->
                            sprintf "You earned the %s badge!" p.achievement_name,
                            Email.achievementEmailTemplate displayName p.icon p.achievement_name p.achievement_slug
                        | "mention" ->
                            sprintf "%s mentioned you in \"%s\"" p.mentioned_by p.thread_title,
                            Email.mentionEmailTemplate displayName p.mentioned_by p.thread_title p.thread_link p.preview
                        | "thread_reply" ->
                            sprintf "New reply in \"%s\"" p.thread_title,
                            Email.threadReplyEmailTemplate displayName p.replied_by p.thread_title p.thread_link p.preview
                        | unknown ->
                            sprintf "Notification from Lagdaemon", sprintf "<p>Template '%s' not implemented.</p>" unknown
                    let! sent = Email.sendEmail { To = user.Email; Subject = subject; HtmlBody = html }
                    if sent then
                        PersonaRepository.completeJob job.Id
                        logger.LogInformation("Email sent: template={Template} to={Email}", p.template, user.Email)
                    else
                        PersonaRepository.failJob job.Id "SES send returned false" job.MaxRetries job.RetryCount
        }

    let processCreateThread (apiKey: string) (job: PersonaRepository.HeartbeatJob) =
        async {
            let payload = JsonSerializer.Deserialize<CreateThreadPayload>(job.Payload, jsonOpts)
            let forumId   = Guid.Parse(payload.ForumId)
            let botUserId = Guid.Parse(payload.BotUserId)

            let userMsg =
                sprintf """You are about to start a new discussion thread in this forum community. Topic hint: "%s"

Generate a forum thread as a JSON object with exactly these two fields:
{"title": "<concise engaging thread title>", "content": "<opening post — 2 to 4 paragraphs, written naturally as a community member, no bullet lists>"}

Return only the JSON object, no other text.""" payload.TopicHint

            let! rawResponse = callClaude apiKey payload.Model payload.SystemDirectives userMsg

            let start = rawResponse.IndexOf('{')
            let stop  = rawResponse.LastIndexOf('}')
            if start >= 0 && stop > start then
                let jsonSlice = rawResponse.[start..stop]
                try
                    let parsed = JsonSerializer.Deserialize<{| title: string; content: string |}>(jsonSlice, jsonOpts)
                    if not (String.IsNullOrWhiteSpace parsed.title) && not (String.IsNullOrWhiteSpace parsed.content) then
                        let htmlContent = sprintf "<p>%s</p>" (parsed.content.Replace("\n\n", "</p><p>").Replace("\n", "<br>"))
                        match ForumRepository.createThread forumId botUserId parsed.title htmlContent with
                        | Some _ ->
                            PersonaRepository.completeJob job.Id
                            logger.LogInformation("Persona created thread '{Title}' in forum {ForumId}", parsed.title, forumId)
                        | None ->
                            PersonaRepository.failJob job.Id "createThread returned None" job.MaxRetries job.RetryCount
                    else
                        PersonaRepository.failJob job.Id "Parsed thread had empty title or content" job.MaxRetries job.RetryCount
                with ex ->
                    PersonaRepository.failJob job.Id (sprintf "JSON parse error: %s" ex.Message) job.MaxRetries job.RetryCount
            else
                PersonaRepository.failJob job.Id "No JSON object found in Claude response" job.MaxRetries job.RetryCount
        }

    let processJob (apiKey: string) (job: PersonaRepository.HeartbeatJob) =
        async {
            try
                match job.ActionType with
                | "GenerateReply"  -> do! processGenerateReply apiKey job
                | "CreateThread"   -> do! processCreateThread apiKey job
                | "SendEmail"      -> do! processSendEmail job
                | unknown ->
                    PersonaRepository.failJob job.Id (sprintf "Unknown action type: %s" unknown) job.MaxRetries job.RetryCount
            with ex ->
                PersonaRepository.failJob job.Id ex.Message job.MaxRetries job.RetryCount
                logger.LogError(ex, "Job {JobId} failed", job.Id)
        }

    // ── Phase 3: Patreon reconciliation (runs once per day) ────────────────────

    let patreonCreatorToken () =
        let v = Environment.GetEnvironmentVariable("PATREON_CREATOR_ACCESS_TOKEN")
        if String.IsNullOrWhiteSpace v then None else Some v

    let runPatreonReconciliation () =
        async {
            try
                let shouldRun =
                    use conn = Database.openConnection()
                    use cmd = new Npgsql.NpgsqlCommand(
                        "SELECT value FROM heartbeat_config WHERE key = 'patreon_reconcile_last_run'", conn)
                    match cmd.ExecuteScalar() with
                    | null | :? DBNull -> true
                    | v ->
                        match System.DateTime.TryParse(string v) with
                        | true, dt -> (DateTime.UtcNow - dt).TotalHours >= 24.0
                        | _ -> true

                if shouldRun then
                    match patreonCreatorToken() with
                    | None ->
                        logger.LogWarning("PATREON_CREATOR_ACCESS_TOKEN not set — skipping reconciliation")
                    | Some token ->
                        use http = new HttpClient()
                        http.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", token)

                        // Fetch campaign ID
                        let! campaignResp = http.GetStringAsync("https://www.patreon.com/api/oauth2/v2/campaigns") |> Async.AwaitTask
                        let campaignDoc = JsonDocument.Parse(campaignResp)
                        let campaignId =
                            campaignDoc.RootElement
                                .GetProperty("data")
                                .EnumerateArray()
                                |> Seq.tryHead
                                |> Option.map (fun el -> el.GetProperty("id").GetString())

                        match campaignId with
                        | None ->
                            logger.LogWarning("Patreon reconciliation: no campaign found")
                        | Some cid ->
                            // Fetch all active members (paginated)
                            let mutable cursor = ""
                            let mutable allMembers : (string * string option) list = []
                            let mutable keepGoing = true

                            while keepGoing do
                                let url =
                                    let base_ = sprintf "https://www.patreon.com/api/oauth2/v2/campaigns/%s/members?include=currently_entitled_tiers&fields%%5Bmember%%5D=patron_status&page%%5Bcount%%5D=500" cid
                                    if cursor = "" then base_ else sprintf "%s&page%%5Bcursor%%5D=%s" base_ cursor
                                let! resp = http.GetStringAsync(url) |> Async.AwaitTask
                                let doc = JsonDocument.Parse(resp)

                                let page =
                                    doc.RootElement.GetProperty("data").EnumerateArray()
                                    |> Seq.choose (fun el ->
                                        let uuid   = el.GetProperty("id").GetString()
                                        let status = el.GetProperty("attributes").GetProperty("patron_status").GetString()
                                        if status = "active_patron" then
                                            let tierId =
                                                try
                                                    el.GetProperty("relationships")
                                                      .GetProperty("currently_entitled_tiers")
                                                      .GetProperty("data")
                                                      .EnumerateArray()
                                                    |> Seq.tryHead
                                                    |> Option.map (fun t -> t.GetProperty("id").GetString())
                                                with _ -> None
                                            Some (uuid, tierId)
                                        else None)
                                    |> Seq.toList

                                allMembers <- allMembers @ page

                                let nextCursor =
                                    try
                                        let mutable pagination = Unchecked.defaultof<JsonElement>
                                        let mutable cursors    = Unchecked.defaultof<JsonElement>
                                        let mutable next       = Unchecked.defaultof<JsonElement>
                                        if doc.RootElement.GetProperty("meta").TryGetProperty("pagination", &pagination) &&
                                           pagination.TryGetProperty("cursors", &cursors) &&
                                           cursors.TryGetProperty("next", &next) then
                                            Some (next.GetString())
                                        else None
                                    with _ -> None

                                match nextCursor with
                                | Some c when not (String.IsNullOrEmpty c) -> cursor <- c
                                | _ -> keepGoing <- false

                            // Reconcile against DB
                            let activeUuids = allMembers |> List.map fst |> Set.ofList
                            use conn = Database.openConnection()
                            use cmd = new Npgsql.NpgsqlCommand(
                                "SELECT patreon_uuid, patreon_tier_id FROM users WHERE patreon_uuid IS NOT NULL", conn)
                            use r = cmd.ExecuteReader()
                            let dbPatrons = [
                                while r.Read() do
                                    yield r.GetString(0), if r.IsDBNull(1) then None else Some (r.GetString(1))
                            ]
                            r.Close()

                            let mutable demoted = 0
                            let mutable updated = 0

                            for (dbUuid, dbTier) in dbPatrons do
                                if not (Set.contains dbUuid activeUuids) then
                                    use upd = new Npgsql.NpgsqlCommand(
                                        "UPDATE users SET patreon_tier_id = NULL WHERE patreon_uuid = @uuid", conn)
                                    upd.Parameters.AddWithValue("uuid", dbUuid) |> ignore
                                    upd.ExecuteNonQuery() |> ignore
                                    demoted <- demoted + 1
                                else
                                    let apiTierId = allMembers |> List.tryFind (fst >> (=) dbUuid) |> Option.bind snd
                                    let resolvedTier =
                                        match apiTierId with
                                        | None -> None
                                        | Some pid ->
                                            use lc = Database.openConnection()
                                            use lk = new Npgsql.NpgsqlCommand(
                                                "SELECT tier_id FROM patreon_tiers WHERE patreon_id = @pid", lc)
                                            lk.Parameters.AddWithValue("pid", pid) |> ignore
                                            match lk.ExecuteScalar() with
                                            | null | :? DBNull -> None
                                            | v -> Some (string v)
                                    if resolvedTier <> dbTier then
                                        use upd = new Npgsql.NpgsqlCommand(
                                            "UPDATE users SET patreon_tier_id = @tier WHERE patreon_uuid = @uuid", conn)
                                        upd.Parameters.AddWithValue("tier", resolvedTier |> Option.toObj) |> ignore
                                        upd.Parameters.AddWithValue("uuid", dbUuid) |> ignore
                                        upd.ExecuteNonQuery() |> ignore
                                        updated <- updated + 1

                            // Record last run time
                            use upsert = new Npgsql.NpgsqlCommand("""
                                INSERT INTO heartbeat_config (key, value) VALUES ('patreon_reconcile_last_run', @v)
                                ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value
                            """, conn)
                            upsert.Parameters.AddWithValue("v", DateTime.UtcNow.ToString("O")) |> ignore
                            upsert.ExecuteNonQuery() |> ignore

                            logger.LogInformation(
                                "Patreon reconciliation: {Active} active patrons, {Demoted} demoted, {Updated} tier updates",
                                allMembers.Length, demoted, updated)
            with ex ->
                logger.LogError(ex, "Patreon reconciliation error")
        }

    // ── Phase 4: Cleanup ────────────────────────────────────────────────────────

    let runCleanupPhase () =
        async {
            try
                let pruned = PersonaRepository.pruneOldFailedJobs()
                if pruned > 0 then
                    logger.LogInformation("Cleanup: pruned {Count} old failed jobs", pruned)
            with ex ->
                logger.LogError(ex, "Cleanup phase error")
        }

    // ── Main loop ───────────────────────────────────────────────────────────────

    override _.ExecuteAsync(ct) =
        task {
            logger.LogInformation("HeartbeatWorker starting")
            while not ct.IsCancellationRequested do
                try
                    let config = PersonaRepository.getConfig()
                    let intervalMin        = config |> Map.tryFind "interval_minutes"        |> Option.defaultValue "5"   |> int
                    let personaIntervalMin = config |> Map.tryFind "persona_interval_minutes" |> Option.defaultValue "660" |> int
                    let batchLimit         = config |> Map.tryFind "batch_limit"              |> Option.defaultValue "10"  |> int
                    let personaOn   = config |> Map.tryFind "persona_phase_active"    |> Option.defaultValue "true" = "true"
                    let moderateOn  = config |> Map.tryFind "moderation_phase_active" |> Option.defaultValue "true" = "true"
                    let cleanupOn   = config |> Map.tryFind "cleanup_phase_active"    |> Option.defaultValue "true" = "true"

                    // Phase A: Achievements (no API key required)
                    let achieveOn = config |> Map.tryFind "achievement_phase_active" |> Option.defaultValue "true" = "true"
                    if achieveOn then
                        try
                            AchievementEngine.runBatch()
                            AchievementEngine.dispatchNotifications()
                            logger.LogInformation("Achievement phase complete")
                        with ex ->
                            logger.LogError(ex, "Achievement phase error")

                    match anthropicKey() with
                    | None ->
                        logger.LogWarning("ANTHROPIC_API_KEY not set — skipping heartbeat phases")
                    | Some apiKey ->
                        // Phase 1: Pre-run
                        if moderateOn then do! runModerationPhase apiKey
                        if personaOn  then do! runPersonaPhase apiKey batchLimit personaIntervalMin

                        // Phase 2: Queue processing
                        let jobs = PersonaRepository.fetchAndLockJobs batchLimit
                        for job in jobs do
                            do! processJob apiKey job

                        // Phase 3: Cleanup
                        if cleanupOn then do! runCleanupPhase()

                    // Phase B: Patreon reconciliation (no Anthropic key needed, runs daily)
                    do! runPatreonReconciliation()

                    do! System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(float intervalMin), ct) |> Async.AwaitTask
                with
                | :? System.Threading.Tasks.TaskCanceledException -> ()
                | ex -> logger.LogError(ex, "HeartbeatWorker iteration error")
        }
