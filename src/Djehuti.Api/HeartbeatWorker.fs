module Djehuti.Api.HeartbeatWorker

open System
open System.Globalization
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Djehuti.Core
open Djehuti.Api

// OpenAI helpers

let private supportedPersonaModels =
    set [ "gpt-4.1"; "gpt-4.1-mini"; "gpt-4o-mini" ]

let private resolvePersonaModel (options: OpenAiResponsesOptions) (model: string) =
    let trimmed = if isNull model then "" else model.Trim()
    if String.IsNullOrWhiteSpace trimmed then
        options.Model
    elif supportedPersonaModels.Contains trimmed then
        trimmed
    else
        "gpt-4o-mini"

let private aiErrorText error =
    match error with
    | AiConnectionUnavailable message -> message
    | AiAuthenticationFailed message -> message
    | AiRateLimited message -> message
    | AiProviderRejectedRequest message -> message
    | AiProviderFailure message -> message
    | AiResponseInvalid message -> message

let private submitOpenAiText
    (options: OpenAiResponsesOptions)
    (model: string)
    (systemPrompt: string)
    (userMessage: string)
    : Async<Result<string, AiConnectionError>> =
    async {
        use http = new HttpClient()
        http.Timeout <- TimeSpan.FromSeconds(90.0)

        let connection =
            OpenAiResponsesConnection(http, options) :> IAiConnection

        let resolvedModel = resolvePersonaModel options model

        let request =
            { ConnectionId = AiConnectionId "heartbeat-openai"
              ConversationId = None
              Model = Some(ModelId resolvedModel)
              Messages = [ Ai.system systemPrompt; Ai.user userMessage ]
              Temperature = None
              MaxOutputTokens = Some 1024
              Metadata = Map.empty }

        let! response = connection.Submit request

        return response |> Result.map _.Content
    }

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
    let mutable consecutiveDbFailures = 0

    let jsonOpts =
        let o = JsonSerializerOptions()
        o.PropertyNameCaseInsensitive <- true
        o

    let tryOpenAiOptions () =
        match OpenAiResponses.tryOptionsFromEnvironment() with
        | Ok options -> Some options
        | Error _ -> None

    let tryParseUtcInstant (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            match DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) with
            | true, parsed -> Some (DateTime.SpecifyKind(parsed, DateTimeKind.Utc))
            | _ -> None

    let getConfigInt (config: Map<string, string>) (key: string) (fallback: int) =
        match Map.tryFind key config with
        | Some value when not (String.IsNullOrWhiteSpace value) ->
            match Int32.TryParse value with
            | true, parsed -> parsed
            | _ -> fallback
        | _ -> fallback

    let isDjehuti (persona: PersonaRepository.AiPersona) =
        persona.Slug.Equals("djehuti", StringComparison.OrdinalIgnoreCase)

    let isWithinWorkWindow (persona: PersonaRepository.AiPersona) (nowUtc: DateTime) =
        match persona.WorkTimezone, persona.WorkStartHour, persona.WorkWindowHours with
        | Some timezone, Some startHour, Some windowHours when windowHours > 0 ->
            try
                let tz = TimeZoneInfo.FindSystemTimeZoneById(timezone)
                let localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz)
                let startHour = ((startHour % 24) + 24) % 24
                if windowHours >= 24 then
                    true
                else
                    let endHour = (startHour + windowHours) % 24
                    if startHour < endHour then
                        localNow.Hour >= startHour && localNow.Hour < endHour
                    else
                        localNow.Hour >= startHour || localNow.Hour < endHour
            with _ ->
                false
        | _ -> false

    let nextWorkWindowUtc (persona: PersonaRepository.AiPersona) (nowUtc: DateTime) =
        match persona.WorkTimezone, persona.WorkStartHour, persona.WorkWindowHours with
        | Some timezone, Some startHour, Some windowHours when windowHours > 0 ->
            try
                let tz = TimeZoneInfo.FindSystemTimeZoneById(timezone)
                let localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz)
                let startHour = ((startHour % 24) + 24) % 24
                let todayStart =
                    DateTime(localNow.Year, localNow.Month, localNow.Day, startHour, 0, 0, DateTimeKind.Unspecified)
                let nextStart =
                    if localNow < todayStart then todayStart else todayStart.AddDays(1.0)
                TimeZoneInfo.ConvertTimeToUtc(nextStart, tz)
            with _ ->
                nowUtc.AddHours(24.0)
        | _ ->
            nowUtc.AddHours(24.0)

    let pickRandom (items: 'a list) =
        match items with
        | [] -> None
        | values -> Some values.[Random.Shared.Next(values.Length)]

    let tryGetLatestThreadInForum (forumId: Guid) =
        use conn = Database.openConnection()
        use cmd = new Npgsql.NpgsqlCommand(
            """SELECT id, last_post_at FROM forum_threads
               WHERE forum_id = @fid
               ORDER BY last_post_at DESC NULLS LAST LIMIT 1""", conn)
        cmd.Parameters.AddWithValue("fid", forumId) |> ignore
        use r = cmd.ExecuteReader()
        if r.Read() then
            let threadId = r.GetGuid(0)
            let lastPostAt =
                if r.IsDBNull(1) then
                    DateTime.MinValue
                else
                    r.GetDateTime(1)
            Some (threadId, lastPostAt)
        else
            None

    let setPersonaNextRun (personaId: Guid) (nextRun: DateTime) =
        use conn = Database.openConnection()
        use cmd = new Npgsql.NpgsqlCommand(
            "UPDATE ai_personas SET next_scheduled_run = @nextRun WHERE id = @id", conn)
        cmd.Parameters.AddWithValue("nextRun", nextRun) |> ignore
        cmd.Parameters.AddWithValue("id", personaId) |> ignore
        cmd.ExecuteNonQuery() |> ignore

    let setPersonaPhaseLastRun (nowUtc: DateTime) =
        use conn = Database.openConnection()
        use cmd = new Npgsql.NpgsqlCommand(
            """INSERT INTO heartbeat_config (key, value)
               VALUES ('persona_phase_last_run', @value)
               ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value""", conn)
        cmd.Parameters.AddWithValue("value", nowUtc.ToString("O")) |> ignore
        cmd.ExecuteNonQuery() |> ignore

    // ── Phase 1: Pre-run ────────────────────────────────────────────────────────

    let runModerationPhase (options: OpenAiResponsesOptions) =
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
                    let! rawResponseResult = submitOpenAiText options "gpt-4.1" systemPrompt userMsg

                    match rawResponseResult with
                    | Error error ->
                        logger.LogError("Moderation request failed: {Message}", aiErrorText error)
                    | Ok rawResponse ->
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

    let runPersonaPhase (_: OpenAiResponsesOptions) (_: int) (personaIntervalMin: int) =
        async {
            try
                let now = DateTime.UtcNow
                let config = PersonaRepository.getConfig()
                let intervalMinutes = getConfigInt config "persona_interval_minutes" personaIntervalMin
                let lastRun =
                    config
                    |> Map.tryFind "persona_phase_last_run"
                    |> Option.bind tryParseUtcInstant

                let shouldRun =
                    match lastRun with
                    | None -> true
                    | Some previous -> (now - previous).TotalMinutes >= float intervalMinutes

                if shouldRun then
                    let eligiblePersonas =
                        PersonaRepository.getPersonas()
                        |> List.filter (fun persona ->
                            persona.Active
                            && persona.TriggerMode = "always"
                            && match persona.NextScheduledRun with
                               | Some nextRun -> nextRun <= now
                               | None -> true)
                        |> List.choose (fun persona ->
                            let forums = PersonaRepository.getPersonaForums persona.Id
                            if forums.IsEmpty then
                                None
                            else
                                let inWindow = if isDjehuti persona then true else isWithinWorkWindow persona now
                                if inWindow then Some (persona, forums) else None)

                    match pickRandom eligiblePersonas with
                    | None ->
                        logger.LogInformation("Persona phase found no eligible personas on shift")
                    | Some (persona, forums) ->
                        match persona.UserId, pickRandom forums with
                        | Some uid, Some forumId ->
                            match tryGetLatestThreadInForum forumId with
                            | Some (threadId, lastPost) when (now - lastPost).TotalHours < 48.0 ->
                                let payload = JsonSerializer.Serialize({
                                    PersonaId        = persona.Id.ToString()
                                    ThreadId         = threadId.ToString()
                                    SystemDirectives = persona.SystemPrompt
                                    Model            = persona.Model
                                    BotUserId        = uid.ToString()
                                })
                                match PersonaRepository.enqueueJob "GenerateReply" payload with
                                | Some job ->
                                    let nextRun = if isDjehuti persona then now.AddHours(1.0) else nextWorkWindowUtc persona now
                                    setPersonaNextRun persona.Id nextRun
                                    setPersonaPhaseLastRun now
                                    logger.LogInformation("Persona phase queued {ActionType} job {JobId} for {Persona}", job.ActionType, job.Id, persona.Name)
                                | None ->
                                    logger.LogWarning("Persona phase could not enqueue a reply job for {Persona}", persona.Name)
                            | _ ->
                                let payload = JsonSerializer.Serialize({
                                    PersonaId        = persona.Id.ToString()
                                    ForumId          = forumId.ToString()
                                    SystemDirectives = persona.SystemPrompt
                                    Model            = persona.Model
                                    BotUserId        = uid.ToString()
                                    TopicHint        = "Choose a topic relevant to this forum that would spark genuine discussion"
                                })
                                match PersonaRepository.enqueueJob "CreateThread" payload with
                                | Some job ->
                                    let nextRun = if isDjehuti persona then now.AddHours(1.0) else nextWorkWindowUtc persona now
                                    setPersonaNextRun persona.Id nextRun
                                    setPersonaPhaseLastRun now
                                    logger.LogInformation("Persona phase queued {ActionType} job {JobId} for {Persona}", job.ActionType, job.Id, persona.Name)
                                | None ->
                                    logger.LogWarning("Persona phase could not enqueue a thread job for {Persona}", persona.Name)
                        | _ ->
                            logger.LogWarning("Persona {Persona} was selected but lacks a bot user or forum scope", persona.Name)
                else
                    logger.LogInformation("Persona phase skipped because the last run was within {IntervalMinutes} minutes", intervalMinutes)
            with ex ->
                logger.LogError(ex, "Persona scheduling phase error")
        }
    let processGenerateReply (options: OpenAiResponsesOptions) (job: PersonaRepository.HeartbeatJob) =
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

            let! replyResult = submitOpenAiText options payload.Model payload.SystemDirectives userMsg

            match replyResult with
            | Error error ->
                PersonaRepository.failJob job.Id (aiErrorText error) job.MaxRetries job.RetryCount
            | Ok reply ->
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

    let processCreateThread (options: OpenAiResponsesOptions) (job: PersonaRepository.HeartbeatJob) =
        async {
            let payload = JsonSerializer.Deserialize<CreateThreadPayload>(job.Payload, jsonOpts)
            let forumId   = Guid.Parse(payload.ForumId)
            let botUserId = Guid.Parse(payload.BotUserId)

            let userMsg =
                sprintf """You are about to start a new discussion thread in this forum community. Topic hint: "%s"

Generate a forum thread as a JSON object with exactly these two fields:
{"title": "<concise engaging thread title>", "content": "<opening post — 2 to 4 paragraphs, written naturally as a community member, no bullet lists>"}

Return only the JSON object, no other text.""" payload.TopicHint

            let! rawResponseResult = submitOpenAiText options payload.Model payload.SystemDirectives userMsg

            match rawResponseResult with
            | Error error ->
                PersonaRepository.failJob job.Id (aiErrorText error) job.MaxRetries job.RetryCount
            | Ok rawResponse ->
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
                    PersonaRepository.failJob job.Id "No JSON object found in OpenAI response" job.MaxRetries job.RetryCount
        }

    let processJob (openAiOptions: OpenAiResponsesOptions option) (job: PersonaRepository.HeartbeatJob) =
        async {
            try
                match job.ActionType with
                | "SendEmail" -> do! processSendEmail job
                | "Ping"      -> PersonaRepository.completeJob job.Id
                | "GenerateReply" | "CreateThread" ->
                    match openAiOptions with
                    | None ->
                        logger.LogWarning("Skipping {ActionType} job {JobId} — OPENAI_API_KEY not set", job.ActionType, job.Id)
                        // Return job to pending so it can be retried when key is available
                        PersonaRepository.failJob job.Id "OPENAI_API_KEY not configured" job.MaxRetries job.RetryCount
                    | Some key ->
                        match job.ActionType with
                        | "GenerateReply" -> do! processGenerateReply key job
                        | _               -> do! processCreateThread key job
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
                    let personaIntervalMin = getConfigInt config "persona_interval_minutes" 60
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

                    let openAiOptions = tryOpenAiOptions()

                    // Phase 1: Pre-run (requires OpenAI key)
                    match openAiOptions with
                    | None ->
                        logger.LogWarning("OPENAI_API_KEY not set — skipping moderation and persona phases")
                    | Some options ->
                        if moderateOn then do! runModerationPhase options
                        if personaOn  then do! runPersonaPhase options batchLimit personaIntervalMin

                    // Phase 2: Queue processing (SendEmail and Ping work without a key)
                    let jobs = PersonaRepository.fetchAndLockJobs batchLimit
                    for job in jobs do
                        do! processJob openAiOptions job

                    // Phase 3: Cleanup (always runs — resets stuck Processing jobs)
                    if cleanupOn then do! runCleanupPhase()

                    // Phase B: Patreon reconciliation (no OpenAI key needed, runs daily)
                    do! runPatreonReconciliation()

                    consecutiveDbFailures <- 0

                    do! System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(float intervalMin), ct) |> Async.AwaitTask
                with
                | :? System.Threading.Tasks.TaskCanceledException -> ()
                | ex ->
                    if WorkerResilience.isDatabaseException ex then
                        consecutiveDbFailures <- consecutiveDbFailures + 1
                        let delay = WorkerResilience.backoffDelay consecutiveDbFailures
                        logger.LogWarning(ex, "HeartbeatWorker database failure. Backing off for {DelaySeconds} seconds (attempt {Attempt})", int delay.TotalSeconds, consecutiveDbFailures)
                        do! System.Threading.Tasks.Task.Delay(delay, ct) |> Async.AwaitTask
                    else
                        logger.LogError(ex, "HeartbeatWorker iteration error")
                        do! System.Threading.Tasks.Task.Delay(WorkerResilience.shortRecoveryDelay, ct) |> Async.AwaitTask
        }
