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

    let runPersonaPhase (apiKey: string) (batchLimit: int) =
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
                            // Get latest thread in forum
                            use conn = Database.openConnection()
                            use cmd = new Npgsql.NpgsqlCommand(
                                "SELECT id FROM forum_threads WHERE forum_id = @fid ORDER BY last_post_at DESC NULLS LAST LIMIT 1", conn)
                            cmd.Parameters.AddWithValue("fid", forumId) |> ignore
                            use r = cmd.ExecuteReader()
                            if r.Read() then
                                let threadId = r.GetGuid(0)
                                match persona.UserId with
                                | Some uid ->
                                    let payload = JsonSerializer.Serialize({
                                        PersonaId        = persona.Id.ToString()
                                        ThreadId         = threadId.ToString()
                                        SystemDirectives = persona.SystemPrompt
                                        Model            = persona.Model
                                        BotUserId        = uid.ToString()
                                    })
                                    PersonaRepository.enqueueJob "GenerateReply" payload |> ignore
                                | None -> ()

                        // Schedule next run (+5 minutes default)
                        use conn = Database.openConnection()
                        use cmd = new Npgsql.NpgsqlCommand(
                            "UPDATE ai_personas SET next_scheduled_run = now() + interval '5 minutes' WHERE id = @id", conn)
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

    let processJob (apiKey: string) (job: PersonaRepository.HeartbeatJob) =
        async {
            try
                match job.ActionType with
                | "GenerateReply" -> do! processGenerateReply apiKey job
                | unknown ->
                    PersonaRepository.failJob job.Id (sprintf "Unknown action type: %s" unknown) job.MaxRetries job.RetryCount
            with ex ->
                PersonaRepository.failJob job.Id ex.Message job.MaxRetries job.RetryCount
                logger.LogError(ex, "Job {JobId} failed", job.Id)
        }

    // ── Phase 3: Cleanup ────────────────────────────────────────────────────────

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
                    let intervalMin = config |> Map.tryFind "interval_minutes" |> Option.defaultValue "5" |> int
                    let batchLimit  = config |> Map.tryFind "batch_limit"       |> Option.defaultValue "10" |> int
                    let personaOn   = config |> Map.tryFind "persona_phase_active"    |> Option.defaultValue "true" = "true"
                    let moderateOn  = config |> Map.tryFind "moderation_phase_active" |> Option.defaultValue "true" = "true"
                    let cleanupOn   = config |> Map.tryFind "cleanup_phase_active"    |> Option.defaultValue "true" = "true"

                    match anthropicKey() with
                    | None ->
                        logger.LogWarning("ANTHROPIC_API_KEY not set — skipping heartbeat phases")
                    | Some apiKey ->
                        // Phase 1: Pre-run
                        if moderateOn then do! runModerationPhase apiKey
                        if personaOn  then do! runPersonaPhase apiKey batchLimit

                        // Phase 2: Queue processing
                        let jobs = PersonaRepository.fetchAndLockJobs batchLimit
                        for job in jobs do
                            do! processJob apiKey job

                        // Phase 3: Cleanup
                        if cleanupOn then do! runCleanupPhase()

                    do! System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(float intervalMin), ct) |> Async.AwaitTask
                with
                | :? System.Threading.Tasks.TaskCanceledException -> ()
                | ex -> logger.LogError(ex, "HeartbeatWorker iteration error")
        }
