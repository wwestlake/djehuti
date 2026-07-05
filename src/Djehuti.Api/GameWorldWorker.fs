module Djehuti.Api.GameWorldWorker

open System
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Djehuti.Core
open Djehuti.Api

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

// Realm-specific visual and architectural constraints, distilled from the
// realm design concept docs. Injected into the builder prompt so AI-
// generated rooms honor each realm's material palette, atmosphere, and
// exit-type conventions instead of relying on the model's generic sense of
// the realm slug.
let private realmAestheticGuide (realmSlug: string) =
    match realmSlug with
    | "the-veil" ->
        """Realm aesthetic - The Veil: a liminal, fractured dimension of shifting geometry and decaying industrial architecture. Heavy, layered, palette-knife texture. Materials: scrap metal, crumbling concrete, rusted iron, shattered glass. Atmosphere: thick hanging fog and static haze, industrial grays, muted rust, stark contrasting neon anomalies. Fixtures: twisted lampposts, jutting fire escapes, asymmetrical archways, fractured stairwells that seem to lead nowhere. Exit rendering: portal = a tear in heavy, textured air glowing with harsh cool light; passage = a narrow, claustrophobic alley hemmed in by looming abstract concrete; elevator = a rusted, exposed-cage lift on exposed gears and frayed cables."""
    | "the-wild-march" ->
        """Realm aesthetic - The Wild March: an untamed, highly vertical frontier where aggressive flora reclaims ancient ruins. Rhythmic, cyclic flow; giant roots and cascading vines weave over and under one another. Materials: massive timber, bioluminescent moss, petrified wood, overgrown weather-beaten stone. Atmosphere: dappled emerald light through a thick canopy, humid air choked with floating spores. Fixtures: enormous hollowed-out tree trunks, natural bridges of intertwined roots, ancient stone altars swallowed by vines. Exit rendering: stairs-up = a spiraling set of natural steps carved into the bark of a colossal tree; ladder = a sheer drop covered in thick, climbable vines anchored into stone; gate = a colossal, overgrown archway of woven branches calcified into wood-stone."""
    | "the-drowned-reach" ->
        """Realm aesthetic - The Drowned Reach: a submerged, abyssal environment of crushing pressure and aquatic decay - interconnected underwater facilities, sunken caverns, air-locked habitats. Deeply oppressive; artificial light fighting the crushing dark. Materials: heavy brass, riveted steel bulkheads, barnacle-encrusted glass, damp porous deep-sea rock. Atmosphere: extremely low visibility outside the immediate room, flickering sickly-yellow maritime lamps or deep-sea bioluminescence pressed against thick glass. Fixtures: massive pressure valves, circular vault doors, dripping pipe networks, condensation-slicked control consoles. Exit rendering: door = a heavy, circular airlock with a central locking wheel and warning lights; stairs-down = a grated metal spiral staircase descending into dark, knee-high pooling water; passage = a cylindrical, reinforced glass tunnel showing the murky ocean exterior on all sides."""
    | _ -> ""

let private buildPrompt (builder: MudConstructionRepository.BuilderAgent) (anchor: MudConstructionRepository.AnchorRoom) (directive: MudConstructionRepository.RealmDirective option) =
    let directiveText =
        directive
        |> Option.map _.NormalizedInstruction
        |> Option.defaultValue "Expand the realm with a useful room that players can search, read, and talk in."

    let aestheticGuide = realmAestheticGuide builder.RealmSlug
    let aestheticSection = if aestheticGuide = "" then "" else $"\n{aestheticGuide}\n"

    let systemPrompt =
        $"""You are a world-building specialist for the Lagdaemon MUD.
Realm: {builder.RealmSlug}
Builder specialty: {builder.Specialty}
{aestheticSection}
Return exactly one JSON object. Keep it grounded in the existing game world. The new room must feel adjacent to the anchor room and obey the standing directive. If a realm aesthetic is given above, the roomDescription must reflect its materials, atmosphere, and fixtures, and exitType should match the exit rendering conventions described.
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
    let mutable consecutiveDbFailures = 0

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

                    consecutiveDbFailures <- 0

                    do! Task.Delay(TimeSpan.FromMinutes(5.0), ct)
                with
                | :? TaskCanceledException -> ()
                | ex ->
                    if WorkerResilience.isDatabaseException ex then
                        consecutiveDbFailures <- consecutiveDbFailures + 1
                        let delay = WorkerResilience.backoffDelay consecutiveDbFailures
                        logger.LogWarning(ex, "GameWorldWorker database failure. Backing off for {DelaySeconds} seconds (attempt {Attempt})", int delay.TotalSeconds, consecutiveDbFailures)
                        do! Task.Delay(delay, ct)
                    else
                        logger.LogError(ex, "GameWorldWorker iteration error")
                        do! Task.Delay(WorkerResilience.shortRecoveryDelay, ct)
            return ()
        }
