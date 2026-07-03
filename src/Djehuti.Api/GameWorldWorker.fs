module Djehuti.Api.GameWorldWorker

open System
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Djehuti.Core

let private aiErrorText error =
    match error with
    | AiConnectionUnavailable message -> message
    | AiAuthenticationFailed message -> message
    | AiRateLimited message -> message
    | AiProviderRejectedRequest message -> message
    | AiProviderFailure message -> message
    | AiResponseInvalid message -> message

let private tryOpenAiOptions () =
    match OpenAiResponses.tryOptionsFromEnvironment() with
    | Ok options -> Some options
    | Error _ -> None

let private submitOpenAiText (options: OpenAiResponsesOptions) (model: string) (systemPrompt: string) (userMessage: string) =
    async {
        use http = new HttpClient()
        http.Timeout <- TimeSpan.FromSeconds(90.0)
        let connection = OpenAiResponsesConnection(http, options) :> IAiConnection
        let request =
            { ConnectionId = AiConnectionId "mud-construction-openai"
              ConversationId = None
              Model = Some (ModelId model)
              Messages = [ Ai.system systemPrompt; Ai.user userMessage ]
              Temperature = Some 0.8
              MaxOutputTokens = Some 1200
              Metadata = Map.empty }
        let! response = connection.Submit request
        return response |> Result.map _.Content
    }

let private buildPrompt (builder: MudConstructionRepository.BuilderAgent) (anchor: MudConstructionRepository.AnchorRoom) (directive: MudConstructionRepository.RealmDirective option) =
    let directiveText =
        directive
        |> Option.map _.NormalizedInstruction
        |> Option.defaultValue "Expand the realm with a useful room that players can search, read, and talk in."

    let systemPrompt =
        $"""You are a world-building specialist for the Lagdaemon MUD.
Realm: {builder.RealmSlug}
Builder specialty: {builder.Specialty}

Return exactly one JSON object. Keep it grounded in the existing game world. The new room must feel adjacent to the anchor room and obey the standing directive.
Every response must include:
- roomName
- roomSlug
- roomDescription
- resourceName
- resourceSlug
- resourceDescription
- loreFigureName
- loreFigureSlug
- loreFigureDescription
- loreSpeech
- forwardExitLabel
- backwardExitLabel
- exitType

Rules:
- No markdown
- No lists
- Keep roomDescription to 2-4 sentences
- loreSpeech should sound like an in-world line a player can read or talk to
- exitType should usually be "passage", "stairs", "gate", or "portal"
- Make the content usable by a live text MUD"""

    let userPrompt =
        $"""Standing directive: {directiveText}
Anchor room: {anchor.RoomName} ({anchor.RoomSlug})
Anchor description: {anchor.RoomDescription |> Option.defaultValue "No additional anchor description provided."}
Required outward direction from anchor: {anchor.Direction}
Required return direction from new room: {anchor.ReverseDirection}

Build one room that this worker could add today."""

    systemPrompt, userPrompt

type GameWorldWorker(logger: ILogger<GameWorldWorker>) =
    inherit BackgroundService()

    override _.ExecuteAsync(ct) =
        task {
            logger.LogInformation("GameWorldWorker starting")
            while not ct.IsCancellationRequested do
                try
                    MudConstructionRepository.ensureBuilderRoster()
                    let queued = MudConstructionRepository.enqueueDueBuildJobs DateTime.UtcNow
                    if queued > 0 then
                        logger.LogInformation("GameWorldWorker queued {Count} construction jobs", queued)

                    let openAiOptions = tryOpenAiOptions ()
                    let jobs = MudConstructionRepository.fetchAndLockBuildJobs 2

                    for job in jobs do
                        match MudConstructionRepository.getBuilderAgent job.BuilderAgentId with
                        | None ->
                            MudConstructionRepository.failBuildJob job.Id "Builder agent not found." job.RetryCount
                        | Some builder ->
                            match MudConstructionRepository.tryPickAnchorRoom builder.RealmSlug with
                            | None ->
                                MudConstructionRepository.failBuildJob job.Id $"No buildable anchor room was found for realm '{builder.RealmSlug}'." job.RetryCount
                            | Some anchor ->
                                let directive = MudConstructionRepository.getLatestDirective builder.RealmSlug
                                let planResult =
                                    async {
                                        match openAiOptions with
                                        | None -> return Ok (MudConstructionRepository.fallbackPlan builder anchor directive)
                                        | Some options ->
                                            let systemPrompt, userPrompt = buildPrompt builder anchor directive
                                            let! responseResult = submitOpenAiText options builder.Model systemPrompt userPrompt
                                            match responseResult with
                                            | Error error ->
                                                logger.LogWarning("Construction AI call failed for {BuilderSlug}: {Message}", builder.Slug, aiErrorText error)
                                                return Ok (MudConstructionRepository.fallbackPlan builder anchor directive)
                                            | Ok rawResponse ->
                                                MudConstructionRepository.saveBuildPayload job.Id rawResponse
                                                match MudConstructionRepository.parseBuildPlan rawResponse with
                                                | Ok parsed -> return Ok parsed
                                                | Error parseError ->
                                                    logger.LogWarning("Construction AI response parse failed for {BuilderSlug}: {Message}", builder.Slug, parseError)
                                                    return Ok (MudConstructionRepository.fallbackPlan builder anchor directive)
                                    }
                                    |> Async.StartAsTask

                                let! resolved = planResult
                                match resolved with
                                | Error message ->
                                    MudConstructionRepository.failBuildJob job.Id message job.RetryCount
                                | Ok plan ->
                                    try
                                        let createdRoomId, summary = MudConstructionRepository.applyBuildPlan job builder anchor plan
                                        logger.LogInformation("GameWorldWorker completed job {JobId}: room {RoomId} created ({Summary})", job.Id, createdRoomId, summary)
                                    with ex ->
                                        logger.LogError(ex, "GameWorldWorker failed to apply build plan for job {JobId}", job.Id)
                                        MudConstructionRepository.failBuildJob job.Id ex.Message job.RetryCount

                    do! Task.Delay(TimeSpan.FromMinutes(5.0), ct)
                with
                | :? TaskCanceledException -> ()
                | ex ->
                    logger.LogError(ex, "GameWorldWorker iteration error")
                    do! Task.Delay(TimeSpan.FromSeconds(30.0), ct)
            return ()
        }
