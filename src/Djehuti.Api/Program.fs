open System
open System.IO
open System.Net.Http
open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Djehuti.Api
open Djehuti.Core

// Constant-time string comparison for HMAC verification (prevent timing attacks)
let compareStringsConstantTime (a: string) (b: string) =
    if a.Length <> b.Length then false
    else
        let mutable result = 0
        for i = 0 to a.Length - 1 do
            result <- result ||| (int a.[i] ^^^ int b.[i])
        result = 0

[<CLIMutable>]
type RenameDataSetRequest =
    { Name: string
      Description: string }

[<CLIMutable>]
type SaveDataSetRequest =
    { Name: string
      Description: string
      SourceKind: string
      TurnCount: int
      DatasetJson: string }

[<CLIMutable>]
type AnalyzeRequest =
    { DatasetJson: string }

[<CLIMutable>]
type AnalystRequest =
    { DatasetJson: string
      Question: string
      Model: string
      Temperature: Nullable<float>
      MaxOutputTokens: Nullable<int> }

[<CLIMutable>]
type SummaryDto =
    { SourceId: string
      SourceName: string
      SourceKind: string
      SessionId: string
      ModelId: string
      TurnCount: int
      VelocityCount: int
      GapCount: int
      AveragePromptResponseCosine: float
      AverageWordCountDelta: float }

[<CLIMutable>]
type TurnMetricDto =
    { SequenceIndex: int
      SessionId: string
      Prompt: string
      Response: string
      PromptWordCount: int
      ResponseWordCount: int
      WordCountDelta: int
      SharedWordCount: int
      PromptResponseCosine: float
      JaccardSimilarity: float
      EditSimilarity: float
      VelocityFromPrevious: Nullable<float>
      Strategy: string
      ContaminationDepth: string
      SourceId: string }

[<CLIMutable>]
type VelocityPointDto =
    { SequenceIndex: int
      SessionId: string
      Value: float
      Basis: string }

[<CLIMutable>]
type ConstantDto =
    { Name: string
      Value: string }

[<CLIMutable>]
type MeasurementReportItemDto =
    { Name: string
      Value: Nullable<float>
      Basis: string
      Assumptions: string list
      RefusalReason: string }

[<CLIMutable>]
type MeasurementReportDto =
    { Subject: string
      Items: MeasurementReportItemDto list
      Warnings: string list }

[<CLIMutable>]
type AttractorEventDto =
    { TurnId: string
      SequenceIndex: int
      Description: string
      TorsionalResistanceValue: Nullable<float>
      TorsionalResistanceBasis: string
      TorsionalResistanceKind: string
      Assumptions: string list }

[<CLIMutable>]
type AnalyzeResponse =
    { Summary: SummaryDto
      Turns: TurnMetricDto list
      Velocities: VelocityPointDto list
      Constants: ConstantDto list
      Reports: MeasurementReportDto list
      AttractorEvents: AttractorEventDto list
      Warnings: string list }

[<CLIMutable>]
type AnalystEvidenceDto =
    { Label: string
      Value: string
      Source: string }

[<CLIMutable>]
type AnalystResponse =
    { Answer: string
      Evidence: AnalystEvidenceDto list
      Model: string
      Metadata: Map<string, string> }

type AnalysisRun =
    { Response: AnalyzeResponse
      Context: DjehutiAnalysisContext }

// DataSetCatalogItem and DataLibrary are now in DataLibrary.fs (PostgreSQL-backed)

// ── Auth DTOs ────────────────────────────────────────────────────────────────

[<CLIMutable>]
type RegisterRequest =
    { Email: string
      Password: string
      HCaptchaToken: string }

[<CLIMutable>]
type LoginRequest =
    { Email: string
      Password: string }

[<CLIMutable>]
type VerifyEmailRequest =
    { Token: string }

[<CLIMutable>]
type PasswordResetRequest =
    { Email: string }

[<CLIMutable>]
type PasswordResetConfirmRequest =
    { Token: string
      Password: string }

[<CLIMutable>]
type UpdateProfileRequest =
    { DisplayName: string option
      Bio: string option
      Pronouns: string option
      Location: string option
      NotifyByEmail: bool }

type UserDto =
    { Id: string
      Email: string
      DisplayName: string option
      AvatarUrl: string option
      Bio: string option
      Pronouns: string option
      Location: string option
      Role: string
      Status: string
      CreatedAt: DateTime }

type PublicProfileDto =
    { Id: string
      DisplayName: string option
      AvatarUrl: string option
      Bio: string option
      Pronouns: string option
      Location: string option }

module Dto =
    let private unwrapSessionId (SessionId value) = value
    let private unwrapModelId (ModelId value) = value
    let private unwrapDataSourceId (DataSourceId value) = value
    let private unwrapTurnId (TurnId value) = value

    let sourceKindText kind =
        match kind with
        | LiveProvider -> "live-provider"
        | ReplayFile -> "replay-file"
        | BenchmarkHarness -> "benchmark-harness"
        | ManualTranscript -> "manual-transcript"
        | MessageQueue -> "message-queue"
        | UnknownSource value -> value

    let strategyText strategy =
        match strategy with
        | Seed -> "seed"
        | Natural -> "natural"
        | Shock -> "shock"
        | InterleavedWithHistory -> "interleaved"

    let contaminationText contamination =
        match contamination with
        | Clean -> "clean"
        | Contaminated count -> $"contaminated:{count}"

    let basisText basis =
        match basis with
        | DirectObservation -> "direct observation"
        | MarginalEstimate -> "marginal estimate"
        | GlobalCalibrationEstimate -> "global calibration"
        | LocalCalibrationEstimate _ -> "local calibration"
        | HypothesisDependent _ -> "hypothesis dependent"
        | ContaminatedTrajectory _ -> "contaminated trajectory"
        | Refused reason -> $"refused: {reason}"

    let assumptionText assumption =
        match assumption with
        | ContinuityOfPerturbationVelocity -> "continuity of perturbation velocity"
        | IsotropicEmbeddingCurvatureApproximation -> "isotropic embedding curvature approximation"
        | CouplingHypothesis name -> $"coupling hypothesis: {name}"
        | ProviderSuppliedMetadata name -> $"provider supplied metadata: {name}"
        | GlobalCalibrationFallback -> "global calibration fallback"
        | LocalCalibrationAssumption(ContextClassId contextClass) -> $"local calibration assumption: {contextClass}"
        | TokenGranularityAggregation statistic -> $"token granularity aggregation: {statistic}"

    let sourceText source =
        match source with
        | FromTurn turnId -> $"turn:{unwrapTurnId turnId}"
        | FromShockTrial(ShockTrialId id) -> $"shock:{id}"
        | FromForkedReplicationBatch(ForkedReplicationBatchId id) -> $"forked-batch:{id}"
        | FromCalibrationRecord(CalibrationRecordId id) -> $"calibration:{id}"
        | FromTextComparison comparisonId -> $"comparison:{comparisonId}"

    let reportItemDto (item: MeasurementReportItem) =
        { Name = item.Name
          Value =
            item.Value
            |> Option.map Nullable
            |> Option.defaultValue (Nullable())
          Basis = basisText item.Basis
          Assumptions = item.Assumptions |> List.map assumptionText
          RefusalReason = item.RefusalReason |> Option.defaultValue "" }

    let reportDto (report: MeasurementReport) =
        { Subject = report.Subject
          Items = report.Items |> List.map reportItemDto
          Warnings = report.Warnings }

    let analystEvidenceDto (evidence: DjehutiAnalystEvidence) =
        { Label = evidence.Label
          Value = evidence.Value
          Source =
            evidence.Source
            |> Option.map sourceText
            |> Option.defaultValue "" }

    let torsionalResistanceKindText kind =
        match kind with
        | MeasuredEscapeThreshold -> "measured escape threshold"
        | QualitativeEstimate -> "qualitative estimate"
        | ArchitecturallyInheritedEstimate -> "architecturally inherited estimate"

    let attractorEventDto sequenceByTurnId (event: AttractorEvent) =
        let resistance =
            event.TorsionalResistance

        { TurnId = unwrapTurnId event.TurnId
          SequenceIndex =
            sequenceByTurnId
            |> Map.tryFind event.TurnId
            |> Option.defaultValue -1
          Description = event.Description
          TorsionalResistanceValue =
            resistance
            |> Option.map (fun value -> Nullable value.Value)
            |> Option.defaultValue (Nullable())
          TorsionalResistanceBasis =
            resistance
            |> Option.map (fun value -> basisText value.Basis)
            |> Option.defaultValue ""
          TorsionalResistanceKind = torsionalResistanceKindText event.TorsionalResistanceKind
          Assumptions = event.Assumptions |> List.map assumptionText }

    let rec jsonValueText value =
        match value with
        | JsonNull -> "null"
        | JsonString value -> value
        | JsonNumber value -> string value
        | JsonBoolean value -> string value
        | JsonArray values ->
            values
            |> List.map jsonValueText
            |> String.concat ", "
            |> sprintf "[%s]"
        | JsonObject values ->
            values
            |> Map.toList
            |> List.map (fun (key, value) -> $"{key}: {jsonValueText value}")
            |> String.concat ", "
            |> sprintf "{%s}"

    let constantDto name value =
        { Name = name
          Value = jsonValueText value }

    let private constantFloat name fallback (constants: Map<string, JsonValue>) =
        constants
        |> Map.tryFind name
        |> Option.bind (function
            | JsonNumber value when not (Double.IsNaN value || Double.IsInfinity value) -> Some value
            | JsonString value ->
                match Double.TryParse value with
                | true, parsed when not (Double.IsNaN parsed || Double.IsInfinity parsed) -> Some parsed
                | _ -> None
            | _ -> None)
        |> Option.defaultValue fallback

    let sourceIdFromMetadata (turn: Turn) =
        turn.Metadata
        |> Map.tryFind "data_source_id"
        |> Option.defaultValue ""

    let private sessionIdsInEncounterOrder (dataSet: JsonInteractionDataSet) =
        dataSet.Interactions
        |> List.map _.SessionId
        |> List.fold
            (fun (seen, ordered) sessionId ->
                if Set.contains sessionId seen then
                    seen, ordered
                else
                    Set.add sessionId seen, ordered @ [ sessionId ])
            (Set.empty, [])
        |> snd

    let private loadSessionTurns context sessionId =
        StorageOps.listTurnsForSession sessionId
        |> Storage.run context
        |> Async.RunSynchronously

    let private measuredValueIsUsable (value: MeasuredValue) =
        match value.Basis with
        | Refused _ -> false
        | _ -> not (Double.IsNaN value.Value || Double.IsInfinity value.Value)

    let private loadTurnsBySession (context: StorageContext) (dataSet: JsonInteractionDataSet) =
        let sessionIds = sessionIdsInEncounterOrder dataSet

        let turnsBySession =
            sessionIds
            |> List.map (fun sessionId ->
                match loadSessionTurns context sessionId with
                | Ok turns -> sessionId, turns
                | Error error -> failwith $"Turn load failed: {error}")

        sessionIds, turnsBySession, turnsBySession |> List.collect snd

    let private computeVelocities (turnsBySession: (SessionId * Turn list) list) =
        turnsBySession
        |> List.collect (fun (_, sessionTurns) ->
            sessionTurns
            |> List.pairwise
            |> List.map (fun (previousTurn, currentTurn) ->
                currentTurn.Id,
                currentTurn.SessionId,
                currentTurn.SequenceIndex,
                Measurement.velocityFromTurnPair CosineDistance previousTurn currentTurn))

    let private computeObservableVectors (turnsBySession: (SessionId * Turn list) list) =
        turnsBySession
        |> List.collect (fun (_, sessionTurns) ->
            sessionTurns
            |> List.mapi (fun index turn ->
                let previousTurn =
                    if index = 0 then None else Some sessionTurns[index - 1]

                Measurement.observableVectorFromTurn CosineDistance previousTurn turn))

    let private detectAttractorEvents
        (context: StorageContext)
        (dataSet: JsonInteractionDataSet)
        (turns: Turn list)
        (observableVectors: ObservableVector list)
        =
        let trajectoryPoints =
            (turns, observableVectors)
            ||> List.zip
            |> List.map (fun (turn, vector) ->
                turn.SessionId, Measurement.trajectoryPointFromObservableVector turn.SequenceIndex vector)

        let curvaturePointsBySession =
            trajectoryPoints
            |> List.groupBy fst
            |> List.collect (fun (sessionId, points) ->
                points
                |> List.map snd
                |> List.windowed 3
                |> List.choose (function
                    | [ previous; current; next ] ->
                        let curvature = Measurement.discreteCurvature previous current next

                        if measuredValueIsUsable curvature then
                            Some
                                (sessionId,
                                 { current with
                                    Coordinates = current.Coordinates |> Map.add "kappa" curvature })
                        else
                            None
                    | _ -> None))

        let attractorThresholds =
            { StabilityMarginMaximum =
                constantFloat "attractorStabilityMarginMaximum" 0.10 dataSet.Constants
              TorsionalAccumulationMinimum =
                constantFloat "attractorTorsionalAccumulationMinimum" 0.60 dataSet.Constants }

        let detectedAttractorEvents =
            curvaturePointsBySession
            |> List.groupBy fst
            |> List.collect (fun (_, points) ->
                points
                |> List.map snd
                |> List.windowed 3
                |> List.choose (Measurement.detectAttractorApproach attractorThresholds))

        for event in detectedAttractorEvents do
            match
                StorageOps.saveAttractorEvent event
                |> Storage.run context
                |> Async.RunSynchronously
            with
            | Ok () -> ()
            | Error error -> failwith $"Attractor event save failed: {error}"

        turns
        |> List.collect (fun turn ->
            match
                StorageOps.listAttractorEventsByTurn turn.Id
                |> Storage.run context
                |> Async.RunSynchronously
            with
            | Ok events -> events
            | Error error -> failwith $"Attractor event load failed: {error}")

    let private reportObservableVectors (observableVectors: ObservableVector list) =
        observableVectors
        |> List.map (fun vector ->
            Measurement.reportObservableVector (unwrapTurnId vector.TurnId) vector)

    let private runAnalysisPipeline
        (dataSet: JsonInteractionDataSet)
        (context: StorageContext)
        (ingestionSummary: IngestionSummary)
        =
        let sessionIds, turnsBySession, turns = loadTurnsBySession context dataSet

        let comparisons =
            turns
            |> List.map (fun turn -> PromptToResponse(turn.Prompt, turn.Response))

        let corpus = TextMetrics.corpusMetrics comparisons
        let velocities = computeVelocities turnsBySession
        let observableVectors = computeObservableVectors turnsBySession
        let attractorEvents = detectAttractorEvents context dataSet turns observableVectors
        let measurementReports = reportObservableVectors observableVectors

        let warnings =
            [ if sessionIds.Length > 1 then
                yield $"{sessionIds.Length} sessions analyzed."
              if ingestionSummary.Gaps.Length > 0 then
                yield $"{ingestionSummary.Gaps.Length} logical-time gap(s) detected." ]

        sessionIds, turns, corpus, velocities, observableVectors, measurementReports, attractorEvents, warnings

    let analyzeRun (datasetJson: string) =
        let dataSet = JsonInterop.readDataSetFromString datasetJson
        let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UtcNow)
        let source = JsonDataSource(dataSet) :> IDataSource

        let ingestion =
            Ingestion.ingestDataSource source Threading.CancellationToken.None
            |> Storage.run context
            |> Async.RunSynchronously

        match ingestion with
        | Error error ->
            failwith $"Ingestion failed: {error}"
        | Ok ingestionSummary ->
            let sessionIds, turns, corpus, velocities, observableVectors, measurementReports, attractorEvents, warnings =
                runAnalysisPipeline dataSet context ingestionSummary

            let velocityByTurnId =
                velocities
                |> List.map (fun (turnId, _, _, measured) -> turnId, measured)
                |> Map.ofList

            let sequenceByTurnId =
                turns
                |> List.map (fun turn -> turn.Id, turn.SequenceIndex)
                |> Map.ofList

            let reports =
                measurementReports
                |> List.map reportDto

            let turnRows =
                (turns, corpus.MetricsByComparison)
                ||> List.zip
                |> List.map (fun (turn, metric) ->
                    let velocity =
                        velocityByTurnId
                        |> Map.tryFind turn.Id
                        |> Option.map (fun value -> Nullable value.Value)
                        |> Option.defaultValue (Nullable())

                    { SequenceIndex = turn.SequenceIndex
                      SessionId = unwrapSessionId turn.SessionId
                      Prompt = turn.Prompt.Text
                      Response = turn.Response.Text
                      PromptWordCount = metric.Left.WordCount
                      ResponseWordCount = metric.Right.WordCount
                      WordCountDelta = metric.WordCountDelta
                      SharedWordCount = metric.SharedWordCount
                      PromptResponseCosine = metric.CosineSimilarity
                      JaccardSimilarity = metric.JaccardSimilarity
                      EditSimilarity = metric.NormalizedEditSimilarity
                      VelocityFromPrevious = velocity
                      Strategy = strategyText turn.Strategy
                      ContaminationDepth = contaminationText turn.ContaminationDepth
                      SourceId = sourceIdFromMetadata turn })

            let velocityRows =
                velocities
                |> List.map (fun (_, sessionId, sequenceIndex, measured) ->
                    { SequenceIndex = sequenceIndex
                      SessionId = unwrapSessionId sessionId
                      Value = measured.Value
                      Basis = basisText measured.Basis })

            let constants =
                dataSet.Constants
                |> Map.toList
                |> List.map (fun (key, value) -> constantDto key value)

            let constantsContext =
                dataSet.Constants
                |> Map.map (fun _ value -> jsonValueText value)

            let sessionId =
                match sessionIds with
                | [] -> ""
                | [ sessionId ] -> unwrapSessionId sessionId
                | values -> values |> List.map unwrapSessionId |> String.concat ", "

            let modelId =
                dataSet.Interactions
                |> List.map _.ModelId
                |> List.distinct
                |> function
                    | [] -> ""
                    | [ modelId ] -> unwrapModelId modelId
                    | modelIds -> modelIds |> List.map unwrapModelId |> String.concat ", "

            { Response =
                { Summary =
                    { SourceId = unwrapDataSourceId dataSet.Source.Id
                      SourceName = dataSet.Source.Name
                      SourceKind = sourceKindText dataSet.Source.Kind
                      SessionId = sessionId
                      ModelId = modelId
                      TurnCount = turns.Length
                      VelocityCount = velocities.Length
                      GapCount = ingestionSummary.Gaps.Length
                      AveragePromptResponseCosine = corpus.AverageCosineSimilarity
                      AverageWordCountDelta = corpus.AverageWordCountDelta }
                  Turns = turnRows
                  Velocities = velocityRows
                  Constants = constants
                  Reports = reports
                  AttractorEvents =
                    attractorEvents
                    |> List.map (attractorEventDto sequenceByTurnId)
                  Warnings = warnings }
              Context =
                { Turns = turns
                  ObservableVectors = observableVectors
                  Reports = measurementReports
                  AttractorEvents = attractorEvents
                  Constants = constantsContext
                  Warnings = warnings } }

    let analyze datasetJson =
        (analyzeRun datasetJson).Response

module AnalystApi =
    let private errorText error =
        match error with
        | AiConnectionUnavailable message -> message
        | AiAuthenticationFailed message -> message
        | AiRateLimited message -> message
        | AiProviderRejectedRequest message -> message
        | AiProviderFailure message -> message
        | AiResponseInvalid message -> message

    let private errorStatus error =
        match error with
        | AiConnectionUnavailable _ -> 503
        | AiAuthenticationFailed _ -> 401
        | AiRateLimited _ -> 429
        | AiProviderRejectedRequest _ -> 400
        | AiProviderFailure _ -> 502
        | AiResponseInvalid _ -> 502

    let ask (request: AnalystRequest) =
        if String.IsNullOrWhiteSpace request.DatasetJson then
            Results.BadRequest("datasetJson is required")
        elif String.IsNullOrWhiteSpace request.Question then
            Results.BadRequest("question is required")
        else
            match OpenAiResponses.tryOptionsFromEnvironment() with
            | Error error ->
                Results.Problem(detail = errorText error, statusCode = errorStatus error, title = "Analyst AI unavailable")
            | Ok options ->
                try
                    let analysisRun = Dto.analyzeRun request.DatasetJson

                    use httpClient = new HttpClient()
                    httpClient.Timeout <- TimeSpan.FromSeconds 90.0

                    let connection =
                        OpenAiResponsesConnection(httpClient, options) :> IAiConnection

                    let analyst =
                        Ai.DjehutiAnalyst(connection, AiConnectionId "openai-responses") :> IDjehutiAnalyst

                    let model =
                        if String.IsNullOrWhiteSpace request.Model then
                            None
                        else
                            Some(ModelId request.Model)

                    let temperature =
                        if request.Temperature.HasValue then Some request.Temperature.Value else Some 0.1

                    let maxOutputTokens =
                        if request.MaxOutputTokens.HasValue then Some request.MaxOutputTokens.Value else Some 900

                    let result =
                        analyst.Ask
                            { Question = request.Question
                              Context = analysisRun.Context
                              ConversationId = None
                              Model = model
                              Temperature = temperature
                              MaxOutputTokens = maxOutputTokens }
                        |> Async.RunSynchronously

                    match result with
                    | Ok answer ->
                        { Answer = answer.Answer
                          Evidence = answer.Evidence |> List.map Dto.analystEvidenceDto
                          Model =
                            answer.AiResponse.Model
                            |> Option.map (fun (ModelId value) -> value)
                            |> Option.defaultValue options.Model
                          Metadata = answer.AiResponse.Metadata }
                        |> Results.Ok
                    | Error error ->
                        Results.Problem(detail = errorText error, statusCode = errorStatus error, title = "Analyst AI failed")
                with ex ->
                    Results.BadRequest(ex.Message)

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    // Djehuti.Api is currently a local workstation service for the dashboard.
    // Restrict origins and add authentication before deploying it remotely.
    builder.Services.AddCors(fun options ->
        options.AddDefaultPolicy(fun policy ->
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod()
            |> ignore))
    |> ignore

    builder.Services.ConfigureHttpJsonOptions(fun options ->
        options.SerializerOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        options.SerializerOptions.WriteIndented <- true)
    |> ignore

    builder.Services.AddHostedService<HeartbeatWorker.HeartbeatWorker>() |> ignore

    let app = builder.Build()

    Database.runMigrations ()
    AchievementRepository.seedDictionary ()

    let datasetDir =
        [ Path.Combine(AppContext.BaseDirectory, "data", "datasets")
          Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "datasets") ]
        |> List.tryFind Directory.Exists
        |> Option.defaultValue (Path.Combine(AppContext.BaseDirectory, "data", "datasets"))
    DataLibrary.migrateFromFilesystem datasetDir

    app.UseCors() |> ignore

    app.MapGet("/api/health", Func<string>(fun () -> "ok")) |> ignore

    app.MapGet(
        "/api/datasets",
        Func<IResult>(fun () ->
            DataLibrary.catalog ()
            |> Results.Ok)
    )
    |> ignore

    app.MapGet(
        "/api/datasets/{id}",
        Func<string, IResult>(fun id ->
            match DataLibrary.tryReadDataSet id with
            | Ok json -> Results.Text(json, "application/json")
            | Error error -> Results.NotFound(error))
    )
    |> ignore

    app.MapPost(
        "/api/datasets",
        Func<SaveDataSetRequest, IResult>(fun request ->
            if String.IsNullOrWhiteSpace request.Name then
                Results.BadRequest("name is required")
            elif String.IsNullOrWhiteSpace request.DatasetJson then
                Results.BadRequest("datasetJson is required")
            else
                try
                    let name = request.Name.Trim()
                    let description = if String.IsNullOrWhiteSpace request.Description then $"Live Lab run: {name}" else request.Description.Trim()
                    let sourceKind = if String.IsNullOrWhiteSpace request.SourceKind then "live-lab" else request.SourceKind.Trim()
                    match DataLibrary.saveDataSet name description sourceKind request.TurnCount request.DatasetJson with
                    | Ok item -> Results.Ok(item)
                    | Error message -> Results.Problem(detail = message, statusCode = 500, title = "Save failed")
                with ex ->
                    Results.Problem(detail = ex.Message, statusCode = 500, title = "Save failed"))
    )
    |> ignore

    app.MapPatch(
        "/api/datasets/{id}",
        Func<string, RenameDataSetRequest, IResult>(fun id request ->
            if String.IsNullOrWhiteSpace request.Name then
                Results.BadRequest("name is required")
            else
                try
                    match DataLibrary.renameDataSet id request.Name request.Description with
                    | Ok item -> Results.Ok(item)
                    | Error message -> Results.NotFound(message)
                with ex ->
                    Results.Problem(detail = ex.Message, statusCode = 500, title = "Rename failed"))
    )
    |> ignore

    app.MapDelete(
        "/api/datasets/{id}",
        Func<string, IResult>(fun id ->
            try
                match DataLibrary.deleteDataSet id with
                | Ok () -> Results.NoContent()
                | Error message -> Results.NotFound(message)
            with ex ->
                Results.Problem(detail = ex.Message, statusCode = 500, title = "Delete failed"))
    )
    |> ignore

    app.MapPost(
        "/api/analyze",
        Func<AnalyzeRequest, IResult>(fun request ->
            if String.IsNullOrWhiteSpace request.DatasetJson then
                Results.BadRequest("datasetJson is required")
            else
                try
                    Dto.analyze request.DatasetJson
                    |> Results.Ok
                with
                | :? ArgumentException as ex -> Results.BadRequest(ex.Message)
                | :? JsonException as ex -> Results.BadRequest(ex.Message)
                | ex -> Results.Problem(detail = ex.Message, statusCode = 500, title = "Analysis failed"))
    )
    |> ignore

    app.MapPost(
        "/api/analyst/ask",
        Func<AnalystRequest, IResult>(AnalystApi.ask)
    )
    |> ignore

    // ── Auth Endpoints ───────────────────────────────────────────────────────────
    app.MapPost(
        "/api/auth/register",
        Func<HttpContext, RegisterRequest, System.Threading.Tasks.Task<IResult>>(fun ctx request ->
            async {
                if String.IsNullOrWhiteSpace request.Email || String.IsNullOrWhiteSpace request.Password then
                    return Results.BadRequest("email and password are required")
                elif request.Password.Length < 8 then
                    return Results.BadRequest("password must be at least 8 characters")
                else
                    let rateLimitConfig = { RateLimiter.MaxAttempts = 5; RateLimiter.WindowSeconds = 3600 }
                    if not (RateLimiter.checkRateLimit request.Email rateLimitConfig) then
                        return Results.StatusCode(429)
                    else
                    let! hcaptchaValid = Auth.verifyHCaptcha request.HCaptchaToken
                    if not hcaptchaValid then
                        return Results.BadRequest("hCaptcha verification failed")
                    else
                        let! existingUser = UserRepository.tryGetByEmail request.Email
                        if existingUser.IsSome then
                            return Results.BadRequest("email already registered")
                        else
                            let passwordHash = Auth.hashPassword request.Password
                            let! newUser = UserRepository.createUser request.Email (Some passwordHash)
                            match newUser with
                            | Some user ->
                                // Record conversion for anonymous visitor tracking
                                let ip =
                                    match ctx.Request.Headers.TryGetValue("X-Real-IP") with
                                    | true, v when v.Count > 0 -> v.[0]
                                    | _ -> ctx.Connection.RemoteIpAddress |> Option.ofObj |> Option.map (fun a -> a.ToString()) |> Option.defaultValue "unknown"
                                let ipBytes = System.Text.Encoding.UTF8.GetBytes(ip)
                                let ipHash  = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ipBytes)).ToLowerInvariant()
                                MetricsRepository.recordConversion ipHash user.Id
                                let verifyToken = Auth.generateSecureToken ()
                                let! tokenStored = UserRepository.createEmailVerificationToken user.Id verifyToken
                                if tokenStored then
                                    let emailBody = Email.verificationEmailTemplate (user.Email) verifyToken
                                    let! _emailSent = Email.sendEmail {
                                        To = user.Email
                                        Subject = "Confirm your email address"
                                        HtmlBody = emailBody
                                    }
                                    return Results.Accepted()
                                else
                                    return Results.Problem(detail = "Failed to generate verification token", statusCode = 500, title = "Registration failed")
                            | None ->
                                return Results.Problem(detail = "Failed to create user", statusCode = 500, title = "Registration failed")
            } |> Async.StartAsTask)
    )
    |> ignore

    app.MapPost(
        "/api/auth/login",
        Func<LoginRequest, HttpContext, System.Threading.Tasks.Task<IResult>>(fun request ctx ->
            async {
                if String.IsNullOrWhiteSpace request.Email || String.IsNullOrWhiteSpace request.Password then
                    return Results.BadRequest("email and password are required")
                else
                    let rateLimitConfig = { RateLimiter.MaxAttempts = 10; RateLimiter.WindowSeconds = 900 }
                    if not (RateLimiter.checkRateLimit request.Email rateLimitConfig) then
                        return Results.StatusCode(429)
                    else
                    let! user = UserRepository.tryGetByEmail request.Email
                    match user with
                    | Some u when u.PasswordHash.IsSome && Auth.verifyPassword request.Password u.PasswordHash.Value ->
                        if u.Status <> "active" then
                            return Results.Problem(detail = $"Account is {u.Status}", statusCode = 403, title = "Login failed")
                        else
                            let token = Auth.generateToken {
                                UserId = u.Id.ToString()
                                Email = u.Email
                                DisplayName = u.DisplayName
                                Role = u.Role
                                IssuedAt = DateTime.UtcNow
                                ExpiresAt = DateTime.UtcNow.AddHours(24.0)
                            }
                            ctx.Response.Cookies.Append(
                                "djehuti_auth",
                                token,
                                Microsoft.AspNetCore.Http.CookieOptions(
                                    HttpOnly = true,
                                    Secure = true,
                                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax, Path = "/",
                                    Expires = DateTimeOffset.UtcNow.AddHours(24.0)
                                )
                            )
                            return Results.Ok({ Id = u.Id.ToString(); Email = u.Email; DisplayName = u.DisplayName; AvatarUrl = u.AvatarUrl; Bio = u.Bio; Pronouns = u.Pronouns; Location = u.Location; Role = u.Role; Status = u.Status; CreatedAt = u.CreatedAt })
                    | _ ->
                        return Results.BadRequest("invalid email or password")
            } |> Async.StartAsTask)
    )
    |> ignore

    app.MapGet(
        "/api/users/{id}",
        Func<string, System.Threading.Tasks.Task<IResult>>(fun id ->
            async {
                match Guid.TryParse(id) with
                | true, userId ->
                    let! profile = UserRepository.getPublicProfile userId
                    return match profile with
                           | Some p -> Results.Ok({ Id = p.Id.ToString(); DisplayName = p.DisplayName; AvatarUrl = p.AvatarUrl; Bio = p.Bio; Pronouns = p.Pronouns; Location = p.Location })
                           | None -> Results.NotFound()
                | _ ->
                    return Results.BadRequest("invalid user id")
            } |> Async.StartAsTask)
    )
    |> ignore

    app.MapPost(
        "/api/auth/verify-email",
        Func<VerifyEmailRequest, System.Threading.Tasks.Task<IResult>>(fun request ->
            async {
                if String.IsNullOrWhiteSpace request.Token then
                    return Results.BadRequest("token is required")
                else
                    let! userId = UserRepository.verifyEmailToken request.Token
                    match userId with
                    | Some id ->
                        let! verified = UserRepository.verifyEmail id
                        if verified then
                            let! _ = UserRepository.deleteEmailVerificationToken request.Token
                            return Results.Ok("Email verified successfully")
                        else
                            return Results.Problem(detail = "Failed to verify email", statusCode = 500, title = "Verification failed")
                    | None ->
                        return Results.BadRequest("invalid or expired token")
            } |> Async.StartAsTask)
    )
    |> ignore

    app.MapPost(
        "/api/auth/password-reset-request",
        Func<PasswordResetRequest, System.Threading.Tasks.Task<IResult>>(fun request ->
            async {
                if String.IsNullOrWhiteSpace request.Email then
                    return Results.BadRequest("email is required")
                else
                    let! user = UserRepository.tryGetByEmail request.Email
                    match user with
                    | Some u ->
                        let resetToken = Auth.generateSecureToken ()
                        let! tokenStored = UserRepository.createPasswordResetToken u.Id resetToken
                        if tokenStored then
                            let resetLink = $"https://djehuti.lagdaemon.com/auth/reset?token={resetToken}"
                            let emailBody = Email.passwordResetEmailTemplate u.Email resetLink
                            let! _emailSent = Email.sendEmail {
                                To = u.Email
                                Subject = "Reset your password"
                                HtmlBody = emailBody
                            }
                            return Results.Accepted()
                        else
                            return Results.Problem(detail = "Failed to generate reset token", statusCode = 500, title = "Reset request failed")
                    | None ->
                        return Results.Accepted()
            } |> Async.StartAsTask)
    )
    |> ignore

    app.MapPost(
        "/api/auth/password-reset-confirm",
        Func<PasswordResetConfirmRequest, System.Threading.Tasks.Task<IResult>>(fun request ->
            async {
                if String.IsNullOrWhiteSpace request.Token || String.IsNullOrWhiteSpace request.Password then
                    return Results.BadRequest("token and password are required")
                elif request.Password.Length < 8 then
                    return Results.BadRequest("password must be at least 8 characters")
                else
                    let! userId = UserRepository.verifyPasswordResetToken request.Token
                    match userId with
                    | Some id ->
                        let passwordHash = Auth.hashPassword request.Password
                        let! updated = UserRepository.updatePassword id passwordHash
                        if updated then
                            let! _ = UserRepository.deletePasswordResetToken request.Token
                            return Results.Ok("Password reset successfully")
                        else
                            return Results.Problem(detail = "Failed to reset password", statusCode = 500, title = "Reset failed")
                    | None ->
                        return Results.BadRequest("invalid or expired token")
            } |> Async.StartAsTask)
    )
    |> ignore

    app.MapGet(
        "/api/auth/me",
        Func<HttpContext, System.Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                match ctx.Request.Cookies.TryGetValue("djehuti_auth") with
                | true, token ->
                    match Auth.verifyToken token with
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | true, userId ->
                            let! user = UserRepository.tryGetById userId
                            return match user with
                                   | Some u -> Results.Ok({ Id = u.Id.ToString(); Email = u.Email; DisplayName = u.DisplayName; AvatarUrl = u.AvatarUrl; Bio = u.Bio; Pronouns = u.Pronouns; Location = u.Location; Role = u.Role; Status = u.Status; CreatedAt = u.CreatedAt })
                                   | None -> Results.NotFound()
                        | _ -> return Results.Unauthorized()
                    | None -> return Results.Unauthorized()
                | _ -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    )
    |> ignore

    app.MapPut(
        "/api/auth/profile",
        Func<HttpContext, UpdateProfileRequest, System.Threading.Tasks.Task<IResult>>(fun ctx request ->
            async {
                match ctx.Request.Cookies.TryGetValue("djehuti_auth") with
                | true, token ->
                    match Auth.verifyToken token with
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | true, userId ->
                            let! updated = UserRepository.updateProfile userId request.DisplayName request.Bio request.Pronouns request.Location request.NotifyByEmail
                            return match updated with
                                   | Some u -> Results.Ok({ Id = u.Id.ToString(); Email = u.Email; DisplayName = u.DisplayName; AvatarUrl = u.AvatarUrl; Bio = u.Bio; Pronouns = u.Pronouns; Location = u.Location; Role = u.Role; Status = u.Status; CreatedAt = u.CreatedAt })
                                   | None -> Results.Problem(detail = "Failed to update profile", statusCode = 500, title = "Update failed")
                        | _ -> return Results.Unauthorized()
                    | None -> return Results.Unauthorized()
                | _ -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    )
    |> ignore

    app.MapPost(
        "/api/auth/logout",
        Func<HttpContext, IResult>(fun ctx ->
            ctx.Response.Cookies.Delete("djehuti_auth")
            Results.Ok("Logged out successfully"))
    )
    |> ignore

    // ── OAuth Endpoints ──────────────────────────────────────────────────────────
    app.MapGet(
        "/api/auth/oauth/google/callback",
        Func<string, string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun code state ctx ->
            async {
                if String.IsNullOrWhiteSpace code then
                    return Results.BadRequest("code is required")
                else
                    let googleClientId = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID")
                    let googleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_SECRET")

                    if String.IsNullOrWhiteSpace googleClientId || String.IsNullOrWhiteSpace googleClientSecret then
                        return Results.Problem(detail = "Google OAuth not configured", statusCode = 500, title = "OAuth configuration error")
                    else
                        let! tokenResponse = OAuth.exchangeGoogleCode code googleClientId googleClientSecret
                        match tokenResponse with
                        | Some tokenData ->
                            let! userInfo = OAuth.getGoogleUserInfo tokenData.access_token
                            match userInfo with
                            | Some info ->
                                let! existingUser = UserRepository.tryGetUserByIdentity "google" info.id
                                match existingUser with
                                | Some user ->
                                    if user.Status = "suspended" then
                                        return Results.Redirect("/?error=suspended")
                                    else
                                    let token = Auth.generateToken {
                                        UserId = user.Id.ToString()
                                        Email = user.Email
                                        DisplayName = user.DisplayName
                                        Role = user.Role
                                        IssuedAt = DateTime.UtcNow
                                        ExpiresAt = DateTime.UtcNow.AddHours(24.0)
                                    }
                                    ctx.Response.Cookies.Append(
                                        "djehuti_auth",
                                        token,
                                        Microsoft.AspNetCore.Http.CookieOptions(
                                            HttpOnly = true,
                                            Secure = true,
                                            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax, Path = "/",
                                            Expires = DateTimeOffset.UtcNow.AddHours(24.0)
                                        )
                                    )
                                    return Results.Redirect("/")
                                | None ->
                                    // Link to existing account by email, or create new
                                    let! existingByEmail = UserRepository.tryGetByEmail info.email
                                    let! user =
                                        match existingByEmail with
                                        | Some u -> async { return Some u }
                                        | None -> UserRepository.createUser info.email None
                                    match user with
                                    | Some u ->
                                        if u.Status = "suspended" then
                                            return Results.Redirect("/?error=suspended")
                                        else
                                        let! _identity = UserRepository.createUserIdentity u.Id "google" info.id (Some info.email) info.name info.picture
                                        let token = Auth.generateToken {
                                            UserId = u.Id.ToString()
                                            Email = u.Email
                                            DisplayName = u.DisplayName
                                            Role = u.Role
                                            IssuedAt = DateTime.UtcNow
                                            ExpiresAt = DateTime.UtcNow.AddHours(24.0)
                                        }
                                        ctx.Response.Cookies.Append(
                                            "djehuti_auth",
                                            token,
                                            Microsoft.AspNetCore.Http.CookieOptions(
                                                HttpOnly = true,
                                                Secure = true,
                                                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax, Path = "/",
                                                Expires = DateTimeOffset.UtcNow.AddHours(24.0)
                                            )
                                        )
                                        return Results.Redirect("/")
                                    | None ->
                                        return Results.Problem(detail = "Failed to create user", statusCode = 500, title = "OAuth login failed")
                            | None ->
                                return Results.BadRequest("Failed to get user info from Google")
                        | None ->
                            return Results.BadRequest("Failed to exchange authorization code")
            } |> Async.StartAsTask)
    )
    |> ignore

    app.MapGet(
        "/api/auth/oauth/github/callback",
        Func<string, string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun code state ctx ->
            async {
                if String.IsNullOrWhiteSpace code then
                    return Results.BadRequest("code is required")
                else
                    let githubClientId = Environment.GetEnvironmentVariable("GH_OAUTH_CLIENT_ID")
                    let githubClientSecret = Environment.GetEnvironmentVariable("GH_OAUTH_CLIENT_SECRET")

                    if String.IsNullOrWhiteSpace githubClientId || String.IsNullOrWhiteSpace githubClientSecret then
                        return Results.Problem(detail = "GitHub OAuth not configured", statusCode = 500, title = "OAuth configuration error")
                    else
                        let! tokenResponse = OAuth.exchangeGitHubCode code githubClientId githubClientSecret
                        match tokenResponse with
                        | Some tokenData ->
                            let! userInfo = OAuth.getGitHubUserInfo tokenData.access_token
                            match userInfo with
                            | Some info ->
                                let! existingUser = UserRepository.tryGetUserByIdentity "github" (info.id.ToString())
                                match existingUser with
                                | Some user ->
                                    let token = Auth.generateToken {
                                        UserId = user.Id.ToString()
                                        Email = user.Email
                                        DisplayName = user.DisplayName
                                        Role = user.Role
                                        IssuedAt = DateTime.UtcNow
                                        ExpiresAt = DateTime.UtcNow.AddHours(24.0)
                                    }
                                    ctx.Response.Cookies.Append(
                                        "djehuti_auth",
                                        token,
                                        Microsoft.AspNetCore.Http.CookieOptions(
                                            HttpOnly = true,
                                            Secure = true,
                                            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax, Path = "/",
                                            Expires = DateTimeOffset.UtcNow.AddHours(24.0)
                                        )
                                    )
                                    return Results.Redirect("/")
                                | None ->
                                    let email = info.email |> Option.defaultValue $"{info.login}@github.local"
                                    let! newUser = UserRepository.createUser email None
                                    match newUser with
                                    | Some user ->
                                        let! _identity = UserRepository.createUserIdentity user.Id "github" (info.id.ToString()) info.email info.name info.avatar_url
                                        let token = Auth.generateToken {
                                            UserId = user.Id.ToString()
                                            Email = user.Email
                                            DisplayName = user.DisplayName
                                            Role = user.Role
                                            IssuedAt = DateTime.UtcNow
                                            ExpiresAt = DateTime.UtcNow.AddHours(24.0)
                                        }
                                        ctx.Response.Cookies.Append(
                                            "djehuti_auth",
                                            token,
                                            Microsoft.AspNetCore.Http.CookieOptions(
                                                HttpOnly = true,
                                                Secure = true,
                                                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax, Path = "/",
                                                Expires = DateTimeOffset.UtcNow.AddHours(24.0)
                                            )
                                        )
                                        return Results.Redirect("/")
                                    | None ->
                                        return Results.Problem(detail = "Failed to create user", statusCode = 500, title = "OAuth login failed")
                            | None ->
                                return Results.BadRequest("Failed to get user info from GitHub")
                        | None ->
                            return Results.BadRequest("Failed to exchange authorization code")
            } |> Async.StartAsTask)
    )
    |> ignore

    // ── Auth helper (used by media, forum, and all subsequent endpoints) ─────

    let tryGetAuthClaims (ctx: HttpContext) =
        match ctx.Request.Cookies.TryGetValue("djehuti_auth") with
        | true, token -> Auth.verifyToken token
        | _ -> None

    // ── Media ─────────────────────────────────────────────────────────────────
    // Returns a presigned S3 PUT URL so the client can upload directly to S3.
    // After upload the client calls /api/media/confirm to record the DB entry.

    app.MapPost(
        "/api/media/upload-url",
        Func<HttpContext, {| filename: string; contentType: string; ``module``: string; contextId: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    if not (MediaService.isAllowedContentType body.contentType) then
                        return Results.BadRequest("Content type not allowed")
                    else
                        let ext = System.IO.Path.GetExtension(body.filename)
                        let s3Key = $"{body.``module``}/{Guid.NewGuid()}{ext}"
                        let presignedUrl = MediaService.generatePresignedUploadUrl s3Key body.contentType 15
                        return Results.Ok({| presignedUrl = presignedUrl; s3Key = s3Key |})
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/media/confirm",
        Func<HttpContext, {| s3Key: string; filename: string; contentType: string; ``module``: string; contextId: string; sizeBytes: int64 |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, userId ->
                        use conn = Database.openConnection()
                        let ctxId = match Guid.TryParse(body.contextId) with | true, g -> Some g | _ -> None
                        let size = if body.sizeBytes > 0L then Some body.sizeBytes else None
                        let media = MediaService.recordMedia conn userId body.``module`` ctxId body.s3Key body.filename body.contentType size
                        return match media with
                               | Some m -> Results.Ok(m)
                               | None   -> Results.Problem(detail = "Failed to record media", statusCode = 500, title = "Error")
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/media/{mediaId}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun mediaId ctx ->
            async {
                match Guid.TryParse(mediaId) with
                | false, _ -> return Results.BadRequest("Invalid media id")
                | true, mid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            use conn = Database.openConnection()
                            let deleted = MediaService.deleteMedia conn mid userId (Permissions.isAdmin claims.Role)
                            return if deleted then Results.Ok() else Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Forum: Categories (read=anonymous, write=admin) ───────────────────────

    app.MapGet(
        "/api/forum/categories",
        Func<IResult>(fun () ->
            let cats = ForumRepository.getCategories ()
            Results.Ok(cats))
    ) |> ignore

    app.MapPost(
        "/api/forum/categories",
        Func<HttpContext, {| name: string; description: string; parentCategoryId: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    let desc = if String.IsNullOrWhiteSpace body.description then None else Some body.description
                    let parentId =
                        if String.IsNullOrWhiteSpace body.parentCategoryId then None
                        else match System.Guid.TryParse(body.parentCategoryId) with
                             | true, g -> Some g
                             | _ -> None
                    return match ForumRepository.createCategory body.name desc parentId with
                           | Some cat -> Results.Created($"/api/forum/categories/{cat.Id}", cat)
                           | None     -> Results.Problem(detail = "Failed to create category", statusCode = 500, title = "Error")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Forum: Forums (read=anonymous, write=admin) ───────────────────────────

    app.MapGet(
        "/api/forum/categories/{categoryId}/forums",
        Func<string, IResult>(fun categoryId ->
            match Guid.TryParse(categoryId) with
            | true, cid -> Results.Ok(ForumRepository.getForumsByCategory cid)
            | _ -> Results.BadRequest("Invalid category id"))
    ) |> ignore

    app.MapPost(
        "/api/forum/categories/{categoryId}/forums",
        Func<string, HttpContext, {| name: string; description: string |}, System.Threading.Tasks.Task<IResult>>(fun categoryId ctx body ->
            async {
                match Guid.TryParse(categoryId) with
                | false, _ -> return Results.BadRequest("Invalid category id")
                | true, cid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        let desc = if String.IsNullOrWhiteSpace body.description then None else Some body.description
                        return match ForumRepository.createForum cid body.name desc with
                               | Some f -> Results.Created($"/api/forum/forums/{f.Id}", f)
                               | None   -> Results.Problem(detail = "Failed to create forum", statusCode = 500, title = "Error")
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapGet(
        "/api/forum/forums/{forumId}",
        Func<string, IResult>(fun forumId ->
            match Guid.TryParse(forumId) with
            | true, fid ->
                match ForumRepository.getForumById fid with
                | Some f -> Results.Ok(f)
                | None   -> Results.NotFound()
            | _ -> Results.BadRequest("Invalid forum id"))
    ) |> ignore

    // ── Forum: Threads (read=anonymous, create=authenticated) ─────────────────

    app.MapGet(
        "/api/forum/forums/{forumId}/threads",
        Func<string, int, int, IResult>(fun forumId page pageSize ->
            match Guid.TryParse(forumId) with
            | true, fid ->
                let p  = if page < 1 then 1 else page
                let ps = if pageSize < 1 || pageSize > 100 then 25 else pageSize
                Results.Ok(ForumRepository.getThreadsByForum fid p ps)
            | _ -> Results.BadRequest("Invalid forum id"))
    ) |> ignore

    app.MapGet(
        "/api/forum/threads/{threadId}",
        Func<string, IResult>(fun threadId ->
            match Guid.TryParse(threadId) with
            | true, tid ->
                ForumRepository.incrementViewCount tid
                match ForumRepository.getThreadById tid with
                | Some t -> Results.Ok(t)
                | None   -> Results.NotFound()
            | _ -> Results.BadRequest("Invalid thread id"))
    ) |> ignore

    app.MapPost(
        "/api/forum/forums/{forumId}/threads",
        Func<string, HttpContext, {| title: string; content: string |}, System.Threading.Tasks.Task<IResult>>(fun forumId ctx body ->
            async {
                match Guid.TryParse(forumId) with
                | false, _ -> return Results.BadRequest("Invalid forum id")
                | true, fid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            match UserRepository.getUserStatus userId with
                            | Some "restricted" -> return Results.Problem(detail = "Your account is restricted from posting", statusCode = 403, title = "Restricted")
                            | _ ->
                            return match ForumRepository.createThread fid userId body.title body.content with
                                   | Some t -> Results.Created($"/api/forum/threads/{t.Id}", t)
                                   | None   -> Results.Problem(detail = "Failed to create thread", statusCode = 500, title = "Error")
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/forum/threads/{threadId}/pin",
        Func<string, HttpContext, {| pinned: bool |}, System.Threading.Tasks.Task<IResult>>(fun threadId ctx body ->
            async {
                match Guid.TryParse(threadId) with
                | false, _ -> return Results.BadRequest("Invalid thread id")
                | true, tid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match ForumRepository.getThreadById tid with
                        | None -> return Results.NotFound()
                        | Some thread ->
                            let isMod = Permissions.hasContextRole (Database.openConnection()) (Guid.Parse(claims.UserId)) Permissions.ModuleForum Permissions.RoleModerator (Some thread.ForumId)
                            if Permissions.isAdmin claims.Role || isMod then
                                ForumRepository.setThreadPinned tid body.pinned
                                return Results.Ok()
                            else return Results.Forbid()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/forum/threads/{threadId}/lock",
        Func<string, HttpContext, {| locked: bool |}, System.Threading.Tasks.Task<IResult>>(fun threadId ctx body ->
            async {
                match Guid.TryParse(threadId) with
                | false, _ -> return Results.BadRequest("Invalid thread id")
                | true, tid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match ForumRepository.getThreadById tid with
                        | None -> return Results.NotFound()
                        | Some thread ->
                            let isMod = Permissions.hasContextRole (Database.openConnection()) (Guid.Parse(claims.UserId)) Permissions.ModuleForum Permissions.RoleModerator (Some thread.ForumId)
                            if Permissions.isAdmin claims.Role || isMod then
                                ForumRepository.setThreadLocked tid body.locked
                                return Results.Ok()
                            else return Results.Forbid()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/forum/threads/{threadId}/move",
        Func<string, HttpContext, {| forumId: string |}, System.Threading.Tasks.Task<IResult>>(fun threadId ctx body ->
            async {
                match Guid.TryParse(threadId), Guid.TryParse(body.forumId) with
                | (false, _), _ | _, (false, _) -> return Results.BadRequest("Invalid id")
                | (true, tid), (true, targetForumId) ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match ForumRepository.getThreadById tid with
                        | None -> return Results.NotFound()
                        | Some thread ->
                            let isMod = Permissions.hasContextRole (Database.openConnection()) (Guid.Parse(claims.UserId)) Permissions.ModuleForum Permissions.RoleModerator (Some thread.ForumId)
                            if Permissions.isAdmin claims.Role || isMod then
                                let ok = ForumRepository.moveThread tid targetForumId
                                return if ok then Results.Ok() else Results.NotFound()
                            else return Results.Forbid()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/forum/threads/{threadId}/split",
        Func<string, HttpContext, {| postIds: string[]; title: string |}, System.Threading.Tasks.Task<IResult>>(fun threadId ctx body ->
            async {
                match Guid.TryParse(threadId) with
                | false, _ -> return Results.BadRequest("Invalid thread id")
                | true, tid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match ForumRepository.getThreadById tid with
                        | None -> return Results.NotFound()
                        | Some thread ->
                            let isMod = Permissions.hasContextRole (Database.openConnection()) (Guid.Parse(claims.UserId)) Permissions.ModuleForum Permissions.RoleModerator (Some thread.ForumId)
                            if Permissions.isAdmin claims.Role || isMod then
                                match Guid.TryParse(claims.UserId) with
                                | false, _ -> return Results.Unauthorized()
                                | true, actorId ->
                                    let postGuids = body.postIds |> Array.choose (fun s -> match Guid.TryParse(s) with true, g -> Some g | _ -> None) |> Array.toList
                                    return match ForumRepository.splitThread tid postGuids body.title actorId with
                                           | Some newThread -> Results.Ok(newThread)
                                           | None -> Results.BadRequest("Split failed")
                            else return Results.Forbid()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/forum/threads/{threadId}/merge",
        Func<string, HttpContext, {| intoThreadId: string |}, System.Threading.Tasks.Task<IResult>>(fun threadId ctx body ->
            async {
                match Guid.TryParse(threadId), Guid.TryParse(body.intoThreadId) with
                | (false, _), _ | _, (false, _) -> return Results.BadRequest("Invalid id")
                | (true, tid), (true, targetId) ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match ForumRepository.getThreadById tid with
                        | None -> return Results.NotFound()
                        | Some thread ->
                            let isMod = Permissions.hasContextRole (Database.openConnection()) (Guid.Parse(claims.UserId)) Permissions.ModuleForum Permissions.RoleModerator (Some thread.ForumId)
                            if Permissions.isAdmin claims.Role || isMod then
                                let ok = ForumRepository.mergeThreads tid targetId
                                return if ok then Results.Ok() else Results.NotFound()
                            else return Results.Forbid()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Forum: Posts (read=anonymous, create=authenticated) ───────────────────

    app.MapGet(
        "/api/forum/threads/{threadId}/posts",
        Func<string, int, int, IResult>(fun threadId page pageSize ->
            match Guid.TryParse(threadId) with
            | true, tid ->
                let p  = if page < 1 then 1 else page
                let ps = if pageSize < 1 || pageSize > 100 then 25 else pageSize
                Results.Ok(ForumRepository.getPostsByThread tid p ps)
            | _ -> Results.BadRequest("Invalid thread id"))
    ) |> ignore

    app.MapPost(
        "/api/forum/threads/{threadId}/posts",
        Func<string, HttpContext, {| content: string |}, System.Threading.Tasks.Task<IResult>>(fun threadId ctx body ->
            async {
                match Guid.TryParse(threadId) with
                | false, _ -> return Results.BadRequest("Invalid thread id")
                | true, tid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match ForumRepository.getThreadById tid with
                        | None -> return Results.NotFound()
                        | Some thread when thread.IsLocked ->
                            return Results.Problem(detail = "Thread is locked", statusCode = 403, title = "Locked")
                        | Some thread ->
                            match Guid.TryParse(claims.UserId) with
                            | false, _ -> return Results.Unauthorized()
                            | true, userId ->
                                match UserRepository.getUserStatus userId with
                                | Some "restricted" -> return Results.Problem(detail = "Your account is restricted from posting", statusCode = 403, title = "Restricted")
                                | _ ->
                                return match ForumRepository.createPost tid userId body.content with
                                       | Some p ->
                                           let link = $"/community/threads/{tid}"
                                           let preview = body.content.Replace("<", "").Replace(">", "")
                                           let preview = if preview.Length > 80 then preview.[..79] + "…" else preview
                                           let authorName =
                                               match UserRepository.getUserById userId with
                                               | Some u -> u.DisplayName |> Option.defaultValue u.Email
                                               | None   -> "Someone"
                                           NotificationRepository.notifySubscribers tid userId authorName thread.Title link preview
                                           // Parse @mentions and notify each mentioned user
                                           let mentionRegex = System.Text.RegularExpressions.Regex(@"data-mention=""([^""]+)""")
                                           let mentionedNames =
                                               mentionRegex.Matches(body.content)
                                               |> Seq.cast<System.Text.RegularExpressions.Match>
                                               |> Seq.map (fun m -> m.Groups.[1].Value)
                                               |> Seq.distinct
                                               |> Seq.toList
                                           let mentionedIds = UserRepository.getUserIdsByDisplayNames mentionedNames
                                           for mentionedUserId in mentionedIds do
                                               NotificationRepository.notifyMention mentionedUserId userId authorName thread.Title link preview
                                           Results.Created($"/api/forum/posts/{p.Id}", p)
                                       | None   -> Results.Problem(detail = "Failed to create post", statusCode = 500, title = "Error")
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPut(
        "/api/forum/posts/{postId}",
        Func<string, HttpContext, {| content: string |}, System.Threading.Tasks.Task<IResult>>(fun postId ctx body ->
            async {
                match Guid.TryParse(postId) with
                | false, _ -> return Results.BadRequest("Invalid post id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            return match ForumRepository.updatePost pid userId body.content with
                                   | Some p -> Results.Ok(p)
                                   | None   -> Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/forum/posts/{postId}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun postId ctx ->
            async {
                match Guid.TryParse(postId) with
                | false, _ -> return Results.BadRequest("Invalid post id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        let deleted = ForumRepository.deletePost pid
                        return if deleted then Results.Ok() else Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/forum/posts/{postId}/vote",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun postId ctx ->
            async {
                match Guid.TryParse(postId) with
                | false, _ -> return Results.BadRequest("Invalid post id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            let voted = ForumRepository.votePost pid userId
                            return Results.Ok({| voted = voted |})
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/forum/posts/{postId}/answer",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun postId ctx ->
            async {
                match Guid.TryParse(postId) with
                | false, _ -> return Results.BadRequest("Invalid post id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, _ ->
                            // find the thread to verify ownership
                            let posts = ForumRepository.getPostsByThread pid 1 1
                            match posts with
                            | [] -> return Results.NotFound()
                            | post :: _ ->
                                match ForumRepository.getThreadById post.ThreadId with
                                | None -> return Results.NotFound()
                                | Some thread when thread.AuthorId.ToString() = claims.UserId || Permissions.isAdmin claims.Role ->
                                    ForumRepository.markAsAnswer pid thread.Id
                                    return Results.Ok()
                                | _ -> return Results.Forbid()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Forum: Reactions ─────────────────────────────────────────────────────

    app.MapGet(
        "/api/forum/posts/{id}/reactions",
        Func<HttpContext, Guid, IResult>(fun ctx postId ->
            let userId =
                match tryGetAuthClaims ctx with
                | Some claims -> Some (System.Guid.Parse(claims.UserId))
                | None -> None
            Results.Ok(ForumRepository.getReactions postId userId))
    ) |> ignore

    app.MapPost(
        "/api/forum/posts/{id}/reactions",
        Func<HttpContext, Guid, {| emoji: string |}, IResult>(fun ctx postId body ->
            match tryGetAuthClaims ctx with
            | Some claims ->
                let userId  = System.Guid.Parse(claims.UserId)
                let added   = ForumRepository.toggleReaction postId userId body.emoji
                Results.Ok({| added = added; emoji = body.emoji |})
            | None -> Results.Unauthorized())
    ) |> ignore

    // ── Forum: Reports ───────────────────────────────────────────────────────

    app.MapPost(
        "/api/forum/reports",
        Func<HttpContext, {| targetType: string; targetId: Guid; reason: string |}, IResult>(fun ctx body ->
            match tryGetAuthClaims ctx with
            | Some claims ->
                let reporterId = Guid.Parse(claims.UserId)
                match ForumRepository.createReport reporterId body.targetType body.targetId body.reason with
                | Some r -> Results.Created($"/api/forum/reports/{r.Id}", r)
                | None   -> Results.Problem(detail = "Failed to create report", statusCode = 500, title = "Error")
            | None -> Results.Unauthorized())
    ) |> ignore

    app.MapGet(
        "/api/admin/forum/reports",
        Func<HttpContext, string, int, int, IResult>(fun ctx status page pageSize ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role || Permissions.hasContextRole (Database.openConnection()) (Guid.Parse(claims.UserId)) Permissions.ModuleForum Permissions.RoleModerator None ->
                let p  = if page < 1 then 1 else page
                let ps = if pageSize < 1 || pageSize > 100 then 25 else pageSize
                let s  = if String.IsNullOrWhiteSpace(status) then None else Some status
                Results.Ok(ForumRepository.getReports s p ps)
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapPatch(
        "/api/admin/forum/reports/{id}",
        Func<Guid, HttpContext, {| status: string |}, IResult>(fun id ctx body ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role || Permissions.hasContextRole (Database.openConnection()) (Guid.Parse(claims.UserId)) Permissions.ModuleForum Permissions.RoleModerator None ->
                let resolverId = Guid.Parse(claims.UserId)
                let valid = [ "dismissed"; "warned"; "deleted" ]
                if not (List.contains body.status valid) then
                    Results.BadRequest("status must be one of: dismissed, warned, deleted")
                else
                    let ok = ForumRepository.resolveReport id resolverId body.status
                    if ok then Results.Ok() else Results.NotFound()
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    // ── Subscriptions ─────────────────────────────────────────────────────────

    app.MapPost(
        "/api/forum/threads/{threadId}/subscribe",
        Func<Guid, HttpContext, {| level: string |}, IResult>(fun threadId ctx body ->
            match tryGetAuthClaims ctx with
            | Some claims ->
                let userId = Guid.Parse(claims.UserId)
                let level  = if [ "watching"; "tracking"; "muted" ] |> List.contains body.level then body.level else "tracking"
                match NotificationRepository.upsertSubscription userId "thread" threadId level with
                | Some sub -> Results.Ok(sub)
                | None     -> Results.Problem(detail = "Failed to subscribe", statusCode = 500, title = "Error")
            | None -> Results.Unauthorized())
    ) |> ignore

    app.MapGet(
        "/api/forum/threads/{threadId}/subscribe",
        Func<Guid, HttpContext, IResult>(fun threadId ctx ->
            match tryGetAuthClaims ctx with
            | Some claims ->
                let userId = Guid.Parse(claims.UserId)
                match NotificationRepository.getSubscription userId "thread" threadId with
                | Some sub -> Results.Ok(sub)
                | None     -> Results.NotFound()
            | None -> Results.Unauthorized())
    ) |> ignore

    app.MapPost(
        "/api/forum/categories/{categoryId}/subscribe",
        Func<Guid, HttpContext, {| level: string |}, IResult>(fun categoryId ctx body ->
            match tryGetAuthClaims ctx with
            | Some claims ->
                let userId = Guid.Parse(claims.UserId)
                let level  = if [ "watching"; "tracking"; "muted" ] |> List.contains body.level then body.level else "tracking"
                match NotificationRepository.upsertSubscription userId "category" categoryId level with
                | Some sub -> Results.Ok(sub)
                | None     -> Results.Problem(detail = "Failed to subscribe", statusCode = 500, title = "Error")
            | None -> Results.Unauthorized())
    ) |> ignore

    // ── Notifications ─────────────────────────────────────────────────────────

    app.MapGet(
        "/api/notifications",
        Func<HttpContext, int, int, IResult>(fun ctx page pageSize ->
            match tryGetAuthClaims ctx with
            | Some claims ->
                let userId = Guid.Parse(claims.UserId)
                let p      = if page < 1 then 1 else page
                let ps     = if pageSize < 1 || pageSize > 50 then 20 else pageSize
                let items  = NotificationRepository.getNotifications userId p ps
                let unread = NotificationRepository.getUnreadCount userId
                Results.Ok({| items = items; unreadCount = unread |})
            | None -> Results.Unauthorized())
    ) |> ignore

    app.MapPatch(
        "/api/notifications/{id}/read",
        Func<Guid, HttpContext, IResult>(fun id ctx ->
            match tryGetAuthClaims ctx with
            | Some claims ->
                let userId = Guid.Parse(claims.UserId)
                let ok = NotificationRepository.markRead id userId
                if ok then Results.Ok() else Results.NotFound()
            | None -> Results.Unauthorized())
    ) |> ignore

    app.MapPatch(
        "/api/notifications/read-all",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some claims ->
                NotificationRepository.markAllRead (Guid.Parse(claims.UserId))
                Results.Ok()
            | None -> Results.Unauthorized())
    ) |> ignore

    // ── User Profile (self) ───────────────────────────────────────────────────

    app.MapGet(
        "/api/users/me/profile",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | None -> Results.Unauthorized()
            | Some claims ->
                match Guid.TryParse(claims.UserId) with
                | false, _ -> Results.Unauthorized()
                | true, uid ->
                match UserRepository.getUserById uid with
                | None -> Results.NotFound()
                | Some u ->
                    Results.Ok({|
                        displayName = u.DisplayName
                        avatarUrl   = u.AvatarUrl
                        bio         = u.Bio
                        pronouns    = u.Pronouns
                        location    = u.Location
                    |}))
    ) |> ignore

    app.MapPatch(
        "/api/users/me/profile",
        Func<HttpContext, {| displayName: string; bio: string; avatarUrl: string; pronouns: string; location: string |}, IResult>(fun ctx body ->
            match tryGetAuthClaims ctx with
            | None -> Results.Unauthorized()
            | Some claims ->
                match Guid.TryParse(claims.UserId) with
                | false, _ -> Results.Unauthorized()
                | true, uid ->
                let dn  = if String.IsNullOrWhiteSpace body.displayName then None else Some (body.displayName.Trim())
                let bio = if String.IsNullOrWhiteSpace body.bio         then None else Some (body.bio.Trim())
                let av  = if String.IsNullOrWhiteSpace body.avatarUrl   then None else Some (body.avatarUrl.Trim())
                let pr  = if String.IsNullOrWhiteSpace body.pronouns    then None else Some (body.pronouns.Trim())
                let loc = if String.IsNullOrWhiteSpace body.location    then None else Some (body.location.Trim())
                match UserRepository.updateProfileFull uid dn bio av pr loc with
                | Some u -> Results.Ok({| displayName = u.DisplayName; avatarUrl = u.AvatarUrl; bio = u.Bio; pronouns = u.Pronouns; location = u.Location |})
                | None   -> Results.Problem(detail = "Update failed", statusCode = 500, title = "Error"))
    ) |> ignore

    // ── User Preferences ─────────────────────────────────────────────────────

    app.MapGet(
        "/api/users/me/preferences",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | None -> Results.Unauthorized()
            | Some claims ->
                match Guid.TryParse(claims.UserId) with
                | false, _ -> Results.Unauthorized()
                | true, uid ->
                let prefs = PreferencesRepository.getPreferences uid
                Results.Ok(prefs |> Map.toSeq |> dict))
    ) |> ignore

    app.MapPatch(
        "/api/users/me/preferences",
        Func<HttpContext, System.Text.Json.JsonElement, IResult>(fun ctx body ->
            match tryGetAuthClaims ctx with
            | None -> Results.Unauthorized()
            | Some claims ->
                let patch =
                    body.EnumerateObject()
                    |> Seq.map (fun p ->
                        let v : obj =
                            match p.Value.ValueKind with
                            | System.Text.Json.JsonValueKind.True  -> box true
                            | System.Text.Json.JsonValueKind.False -> box false
                            | System.Text.Json.JsonValueKind.Number -> box (p.Value.GetDouble())
                            | _ -> box (p.Value.GetString())
                        p.Name, v)
                    |> Map.ofSeq
                match Guid.TryParse(claims.UserId) with
                | false, _ -> Results.Unauthorized()
                | true, uid ->
                let updated = PreferencesRepository.patchPreferences uid patch
                Results.Ok(updated |> Map.toSeq |> dict))
    ) |> ignore

    // ── Achievements ─────────────────────────────────────────────────────────

    // GET /api/achievements — full dictionary (admin sees hidden)
    app.MapGet(
        "/api/achievements",
        Func<HttpContext, IResult>(fun ctx ->
            let isAdmin = tryGetAuthClaims ctx |> Option.map (fun c -> c.Role = "admin") |> Option.defaultValue false
            Results.Ok(AchievementRepository.getAllAchievements isAdmin))
    ) |> ignore

    // GET /api/users/me/achievements
    app.MapGet(
        "/api/users/me/achievements",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | None -> Results.Unauthorized()
            | Some claims ->
                match Guid.TryParse(claims.UserId) with
                | false, _ -> Results.Unauthorized()
                | true, uid -> Results.Ok(AchievementRepository.getUserAchievements uid))
    ) |> ignore

    // GET /api/users/{userId}/achievements
    app.MapGet(
        "/api/users/{userId}/achievements",
        Func<Guid, IResult>(fun userId ->
            Results.Ok(AchievementRepository.getUserAchievements userId))
    ) |> ignore

    // GET /api/users/me/metrics
    app.MapGet(
        "/api/users/me/metrics",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | None -> Results.Unauthorized()
            | Some claims ->
                match Guid.TryParse(claims.UserId) with
                | false, _ -> Results.Unauthorized()
                | true, uid -> Results.Ok(AchievementRepository.getMetrics uid))
    ) |> ignore

    // POST /api/admin/achievements/recompute — force full batch recompute
    app.MapPost(
        "/api/admin/achievements/recompute",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some c when c.Role = "admin" ->
                AchievementEngine.runBatch()
                AchievementEngine.dispatchNotifications()
                Results.Ok({| message = "Recompute complete" |})
            | _ -> Results.Forbid())
    ) |> ignore

    // ── Public profiles ──────────────────────────────────────────────────────

    app.MapGet(
        "/api/users/{userId}/public",
        Func<Guid, IResult>(fun userId ->
            match UserRepository.getUserById userId with
            | None -> Results.NotFound()
            | Some u ->
                let dn = match u.DisplayName with | Some d -> d | None -> u.Email
                Results.Ok({| id = u.Id.ToString(); displayName = dn; email = u.Email; bio = u.Bio; avatarUrl = u.AvatarUrl; pronouns = u.Pronouns; location = u.Location; createdAt = u.CreatedAt |})
        )
    ) |> ignore

    app.MapGet(
        "/api/users/{userId}/activity",
        Func<Guid, System.Threading.Tasks.Task<IResult>>(fun userId ->
            task {
                use conn = Database.openConnection()
                let items = ResizeArray<{| type_: string; id: string; title: string; createdAt: System.DateTime |}>()

                // Recent forum posts
                use cmd1 = new Npgsql.NpgsqlCommand("""
                    SELECT id, content, created_at FROM forum_posts
                    WHERE author_id = @uid AND NOT deleted
                    ORDER BY created_at DESC LIMIT 15
                """, conn)
                cmd1.Parameters.AddWithValue("uid", userId) |> ignore
                use r1 = cmd1.ExecuteReader()
                while r1.Read() do
                    let preview = (r1.GetString(1).Replace("<", "").Replace(">", "")).[..60]
                    items.Add({| type_ = "post"; id = r1.GetGuid(0).ToString(); title = preview; createdAt = r1.GetDateTime(2) |})
                r1.Close()

                // Recent forum threads
                use cmd2 = new Npgsql.NpgsqlCommand("""
                    SELECT id, title, created_at FROM forum_threads
                    WHERE author_id = @uid
                    ORDER BY created_at DESC LIMIT 15
                """, conn)
                cmd2.Parameters.AddWithValue("uid", userId) |> ignore
                use r2 = cmd2.ExecuteReader()
                while r2.Read() do
                    items.Add({| type_ = "thread"; id = r2.GetGuid(0).ToString(); title = r2.GetString(1); createdAt = r2.GetDateTime(2) |})
                r2.Close()

                // Recent blog articles
                use cmd3 = new Npgsql.NpgsqlCommand("""
                    SELECT id, title, created_at FROM blog_articles
                    WHERE author_id = @uid
                    ORDER BY created_at DESC LIMIT 15
                """, conn)
                cmd3.Parameters.AddWithValue("uid", userId) |> ignore
                use r3 = cmd3.ExecuteReader()
                while r3.Read() do
                    items.Add({| type_ = "article"; id = r3.GetGuid(0).ToString(); title = r3.GetString(1); createdAt = r3.GetDateTime(2) |})
                r3.Close()

                let activity = items |> Seq.sortByDescending (fun a -> a.createdAt) |> Seq.take (min 30 items.Count) |> Seq.toList
                return Results.Ok({| activity = activity |})
            }
        )
    ) |> ignore

    // ── User search (mention autocomplete) ───────────────────────────────────

    app.MapGet(
        "/api/users/search",
        Func<string, int, IResult>(fun q limit ->
            let query = if String.IsNullOrWhiteSpace(q) then "" else q.Trim()
            if query.Length < 1 then Results.Ok([])
            else
                let l = if limit < 1 || limit > 20 then 8 else limit
                Results.Ok(UserRepository.searchUsersByName query l))
    ) |> ignore

    // ── Forum: Global Search ─────────────────────────────────────────────────

    app.MapGet(
        "/api/forum/search",
        Func<HttpContext, string, string, string, string, int, int, IResult>(fun ctx q author fromDate toDate page pageSize ->
            let query = if String.IsNullOrWhiteSpace(q) then "" else q.Trim()
            if query.Length < 2 then Results.BadRequest("Query must be at least 2 characters") :> IResult
            else
                use conn = Database.openConnection()
                let sql = """
                    WITH thread_hits AS (
                        SELECT t.id, t.title, t.forum_id, t.created_at, t.author_id,
                               NULL::uuid AS post_id, NULL::text AS post_snippet,
                               ts_rank(t.search_vector, query) AS rank,
                               'thread' AS hit_type
                        FROM forum_threads t, to_tsquery('english', unaccent(regexp_replace(@q, '\s+', ' & ', 'g'))) query
                        WHERE t.search_vector @@ query
                          AND (@author = '' OR t.author_id::text = @author)
                          AND (@fromDate = '' OR t.created_at >= @fromDate::timestamptz)
                          AND (@toDate   = '' OR t.created_at <= @toDate::timestamptz)
                    ),
                    post_hits AS (
                        SELECT t.id, t.title, t.forum_id, p.created_at, p.author_id,
                               p.id AS post_id,
                               left(regexp_replace(p.content, '<[^>]+>', '', 'g'), 200) AS post_snippet,
                               ts_rank(p.search_vector, query) AS rank,
                               'post' AS hit_type
                        FROM forum_posts p
                        JOIN forum_threads t ON t.id = p.thread_id,
                             to_tsquery('english', unaccent(regexp_replace(@q, '\s+', ' & ', 'g'))) query
                        WHERE p.search_vector @@ query
                          AND p.state IN ('published','flagged')
                          AND p.deleted_at IS NULL
                          AND (@author = '' OR p.author_id::text = @author)
                          AND (@fromDate = '' OR p.created_at >= @fromDate::timestamptz)
                          AND (@toDate   = '' OR p.created_at <= @toDate::timestamptz)
                    )
                    SELECT hit_type, id, title, forum_id, created_at, author_id, post_id, post_snippet, rank
                    FROM (SELECT * FROM thread_hits UNION ALL SELECT * FROM post_hits) combined
                    ORDER BY rank DESC, created_at DESC
                    LIMIT @pageSize OFFSET @offset
                """
                use cmd = new Npgsql.NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("q", query) |> ignore
                cmd.Parameters.AddWithValue("author", if String.IsNullOrWhiteSpace(author) then "" else author) |> ignore
                cmd.Parameters.AddWithValue("fromDate", if String.IsNullOrWhiteSpace(fromDate) then "" else fromDate) |> ignore
                cmd.Parameters.AddWithValue("toDate",   if String.IsNullOrWhiteSpace(toDate)   then "" else toDate)   |> ignore
                let pg = if page < 1 then 1 else page
                let ps = if pageSize < 1 || pageSize > 50 then 20 else pageSize
                cmd.Parameters.AddWithValue("pageSize", ps) |> ignore
                cmd.Parameters.AddWithValue("offset", (pg - 1) * ps) |> ignore
                use r = cmd.ExecuteReader()
                let results =
                    [ while r.Read() do
                        yield {|
                            hitType     = r.GetString(0)
                            threadId    = r.GetGuid(1)
                            title       = r.GetString(2)
                            forumId     = r.GetGuid(3)
                            createdAt   = r.GetDateTime(4)
                            authorId    = r.GetGuid(5)
                            postId      = if r.IsDBNull(6) then None else Some(r.GetGuid(6))
                            postSnippet = if r.IsDBNull(7) then None else Some(r.GetString(7))
                            rank        = r.GetFloat(8)
                        |} ]
                Results.Ok(results) :> IResult)
    ) |> ignore

    // ── Forum: Polls ─────────────────────────────────────────────────────────

    // GET /api/forum/threads/{threadId}/poll — fetch poll with options + vote counts + user's votes
    app.MapGet(
        "/api/forum/threads/{threadId}/poll",
        Func<HttpContext, Guid, IResult>(fun ctx threadId ->
            use conn = Database.openConnection()
            use cmd = new Npgsql.NpgsqlCommand(
                """SELECT p.id, p.question, p.closes_at, p.allow_multiple,
                          o.id, o.text, o.position,
                          COUNT(v.id) AS vote_count
                   FROM forum_polls p
                   JOIN forum_poll_options o ON o.poll_id = p.id
                   LEFT JOIN forum_poll_votes v ON v.option_id = o.id
                   WHERE p.thread_id = @tid
                   GROUP BY p.id, p.question, p.closes_at, p.allow_multiple, o.id, o.text, o.position
                   ORDER BY o.position""", conn)
            cmd.Parameters.AddWithValue("tid", threadId) |> ignore
            use r = cmd.ExecuteReader()
            let mutable pollId = Guid.Empty
            let mutable question = ""
            let mutable closesAt : DateTime option = None
            let mutable allowMultiple = false
            let options = System.Collections.Generic.List<{| id: Guid; text: string; position: int; voteCount: int64 |}>()
            while r.Read() do
                if pollId = Guid.Empty then
                    pollId        <- r.GetGuid(0)
                    question      <- r.GetString(1)
                    closesAt      <- if r.IsDBNull(2) then None else Some (r.GetDateTime(2))
                    allowMultiple <- r.GetBoolean(3)
                options.Add({| id = r.GetGuid(4); text = r.GetString(5); position = r.GetInt32(6); voteCount = r.GetInt64(7) |})
            if pollId = Guid.Empty then Results.NotFound() :> IResult
            else
                let userVotes =
                    match tryGetAuthClaims ctx with
                    | Some claims ->
                        use cmd2 = new Npgsql.NpgsqlCommand(
                            "SELECT option_id FROM forum_poll_votes WHERE poll_id = @pid AND user_id = @uid", conn)
                        cmd2.Parameters.AddWithValue("pid", pollId) |> ignore
                        cmd2.Parameters.AddWithValue("uid", claims.UserId) |> ignore
                        use r2 = cmd2.ExecuteReader()
                        [ while r2.Read() do yield r2.GetGuid(0) ]
                    | None -> []
                Results.Ok({|
                    id = pollId; question = question; closesAt = closesAt
                    allowMultiple = allowMultiple; options = options
                    userVotes = userVotes
                |}) :> IResult)
    ) |> ignore

    // POST /api/forum/threads/{threadId}/poll — create poll (thread author or admin)
    app.MapPost(
        "/api/forum/threads/{threadId}/poll",
        Func<HttpContext, Guid, {| question: string; options: string[]; closesAt: string; allowMultiple: bool |}, IResult>(fun ctx threadId body ->
            match tryGetAuthClaims ctx with
            | None -> Results.Unauthorized() :> IResult
            | Some claims ->
                use conn = Database.openConnection()
                use check = new Npgsql.NpgsqlCommand("SELECT author_id FROM forum_threads WHERE id = @tid", conn)
                check.Parameters.AddWithValue("tid", threadId) |> ignore
                use cr = check.ExecuteReader()
                let authorId = if cr.Read() then Some (cr.GetGuid(0)) else None
                cr.Close()
                match authorId with
                | None -> Results.NotFound() :> IResult
                | Some aid when (match Guid.TryParse(claims.UserId) with true, uid -> aid <> uid | _ -> true) && not (Permissions.isAdmin claims.Role) ->
                    Results.Forbid() :> IResult
                | _ ->
                    let closesAt =
                        if String.IsNullOrWhiteSpace body.closesAt then None
                        else match System.DateTime.TryParse(body.closesAt) with
                             | true, d -> Some d
                             | _ -> None
                    use ins = new Npgsql.NpgsqlCommand(
                        """INSERT INTO forum_polls (thread_id, question, closes_at, allow_multiple)
                           VALUES (@tid, @q, @closes, @multi) RETURNING id""", conn)
                    ins.Parameters.AddWithValue("tid", threadId) |> ignore
                    ins.Parameters.AddWithValue("q", body.question) |> ignore
                    ins.Parameters.AddWithValue("closes", closesAt |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
                    ins.Parameters.AddWithValue("multi", body.allowMultiple) |> ignore
                    let pollId = ins.ExecuteScalar() :?> Guid
                    for i, opt in body.options |> Array.indexed do
                        use oins = new Npgsql.NpgsqlCommand(
                            "INSERT INTO forum_poll_options (poll_id, text, position) VALUES (@pid, @t, @pos)", conn)
                        oins.Parameters.AddWithValue("pid", pollId) |> ignore
                        oins.Parameters.AddWithValue("t", opt) |> ignore
                        oins.Parameters.AddWithValue("pos", i) |> ignore
                        oins.ExecuteNonQuery() |> ignore
                    Results.Created($"/api/forum/threads/{threadId}/poll", {| id = pollId |}) :> IResult)
    ) |> ignore

    // POST /api/forum/polls/{pollId}/vote — cast vote
    app.MapPost(
        "/api/forum/polls/{pollId}/vote",
        Func<HttpContext, Guid, {| optionIds: Guid[] |}, IResult>(fun ctx pollId body ->
            match tryGetAuthClaims ctx with
            | None -> Results.Unauthorized() :> IResult
            | Some claims ->
                use conn = Database.openConnection()
                use pc = new Npgsql.NpgsqlCommand("SELECT allow_multiple, closes_at FROM forum_polls WHERE id = @pid", conn)
                pc.Parameters.AddWithValue("pid", pollId) |> ignore
                use pr = pc.ExecuteReader()
                if not (pr.Read()) then pr.Close(); Results.NotFound() :> IResult
                else
                    let allowMultiple = pr.GetBoolean(0)
                    let closesAt = if pr.IsDBNull(1) then None else Some (pr.GetDateTime(1))
                    pr.Close()
                    match closesAt with
                    | Some d when d < DateTime.UtcNow -> Results.BadRequest("Poll is closed") :> IResult
                    | _ ->
                        let optIds = if allowMultiple then body.optionIds else body.optionIds |> Array.truncate 1
                        use del = new Npgsql.NpgsqlCommand(
                            "DELETE FROM forum_poll_votes WHERE poll_id = @pid AND user_id = @uid", conn)
                        del.Parameters.AddWithValue("pid", pollId) |> ignore
                        del.Parameters.AddWithValue("uid", claims.UserId) |> ignore
                        del.ExecuteNonQuery() |> ignore
                        for optId in optIds do
                            use vi = new Npgsql.NpgsqlCommand(
                                "INSERT INTO forum_poll_votes (poll_id, option_id, user_id) VALUES (@pid, @oid, @uid) ON CONFLICT DO NOTHING", conn)
                            vi.Parameters.AddWithValue("pid", pollId) |> ignore
                            vi.Parameters.AddWithValue("oid", optId) |> ignore
                            vi.Parameters.AddWithValue("uid", claims.UserId) |> ignore
                            vi.ExecuteNonQuery() |> ignore
                        Results.Ok({| voted = true |}) :> IResult)
    ) |> ignore

    // ── Forum: Tags ───────────────────────────────────────────────────────────

    app.MapGet(
        "/api/forum/tags",
        Func<IResult>(fun () -> Results.Ok(ForumRepository.getTags()))
    ) |> ignore

    app.MapPost(
        "/api/forum/tags",
        Func<HttpContext, {| name: string; slug: string; description: string |}, IResult>(fun ctx body ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                let desc = if String.IsNullOrWhiteSpace body.description then None else Some body.description
                let tag  = ForumRepository.createTag body.name body.slug desc
                Results.Created($"/api/forum/tags/{tag.Id}", tag)
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapDelete(
        "/api/forum/tags/{id}",
        Func<HttpContext, Guid, IResult>(fun ctx tagId ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                if ForumRepository.deleteTag tagId then Results.NoContent()
                else Results.NotFound()
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapGet(
        "/api/forum/threads/{id}/tags",
        Func<Guid, IResult>(fun threadId ->
            Results.Ok(ForumRepository.getTagsForThread threadId))
    ) |> ignore

    app.MapPut(
        "/api/forum/threads/{id}/tags",
        Func<HttpContext, Guid, {| tagIds: Guid list |}, System.Threading.Tasks.Task<IResult>>(fun ctx threadId body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims ->
                    let thread = ForumRepository.getThreadById threadId
                    match thread with
                    | None -> return Results.NotFound()
                    | Some t ->
                        let userId = System.Guid.Parse(claims.UserId)
                        let isMod  = Permissions.isAdmin claims.Role || Permissions.hasContextRole (Database.openConnection()) userId Permissions.ModuleForum Permissions.RoleModerator (Some t.ForumId)
                        if t.AuthorId = userId || isMod then
                            ForumRepository.setTagsForThread threadId (body.tagIds |> List.ofSeq)
                            return Results.Ok(ForumRepository.getTagsForThread threadId)
                        else return Results.Forbid()
                | None -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Blog: Sections ────────────────────────────────────────────────────────

    app.MapGet(
        "/api/blog/sections",
        Func<IResult>(fun () -> Results.Ok(BlogRepository.getSections()))
    ) |> ignore

    app.MapPost(
        "/api/blog/sections",
        Func<HttpContext, {| name: string; description: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    let desc = if String.IsNullOrWhiteSpace body.description then None else Some body.description
                    return match BlogRepository.createSection body.name desc with
                           | Some s -> Results.Created($"/api/blog/sections/{s.Slug}", s)
                           | None   -> Results.Problem(detail = "Failed to create section", statusCode = 500, title = "Error")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Blog: Tags ────────────────────────────────────────────────────────────

    app.MapGet(
        "/api/blog/tags",
        Func<IResult>(fun () -> Results.Ok(BlogRepository.getTags()))
    ) |> ignore

    app.MapPost(
        "/api/blog/tags",
        Func<HttpContext, {| name: string; description: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    let desc = if String.IsNullOrWhiteSpace body.description then None else Some body.description
                    return match BlogRepository.createTag body.name desc with
                           | Some t -> Results.Created($"/api/blog/tags/{t.Id}", t)
                           | None   -> Results.Conflict("Tag with that name already exists")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPut(
        "/api/blog/tags/{id}",
        Func<string, HttpContext, {| name: string; description: string |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid tag id")
                | true, tid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        let desc = if String.IsNullOrWhiteSpace body.description then None else Some body.description
                        return match BlogRepository.updateTag tid body.name desc with
                               | Some t -> Results.Ok(t)
                               | None   -> Results.NotFound()
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/blog/tags/{id}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid tag id")
                | true, tid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        return if BlogRepository.deleteTag tid then Results.Ok() else Results.NotFound()
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Blog: Articles ────────────────────────────────────────────────────────

    app.MapGet(
        "/api/blog/articles",
        Func<HttpContext, IResult>(fun ctx ->
            let q2 = ctx.Request.Query
            let sid = match Guid.TryParse(string q2["sectionId"]) with | true, g -> Some g | _ -> None
            let q   = let v = string q2["search"]   in if String.IsNullOrWhiteSpace v then None else Some v
            let t   = let v = string q2["tag"]      in if String.IsNullOrWhiteSpace v then None else Some v
            let p   = match System.Int32.TryParse(string q2["page"])     with | true, v when v > 0 -> v | _ -> 1
            let ps  = match System.Int32.TryParse(string q2["pageSize"]) with | true, v when v > 0 && v <= 50 -> v | _ -> 20
            Results.Ok(BlogRepository.getPublishedArticles sid q t p ps))
    ) |> ignore

    app.MapGet(
        "/api/blog/articles/random",
        Func<IResult>(fun () ->
            match BlogRepository.getRandomPublishedArticle () with
            | Some a -> Results.Ok(a)
            | None   -> Results.NotFound())
    ) |> ignore

    app.MapGet(
        "/api/blog/articles/{slug}",
        Func<string, IResult>(fun slug ->
            match BlogRepository.getArticleBySlug slug with
            | Some a -> Results.Ok(a)
            | None   -> Results.NotFound())
    ) |> ignore

    app.MapGet(
        "/api/blog/articles/{id}/tags",
        Func<string, IResult>(fun id ->
            match Guid.TryParse(id) with
            | true, aid -> Results.Ok(BlogRepository.getTagsForArticle aid)
            | _ -> Results.BadRequest("Invalid article id"))
    ) |> ignore

    app.MapGet(
        "/api/blog/my-articles",
        Func<HttpContext, int, int, System.Threading.Tasks.Task<IResult>>(fun ctx page pageSize ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, userId ->
                        let p  = if page < 1 then 1 else page
                        let ps = if pageSize < 1 || pageSize > 50 then 20 else pageSize
                        return Results.Ok(BlogRepository.getArticlesByAuthor userId p ps)
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/blog/articles",
        Func<HttpContext, {| sectionId: string; title: string; subtitle: string; content: string; bodyJson: string; excerpt: string; visibility: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, userId ->
                        let sectionId =
                            match Guid.TryParse(body.sectionId) with
                            | true, g -> g
                            | _       -> (BlogRepository.getOrCreateDefaultSection ()).Id
                        let subtitle    = if String.IsNullOrWhiteSpace body.subtitle  then None else Some body.subtitle
                        let excerpt     = if String.IsNullOrWhiteSpace body.excerpt   then None else Some body.excerpt
                        let bodyJson    = if String.IsNullOrWhiteSpace body.bodyJson  then None else Some body.bodyJson
                        let visibility  = if String.IsNullOrWhiteSpace body.visibility then "public" else body.visibility
                        return match BlogRepository.createArticle sectionId userId body.title subtitle body.content bodyJson excerpt visibility with
                               | Some a -> Results.Created($"/api/blog/articles/{a.Slug}", a)
                               | None   -> Results.Problem(detail = "Failed to create article", statusCode = 500, title = "Error")
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPut(
        "/api/blog/articles/{id}",
        Func<string, HttpContext, {| title: string; subtitle: string; content: string; bodyJson: string; excerpt: string; coverUrl: string; visibility: string |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid article id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            let isAdmin  = Permissions.isAdmin claims.Role
                            let subtitle = if String.IsNullOrWhiteSpace body.subtitle  then None else Some body.subtitle
                            let excerpt  = if String.IsNullOrWhiteSpace body.excerpt   then None else Some body.excerpt
                            let bodyJson = if String.IsNullOrWhiteSpace body.bodyJson  then None else Some body.bodyJson
                            let coverUrl = if String.IsNullOrWhiteSpace body.coverUrl  then None else Some body.coverUrl
                            let vis      = if String.IsNullOrWhiteSpace body.visibility then "public" else body.visibility
                            return match BlogRepository.updateArticle aid userId isAdmin body.title subtitle body.content bodyJson excerpt coverUrl vis with
                                   | Some a -> Results.Ok(a)
                                   | None   -> Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/blog/articles/{id}/status",
        Func<string, HttpContext, {| status: string; note: string |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid article id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            let isAdmin = Permissions.isAdmin claims.Role
                            let allowed =
                                match body.status with
                                | "submitted" | "draft" -> true
                                | "approved" | "published" | "rejected" | "under_review" | "needs_revision" -> isAdmin
                                | _ -> false
                            if not allowed then return Results.Forbid()
                            else
                                match BlogRepository.setArticleStatus aid body.status with
                                | None -> return Results.NotFound()
                                | Some a ->
                                    let note = if String.IsNullOrWhiteSpace body.note then None else Some body.note
                                    BlogRepository.logModerationAction aid (Some userId) body.status note |> ignore
                                    return Results.Ok(a)
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPut(
        "/api/blog/articles/{id}/tags",
        Func<string, HttpContext, {| tagIds: string[] |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid article id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        let tagIds = body.tagIds |> Array.choose (fun s -> match Guid.TryParse(s) with | true, g -> Some g | _ -> None) |> Array.toList
                        BlogRepository.setArticleTags aid tagIds
                        return Results.Ok()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/blog/articles/{id}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid article id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            let deleted = BlogRepository.deleteArticle aid userId (Permissions.isAdmin claims.Role)
                            return if deleted then Results.Ok() else Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Blog: Authors ─────────────────────────────────────────────────────────

    app.MapGet(
        "/api/blog/authors",
        Func<IResult>(fun () -> Results.Ok(BlogRepository.listAuthors()))
    ) |> ignore

    app.MapGet(
        "/api/blog/authors/{userId}",
        Func<string, IResult>(fun userId ->
            match Guid.TryParse(userId) with
            | false, _ -> Results.BadRequest("Invalid user id")
            | true, uid ->
                match BlogRepository.getAuthor uid with
                | Some a -> Results.Ok(a)
                | None   -> Results.NotFound())
    ) |> ignore

    app.MapPost(
        "/api/blog/authors",
        Func<HttpContext, {| userId: string; bio: string; displayName: string; avatarUrl: string; socialLinks: string; trusted: bool |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(body.userId) with
                    | false, _ -> return Results.BadRequest("Invalid userId")
                    | true, uid ->
                        let bio     = if String.IsNullOrWhiteSpace body.bio         then None else Some body.bio
                        let dn      = if String.IsNullOrWhiteSpace body.displayName then None else Some body.displayName
                        let av      = if String.IsNullOrWhiteSpace body.avatarUrl   then None else Some body.avatarUrl
                        let sl      = if String.IsNullOrWhiteSpace body.socialLinks then "[]" else body.socialLinks
                        return match BlogRepository.upsertAuthor uid bio dn av sl body.trusted with
                               | Some a -> Results.Created($"/api/blog/authors/{uid}", a)
                               | None   -> Results.Problem(detail = "Failed to create author", statusCode = 500, title = "Error")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPut(
        "/api/blog/authors/{userId}",
        Func<string, HttpContext, {| bio: string; displayName: string; avatarUrl: string; socialLinks: string; trusted: bool |}, System.Threading.Tasks.Task<IResult>>(fun userId ctx body ->
            async {
                match Guid.TryParse(userId) with
                | false, _ -> return Results.BadRequest("Invalid user id")
                | true, uid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, requesterId ->
                            let isAdmin = Permissions.isAdmin claims.Role
                            if requesterId <> uid && not isAdmin then return Results.Forbid()
                            else
                                let bio     = if String.IsNullOrWhiteSpace body.bio         then None else Some body.bio
                                let dn      = if String.IsNullOrWhiteSpace body.displayName then None else Some body.displayName
                                let av      = if String.IsNullOrWhiteSpace body.avatarUrl   then None else Some body.avatarUrl
                                let sl      = if String.IsNullOrWhiteSpace body.socialLinks then "[]" else body.socialLinks
                                let trusted = if isAdmin then body.trusted else
                                                  (BlogRepository.getAuthor uid |> Option.map (fun a -> a.Trusted) |> Option.defaultValue false)
                                return match BlogRepository.upsertAuthor uid bio dn av sl trusted with
                                       | Some a -> Results.Ok(a)
                                       | None   -> Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/blog/authors/{userId}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun userId ctx ->
            async {
                match Guid.TryParse(userId) with
                | false, _ -> return Results.BadRequest("Invalid user id")
                | true, uid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        return if BlogRepository.deleteAuthor uid then Results.Ok() else Results.NotFound()
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Blog: Uploads ─────────────────────────────────────────────────────────

    // Client flow: call /api/media/upload-url to get S3 presigned URL, upload file
    // directly to S3, then POST here to register the upload and trigger conversion.
    app.MapPost(
        "/api/blog/uploads",
        Func<HttpContext, {| originalFilename: string; mimeType: string; format: string; storageKey: string; sizeBytes: int64; conversionOption: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, userId ->
                        let validFormats = Set.ofList ["docx";"pdf";"md";"html";"txt"]
                        let validOptions = Set.ofList ["as-is";"convert";"reformat"]
                        let allowedFormats  = String.concat ", " validFormats
                        let allowedOptions  = String.concat ", " validOptions
                        if not (Set.contains body.format validFormats) then
                            return Results.BadRequest($"Invalid format. Allowed: {allowedFormats}") :> IResult
                        elif not (Set.contains body.conversionOption validOptions) then
                            return Results.BadRequest($"Invalid conversionOption. Allowed: {allowedOptions}") :> IResult
                        else
                            let size = if body.sizeBytes > 0L then Some body.sizeBytes else None
                            match BlogRepository.createUpload userId body.originalFilename body.mimeType body.format body.storageKey size body.conversionOption with
                            | None -> return Results.Problem(detail = "Failed to create upload record", statusCode = 500, title = "Error") :> IResult
                            | Some upload ->
                                let s3BaseUrl = Environment.GetEnvironmentVariable("S3_BUCKET")
                                let s3Region  = Environment.GetEnvironmentVariable("S3_REGION")
                                let fileUrl = $"https://{s3BaseUrl}.s3.{s3Region}.amazonaws.com/{body.storageKey}"
                                let converted =
                                    match body.format, body.conversionOption with
                                    | ("html" | "md" | "txt"), "convert" ->
                                        let convHtml = $"""<p>Content loaded from: <a href="{fileUrl}">{body.originalFilename}</a></p>"""
                                        BlogRepository.updateUploadConversion upload.Id "done" (Some convHtml) None
                                    | _ ->
                                        BlogRepository.updateUploadConversion upload.Id "done" None None
                                return match converted with
                                       | Some u -> Results.Created($"/api/blog/uploads/{u.Id}", u) :> IResult
                                       | None   -> Results.Ok(upload) :> IResult
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapGet(
        "/api/blog/uploads/{id}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid upload id")
                | true, uid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        return match BlogRepository.getUpload uid with
                               | Some u -> Results.Ok(u)
                               | None   -> Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/blog/uploads/{id}/attach",
        Func<string, HttpContext, {| articleId: string |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id), Guid.TryParse(body.articleId) with
                | (true, uid), (true, aid) ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        return match BlogRepository.attachUploadToArticle uid aid with
                               | Some u -> Results.Ok(u)
                               | None   -> Results.NotFound()
                | _ -> return Results.BadRequest("Invalid upload or article id")
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Blog: Moderation ──────────────────────────────────────────────────────

    app.MapGet(
        "/api/blog/articles/{id}/moderation-log",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid article id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        return Results.Ok(BlogRepository.getModerationLog aid)
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/blog/articles/{id}/feature",
        Func<string, HttpContext, {| featured: bool; position: int |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid article id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            let pos = if body.position > 0 then Some body.position else None
                            let action = if body.featured then "featured" else "unfeatured"
                            BlogRepository.logModerationAction aid (Some userId) action None |> ignore
                            return match BlogRepository.setArticleFeatured aid body.featured pos with
                                   | Some a -> Results.Ok(a)
                                   | None   -> Results.NotFound()
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/blog/articles/{id}/pin",
        Func<string, HttpContext, {| pinned: bool |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid article id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            let action = if body.pinned then "pinned" else "unpinned"
                            BlogRepository.logModerationAction aid (Some userId) action None |> ignore
                            return match BlogRepository.setArticlePinned aid body.pinned with
                                   | Some a -> Results.Ok(a)
                                   | None   -> Results.NotFound()
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Blog: Comments ────────────────────────────────────────────────────────

    app.MapGet(
        "/api/blog/articles/{id}/comments",
        Func<string, IResult>(fun id ->
            match Guid.TryParse(id) with
            | true, aid -> Results.Ok(BlogRepository.getComments aid)
            | _ -> Results.BadRequest("Invalid article id"))
    ) |> ignore

    app.MapPost(
        "/api/blog/articles/{id}/comments",
        Func<string, HttpContext, {| content: string |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid article id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            return match BlogRepository.createComment aid userId body.content with
                                   | Some c -> Results.Created($"/api/blog/comments/{c.Id}", c)
                                   | None   -> Results.Problem(detail = "Failed to create comment", statusCode = 500, title = "Error")
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/blog/comments/{id}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid comment id")
                | true, cid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        let deleted = BlogRepository.deleteComment cid
                        return if deleted then Results.Ok() else Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Site Config ───────────────────────────────────────────────────────────

    app.MapGet(
        "/api/config",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun scope ctx ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    let s = if String.IsNullOrWhiteSpace scope then None else Some scope
                    return Results.Ok(BlogRepository.getConfig s)
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPut(
        "/api/config/{scope}/{key}",
        Func<string, string, HttpContext, {| value: string |}, System.Threading.Tasks.Task<IResult>>(fun scope key ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, userId ->
                        return match BlogRepository.setConfig scope key body.value userId with
                               | Some c -> Results.Ok(c)
                               | None   -> Results.Problem(detail = "Failed to save config", statusCode = 500, title = "Error")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Announcements ─────────────────────────────────────────────────────────

    // Public: get recent published announcements
    app.MapGet(
        "/api/announcements",
        Func<int, IResult>(fun limit ->
            let lim = if limit < 1 || limit > 50 then 10 else limit
            Results.Ok(AnnouncementRepository.getPublished lim))
    ) |> ignore

    // Admin: get all (including drafts)
    app.MapGet(
        "/api/admin/announcements",
        Func<HttpContext, System.Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    return Results.Ok(AnnouncementRepository.getAll ())
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // Admin: create announcement
    app.MapPost(
        "/api/admin/announcements",
        Func<HttpContext, {| title: string; body: string; priority: int; expiresAt: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, userId ->
                        let expiresAt =
                            match DateTime.TryParse(body.expiresAt) with
                            | true, dt -> Some dt
                            | _        -> None
                        return match AnnouncementRepository.create userId body.title body.body body.priority expiresAt with
                               | Some a -> Results.Created($"/api/admin/announcements/{a.Id}", a)
                               | None   -> Results.Problem(detail = "Failed to create announcement", statusCode = 500, title = "Error")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // Admin: update announcement
    app.MapPut(
        "/api/admin/announcements/{id}",
        Func<string, HttpContext, {| title: string; body: string; priority: int; expiresAt: string |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        let expiresAt =
                            match DateTime.TryParse(body.expiresAt) with
                            | true, dt -> Some dt
                            | _        -> None
                        return match AnnouncementRepository.update aid body.title body.body body.priority expiresAt with
                               | Some a -> Results.Ok(a)
                               | None   -> Results.NotFound()
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // Admin: publish and email all confirmed subscribers
    app.MapPost(
        "/api/admin/announcements/{id}/publish",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        match AnnouncementRepository.publish aid with
                        | None -> return Results.NotFound()
                        | Some a ->
                            // Fire-and-forget email blast to confirmed subscribers
                            let subs = AnnouncementRepository.getConfirmedSubscribers ()
                            let baseUrl = "https://lagdaemon.com"
                            for sub in subs do
                                let unsubUrl = $"{baseUrl}/api/announcements/unsubscribe?token={sub.UnsubscribeToken}"
                                let html = Email.announcementEmailTemplate a.Title a.Body unsubUrl
                                Email.sendEmail { To = sub.Email; Subject = a.Title; HtmlBody = html }
                                |> Async.Ignore |> Async.Start
                            return Results.Ok(a)
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // Admin: unpublish
    app.MapPost(
        "/api/admin/announcements/{id}/unpublish",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        return match AnnouncementRepository.unpublish aid with
                               | Some a -> Results.Ok(a)
                               | None   -> Results.NotFound()
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // Admin: delete announcement
    app.MapDelete(
        "/api/admin/announcements/{id}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid id")
                | true, aid ->
                    match tryGetAuthClaims ctx with
                    | Some claims when Permissions.isAdmin claims.Role ->
                        return if AnnouncementRepository.delete aid then Results.Ok() else Results.NotFound()
                    | Some _ -> return Results.Forbid()
                    | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // Public: subscribe (logged-in users auto-confirm)
    app.MapPost(
        "/api/announcements/subscribe",
        Func<HttpContext, {| email: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                let email = body.email.Trim().ToLowerInvariant()
                if String.IsNullOrWhiteSpace(email) then
                    return Results.BadRequest("Email required") :> IResult
                else
                    let claims = tryGetAuthClaims ctx
                    let userId = claims |> Option.bind (fun c -> match Guid.TryParse(c.UserId) with | true, g -> Some g | _ -> None)
                    let autoConfirm = claims.IsSome
                    match AnnouncementRepository.subscribe userId email autoConfirm with
                    | None -> return Results.Problem(detail = "Failed to subscribe", statusCode = 500, title = "Error") :> IResult
                    | Some sub ->
                        if not autoConfirm then
                            // Send confirmation email
                            let baseUrl = "https://lagdaemon.com"
                            let confirmUrl = $"{baseUrl}/api/announcements/confirm?token={sub.UnsubscribeToken}"
                            let html = Email.confirmSubscriptionEmailTemplate confirmUrl
                            Email.sendEmail { To = email; Subject = "Confirm your subscription"; HtmlBody = html }
                            |> Async.Ignore |> Async.Start
                        return Results.Ok({| subscribed = true; confirmed = sub.Confirmed |}) :> IResult
            } |> Async.StartAsTask)
    ) |> ignore

    // Public: check subscription status
    app.MapGet(
        "/api/announcements/subscribed",
        Func<HttpContext, System.Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Ok({| subscribed = false; confirmed = false |})
                | Some claims ->
                    let email =
                        match AnnouncementRepository.getSubscriptionByEmail claims.Email with
                        | Some s -> Some s
                        | None   -> None
                    return match email with
                           | Some s -> Results.Ok({| subscribed = true; confirmed = s.Confirmed |})
                           | None   -> Results.Ok({| subscribed = false; confirmed = false |})
            } |> Async.StartAsTask)
    ) |> ignore

    // Public: unsubscribe via token (from email link)
    app.MapGet(
        "/api/announcements/unsubscribe",
        Func<string, IResult>(fun token ->
            if AnnouncementRepository.unsubscribeByToken token then
                Results.Ok({| message = "You have been unsubscribed." |})
            else
                Results.NotFound())
    ) |> ignore

    // Public: confirm subscription
    app.MapGet(
        "/api/announcements/confirm",
        Func<string, IResult>(fun token ->
            match AnnouncementRepository.confirmSubscription token with
            | Some _ -> Results.Ok({| message = "Subscription confirmed!" |})
            | None   -> Results.NotFound())
    ) |> ignore

    // Public: unsubscribe by email (for logged-in users)
    app.MapDelete(
        "/api/announcements/subscribe",
        Func<HttpContext, System.Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    AnnouncementRepository.unsubscribeByEmail claims.Email |> ignore
                    return Results.Ok()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Papers ────────────────────────────────────────────────────────────────

    app.MapGet(
        "/api/papers",
        Func<HttpContext, System.Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, userId ->
                        use conn = Database.openConnection()
                        return Results.Ok(PaperRepository.getPapersByOwner conn userId)
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapGet(
        "/api/papers/{id}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid paper id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        use conn = Database.openConnection()
                        match PaperRepository.getPaperById conn pid with
                        | None -> return Results.NotFound()
                        | Some p when p.OwnerId.ToString() = claims.UserId || Permissions.isAdmin claims.Role ->
                            return Results.Ok(p)
                        | _ -> return Results.Forbid()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/papers",
        Func<HttpContext, {| title: string; summary: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, userId ->
                        let abs = if String.IsNullOrWhiteSpace(body.summary) then None else Some body.summary
                        use conn = Database.openConnection()
                        return match PaperRepository.createPaper conn userId body.title abs with
                               | Some p -> Results.Created($"/api/papers/{p.Id}", p)
                               | None   -> Results.Problem(detail = "Failed to create paper", statusCode = 500, title = "Error")
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPut(
        "/api/papers/{id}",
        Func<string, HttpContext, {| title: string; summary: string |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid paper id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        use conn = Database.openConnection()
                        match PaperRepository.getPaperById conn pid with
                        | None -> return Results.NotFound()
                        | Some p when p.OwnerId.ToString() <> claims.UserId && not (Permissions.isAdmin claims.Role) ->
                            return Results.Forbid()
                        | _ ->
                            let abs = if String.IsNullOrWhiteSpace(body.summary) then None else Some body.summary
                            return match PaperRepository.updatePaper conn pid body.title abs with
                                   | Some updated -> Results.Ok(updated)
                                   | None         -> Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/papers/{id}/status",
        Func<string, HttpContext, {| status: string |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid paper id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        use conn = Database.openConnection()
                        match PaperRepository.getPaperById conn pid with
                        | None -> return Results.NotFound()
                        | Some p when p.OwnerId.ToString() <> claims.UserId && not (Permissions.isAdmin claims.Role) ->
                            return Results.Forbid()
                        | _ ->
                            return match PaperRepository.setStatus conn pid body.status with
                                   | Some updated -> Results.Ok(updated)
                                   | None         -> Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/papers/{id}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid paper id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some claims ->
                        match Guid.TryParse(claims.UserId) with
                        | false, _ -> return Results.Unauthorized()
                        | true, userId ->
                            use conn = Database.openConnection()
                            let deleted = PaperRepository.deletePaper conn pid userId (Permissions.isAdmin claims.Role)
                            return if deleted then Results.Ok() else Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    // Papers: Sections

    app.MapGet(
        "/api/papers/{id}/sections",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid paper id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        use conn = Database.openConnection()
                        return Results.Ok(PaperRepository.getSections conn pid)
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/papers/{id}/sections",
        Func<string, HttpContext, {| title: string; position: int |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid paper id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        use conn = Database.openConnection()
                        return match PaperRepository.createSection conn pid body.title body.position with
                               | Some s -> Results.Created($"/api/papers/{pid}/sections/{s.Id}", s)
                               | None   -> Results.Problem(detail = "Failed to create section", statusCode = 500, title = "Error")
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPut(
        "/api/papers/sections/{sectionId}",
        Func<string, HttpContext, {| title: string; content: string |}, System.Threading.Tasks.Task<IResult>>(fun sectionId ctx body ->
            async {
                match Guid.TryParse(sectionId) with
                | false, _ -> return Results.BadRequest("Invalid section id")
                | true, sid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        use conn = Database.openConnection()
                        return match PaperRepository.updateSection conn sid body.title body.content with
                               | Some s -> Results.Ok(s)
                               | None   -> Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/papers/sections/{sectionId}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun sectionId ctx ->
            async {
                match Guid.TryParse(sectionId) with
                | false, _ -> return Results.BadRequest("Invalid section id")
                | true, sid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        use conn = Database.openConnection()
                        let deleted = PaperRepository.deleteSection conn sid
                        return if deleted then Results.Ok() else Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    // Papers: Collaborators

    app.MapGet(
        "/api/papers/{id}/collaborators",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid paper id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        use conn = Database.openConnection()
                        return Results.Ok(PaperRepository.getCollaborators conn pid)
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/papers/{id}/collaborators",
        Func<string, HttpContext, {| name: string; email: string; role: string; isExternal: bool |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid paper id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        let email = if String.IsNullOrWhiteSpace(body.email) then None else Some body.email
                        use conn = Database.openConnection()
                        PaperRepository.addCollaborator conn pid body.name email body.role None body.isExternal |> ignore
                        return Results.Ok()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/papers/{id}/collaborators/{name}",
        Func<string, string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id name ctx ->
            async {
                match Guid.TryParse(id) with
                | false, _ -> return Results.BadRequest("Invalid paper id")
                | true, pid ->
                    match tryGetAuthClaims ctx with
                    | None -> return Results.Unauthorized()
                    | Some _ ->
                        use conn = Database.openConnection()
                        let removed = PaperRepository.removeCollaborator conn pid name
                        return if removed then Results.Ok() else Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Admin ─────────────────────────────────────────────────────────────────

    app.MapGet(
        "/api/admin/users",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                let q  = ctx.Request.Query
                let s  = let v = string q["search"] in if String.IsNullOrWhiteSpace v then None else Some v
                let r  = let v = string q["role"]   in if String.IsNullOrWhiteSpace v then None else Some v
                let st = let v = string q["status"] in if String.IsNullOrWhiteSpace v then None else Some v
                let p  = match System.Int32.TryParse(string q["page"])     with | true, v when v > 0 -> v | _ -> 1
                let ps = match System.Int32.TryParse(string q["pageSize"]) with | true, v when v > 0 && v <= 100 -> v | _ -> 50
                let rows  = UserRepository.getAdminUsers s r st p ps
                let total = UserRepository.countAdminUsers s r st
                Results.Ok({| data = rows; total = total; page = p; pageSize = ps |})
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapPatch(
        "/api/admin/users/{id}/role",
        Func<string, HttpContext, {| role: string |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(id), Guid.TryParse(claims.UserId) with
                    | (true, uid), (true, adminId) ->
                        use conn = Database.openConnection()
                        use cmd = new Npgsql.NpgsqlCommand(
                            "UPDATE users SET role = @role WHERE id = @id", conn)
                        cmd.Parameters.AddWithValue("role", body.role) |> ignore
                        cmd.Parameters.AddWithValue("id", uid) |> ignore
                        let affected = cmd.ExecuteNonQuery()
                        if affected > 0 then
                            UserRepository.logAdminAudit adminId uid "update" (Some "role") None (Some body.role)
                        return if affected > 0 then Results.Ok() else Results.NotFound()
                    | _ -> return Results.BadRequest("Invalid id")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/admin/users/{id}/display-name",
        Func<string, HttpContext, {| displayName: string |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(id), Guid.TryParse(claims.UserId) with
                    | (true, uid), (true, adminId) ->
                        let dn = if String.IsNullOrWhiteSpace body.displayName then None else Some (body.displayName.Trim())
                        use conn = Database.openConnection()
                        use cmd = new Npgsql.NpgsqlCommand(
                            "UPDATE users SET display_name = @dn, updated_at = now() WHERE id = @id", conn)
                        cmd.Parameters.AddWithValue("dn", if dn.IsSome then box dn.Value else box DBNull.Value) |> ignore
                        cmd.Parameters.AddWithValue("id", uid) |> ignore
                        let affected = cmd.ExecuteNonQuery()
                        if affected > 0 then
                            UserRepository.logAdminAudit adminId uid "update" (Some "display_name") None dn
                        return if affected > 0 then Results.Ok() else Results.NotFound()
                    | _ -> return Results.BadRequest("Invalid id")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/admin/users/{id}/verify",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(id), Guid.TryParse(claims.UserId) with
                    | (true, uid), (true, adminId) ->
                        let! ok = UserRepository.verifyEmail uid
                        if ok then UserRepository.logAdminAudit adminId uid "verify_email" None None None
                        return if ok then Results.Ok() else Results.NotFound()
                    | _ -> return Results.BadRequest("Invalid id")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/admin/users/{id}/suspend",
        Func<string, HttpContext, {| suspend: bool |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(id), Guid.TryParse(claims.UserId) with
                    | (true, uid), (true, adminId) ->
                        let newStatus = if body.suspend then "suspended" else "active"
                        use conn = Database.openConnection()
                        use cmd = new Npgsql.NpgsqlCommand(
                            "UPDATE users SET status = @status, updated_at = now() WHERE id = @id", conn)
                        cmd.Parameters.AddWithValue("status", newStatus) |> ignore
                        cmd.Parameters.AddWithValue("id", uid) |> ignore
                        let affected = cmd.ExecuteNonQuery()
                        if affected > 0 then
                            UserRepository.logAdminAudit adminId uid "update" (Some "status") None (Some newStatus)
                        return if affected > 0 then Results.Ok() else Results.NotFound()
                    | _ -> return Results.BadRequest("Invalid id")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPatch(
        "/api/admin/users/{id}/restrict",
        Func<string, HttpContext, {| restrict: bool |}, System.Threading.Tasks.Task<IResult>>(fun id ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(id), Guid.TryParse(claims.UserId) with
                    | (true, uid), (true, adminId) ->
                        let newStatus = if body.restrict then "restricted" else "active"
                        use conn = Database.openConnection()
                        use cmd = new Npgsql.NpgsqlCommand(
                            "UPDATE users SET status = @status, updated_at = now() WHERE id = @id", conn)
                        cmd.Parameters.AddWithValue("status", newStatus) |> ignore
                        cmd.Parameters.AddWithValue("id", uid) |> ignore
                        let affected = cmd.ExecuteNonQuery()
                        if affected > 0 then
                            UserRepository.logAdminAudit adminId uid "update" (Some "status") None (Some newStatus)
                        return if affected > 0 then Results.Ok() else Results.NotFound()
                    | _ -> return Results.BadRequest("Invalid id")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/admin/users/{id}/reset-password",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(id) with
                    | false, _ -> return Results.BadRequest("Invalid user id")
                    | true, uid ->
                        let! userOpt = UserRepository.tryGetById uid
                        match userOpt with
                        | None -> return Results.NotFound()
                        | Some u ->
                            let token = Auth.generateSecureToken()
                            let! _ = UserRepository.createPasswordResetToken uid token
                            let resetUrl = "https://lagdaemon.com/djehuti/#/reset-password?token=" + token
                            let msg = { Email.To = u.Email
                                        Email.Subject = "Password Reset - Lag Daemon"
                                        Email.HtmlBody = "<p>An admin has initiated a password reset for your account.</p><p><a href=\"" + resetUrl + "\">Click here to set your password</a></p><p>This link expires in 1 hour.</p>" }
                            Email.sendEmail msg |> Async.Ignore |> Async.Start
                            return Results.Ok()
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/admin/users/{id}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(id), Guid.TryParse(claims.UserId) with
                    | (true, uid), (true, adminId) ->
                        if uid = adminId then return Results.BadRequest("Cannot delete your own account")
                        else
                            let deleted = UserRepository.hardDeleteUser uid
                            if deleted then
                                UserRepository.logAdminAudit adminId uid "hard_delete" None None None
                                return Results.Ok()
                            else return Results.NotFound()
                    | _ -> return Results.BadRequest("Invalid id")
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPost(
        "/api/admin/users/invite",
        Func<HttpContext, {| email: string; role: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, adminId ->
                        let email = body.email.Trim().ToLower()
                        if String.IsNullOrWhiteSpace email then return Results.BadRequest("Email required")
                        else
                            let! existing = UserRepository.tryGetByEmail email
                            if existing.IsSome then return Results.Conflict("User already exists")
                            else
                                let! userOpt = UserRepository.createUser email None
                                match userOpt with
                                | None -> return Results.Problem(detail = "Failed to create user", statusCode = 500, title = "Error")
                                | Some u ->
                                    let allowedRoles = ["user";"admin";"moderator";"author"]
                                    let role = if List.contains body.role allowedRoles then body.role else "user"
                                    use conn = Database.openConnection()
                                    use cmd = new Npgsql.NpgsqlCommand("UPDATE users SET role = @role, status = 'active' WHERE id = @id", conn)
                                    cmd.Parameters.AddWithValue("role", role) |> ignore
                                    cmd.Parameters.AddWithValue("id",   u.Id)  |> ignore
                                    cmd.ExecuteNonQuery() |> ignore
                                    let token = Auth.generateSecureToken()
                                    let! _ = UserRepository.createPasswordResetToken u.Id token
                                    let inviteUrl = "https://lagdaemon.com/djehuti/#/reset-password?token=" + token
                                    let msg = { Email.To = email
                                                Email.Subject = "You have been invited to Lag Daemon"
                                                Email.HtmlBody = "<p>An administrator has created an account for you on Lag Daemon.</p><p><a href=\"" + inviteUrl + "\">Click here to set your password and get started</a></p><p>This link expires in 1 hour.</p>" }
                                    Email.sendEmail msg |> Async.Ignore |> Async.Start
                                    UserRepository.logAdminAudit adminId u.Id "invite" (Some "role") None (Some role)
                                    return Results.Created("/api/admin/users", {| id = u.Id; email = email; role = role |})
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapGet(
        "/api/admin/blog/queue",
        Func<HttpContext, System.Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    return Results.Ok(BlogRepository.getModerationQueue ())
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapGet(
        "/api/admin/blog/articles",
        Func<HttpContext, System.Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    return Results.Ok(BlogRepository.getAllArticlesAdmin ())
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapGet(
        "/api/admin/context-roles",
        Func<HttpContext, System.Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    use conn = Database.openConnection()
                    let roles = Permissions.getAllContextRoles conn
                    return Results.Ok(roles)
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapDelete(
        "/api/admin/context-roles/{id}",
        Func<string, HttpContext, System.Threading.Tasks.Task<IResult>>(fun id ctx ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    match Guid.TryParse(id) with
                    | false, _ -> return Results.BadRequest("Invalid role id")
                    | true, rid ->
                        use conn = Database.openConnection()
                        let removed = Permissions.revokeContextRoleById conn rid
                        return if removed then Results.Ok() else Results.NotFound()
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    // ── User Profiles ─────────────────────────────────────────────────────────

    app.MapGet(
        "/api/profiles/{userId}",
        Func<string, IResult>(fun userId ->
            match Guid.TryParse(userId) with
            | false, _ -> Results.BadRequest("Invalid user id")
            | true, uid ->
                use conn = Database.openConnection()
                match UserProfileRepository.getProfile conn uid with
                | Some p -> Results.Ok(p)
                | None   -> Results.NotFound())
    ) |> ignore

    app.MapGet(
        "/api/profiles/me",
        Func<HttpContext, System.Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, userId ->
                        use conn = Database.openConnection()
                        match UserProfileRepository.getProfile conn userId with
                        | Some p -> return Results.Ok(p)
                        | None   -> return Results.NotFound()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPut(
        "/api/profiles/me",
        Func<HttpContext, {| displayName: string; bio: string; avatarUrl: string; website: string; location: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    match Guid.TryParse(claims.UserId) with
                    | false, _ -> return Results.Unauthorized()
                    | true, userId ->
                        let opt (s: string) = if String.IsNullOrWhiteSpace(s) then None else Some s
                        use conn = Database.openConnection()
                        let p = UserProfileRepository.upsertProfile conn userId (opt body.displayName) (opt body.bio) (opt body.avatarUrl) (opt body.website) (opt body.location)
                        return Results.Ok(p)
            } |> Async.StartAsTask)
    ) |> ignore

    // ── Admin: AI Personas ────────────────────────────────────────────────────

    app.MapGet(
        "/api/admin/personas",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                let personas = PersonaRepository.getPersonas()
                let withForums = personas |> List.map (fun p ->
                    {| persona = p; forumIds = PersonaRepository.getPersonaForums p.Id |})
                Results.Ok(withForums)
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapPost(
        "/api/admin/personas",
        Func<HttpContext, {| name: string; slug: string; systemPrompt: string; model: string; triggerMode: string; avatarUrl: string; forumIds: string[] |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    // Create a bot user account for the persona
                    let botEmail = sprintf "persona+%s@djehuti.internal" body.slug
                    let! botUserOpt = UserRepository.createUser botEmail None
                    match botUserOpt with
                    | None -> return Results.Problem(detail = "Failed to create bot user", statusCode = 500, title = "Error")
                    | Some botUser ->
                        // Mark as bot and set display name
                        use conn = Database.openConnection()
                        use flagCmd = new Npgsql.NpgsqlCommand(
                            "UPDATE users SET is_bot = true, display_name = @dn, status = 'active' WHERE id = @id", conn)
                        flagCmd.Parameters.AddWithValue("id", botUser.Id) |> ignore
                        flagCmd.Parameters.AddWithValue("dn", body.name)  |> ignore
                        flagCmd.ExecuteNonQuery() |> ignore

                        let av = if String.IsNullOrWhiteSpace(body.avatarUrl) then None else Some body.avatarUrl
                        match PersonaRepository.createPersona body.name body.slug body.systemPrompt body.model body.triggerMode av botUser.Id with
                        | None -> return Results.Problem(detail = "Failed to create persona", statusCode = 500, title = "Error")
                        | Some persona ->
                            let forumIds = body.forumIds |> Array.choose (fun s -> match Guid.TryParse(s) with | true, g -> Some g | _ -> None) |> Array.toList
                            PersonaRepository.setPersonaForums persona.Id forumIds
                            return Results.Created($"/api/admin/personas/{persona.Id}", persona)
                | Some _ -> return Results.Forbid()
                | None   -> return Results.Unauthorized()
            } |> Async.StartAsTask)
    ) |> ignore

    app.MapPut(
        "/api/admin/personas/{id}",
        Func<Guid, HttpContext, {| name: string; systemPrompt: string; model: string; triggerMode: string; active: bool; avatarUrl: string; forumIds: string[] |}, IResult>(fun id ctx body ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                let av = if String.IsNullOrWhiteSpace(body.avatarUrl) then None else Some body.avatarUrl
                match PersonaRepository.updatePersona id body.name body.systemPrompt body.model body.triggerMode body.active av with
                | None -> Results.NotFound()
                | Some persona ->
                    let forumIds = body.forumIds |> Array.choose (fun s -> match Guid.TryParse(s) with | true, g -> Some g | _ -> None) |> Array.toList
                    PersonaRepository.setPersonaForums persona.Id forumIds
                    Results.Ok(persona)
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapDelete(
        "/api/admin/personas/{id}",
        Func<Guid, HttpContext, IResult>(fun id ctx ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                let ok = PersonaRepository.deletePersona id
                if ok then Results.Ok() else Results.NotFound()
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    // ── Admin: Heartbeat ──────────────────────────────────────────────────────

    app.MapGet(
        "/api/admin/heartbeat/config",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                Results.Ok(PersonaRepository.getConfig())
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapPatch(
        "/api/admin/heartbeat/config",
        Func<HttpContext, Map<string, string>, IResult>(fun ctx body ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                for kvp in body do
                    PersonaRepository.setConfig kvp.Key kvp.Value
                Results.Ok(PersonaRepository.getConfig())
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapGet(
        "/api/admin/heartbeat/jobs",
        Func<HttpContext, int, IResult>(fun ctx limit ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                let l = if limit < 1 || limit > 200 then 50 else limit
                Results.Ok(PersonaRepository.getRecentJobs l)
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapPost(
        "/api/admin/heartbeat/trigger",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                // Enqueue a no-op sentinel job to confirm queue is alive
                match PersonaRepository.enqueueJob "Ping" """{"message":"manual trigger"}""" with
                | Some job -> Results.Ok({| jobId = job.Id; status = "queued" |})
                | None     -> Results.Problem(detail = "Failed to enqueue", statusCode = 500, title = "Error")
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    // ── Patreon: Link Account (Manual Member ID) ─────────────────────────────

    app.MapGet(
        "/api/patreon/tiers",
        Func<IResult>(fun () -> Results.Ok(PatreonService.getAllTiers()))
    ) |> ignore

    app.MapGet(
        "/api/patreon/supporters",
        Func<IResult>(fun () -> Results.Ok(PatreonService.getSupporters()))
    ) |> ignore

    app.MapGet(
        "/api/patreon/user-tiers",
        Func<HttpContext, IResult>(fun ctx ->
            let ids = ctx.Request.Query["ids"].ToString().Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                      |> Array.choose (fun s -> match Guid.TryParse(s) with | true, g -> Some g | _ -> None)
                      |> Array.toList
            Results.Ok(PatreonService.getUserTiers ids))
    ) |> ignore

    app.MapPost(
        "/api/users/patreon/link",
        Func<HttpContext, {| memberId: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            task {
                match tryGetAuthClaims ctx with
                | None -> return Results.Unauthorized()
                | Some claims ->
                    let userId = System.Guid.Parse(claims.UserId)
                    if System.String.IsNullOrWhiteSpace(body.memberId) then
                        return Results.BadRequest("Member ID required")
                    else
                        try
                            use conn = Database.openConnection()
                            use cmd = new Npgsql.NpgsqlCommand("""
                                UPDATE users SET patreon_uuid = @uuid
                                WHERE id = @userId
                            """, conn)
                            cmd.Parameters.AddWithValue("uuid", body.memberId) |> ignore
                            cmd.Parameters.AddWithValue("userId", userId) |> ignore
                            let rowsAffected = cmd.ExecuteNonQuery()
                            if rowsAffected > 0 then
                                return Results.Ok({| status = "linked"; memberId = body.memberId |})
                            else
                                return Results.NotFound()
                        with ex ->
                            return Results.Problem(detail = ex.Message, statusCode = 400, title = "Link failed")
            }
        )
    ) |> ignore

    app.MapGet(
        "/api/users/patreon/status",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | None -> Results.Unauthorized()
            | Some claims ->
                let userId = System.Guid.Parse(claims.UserId)
                match Djehuti.Api.PatreonService.getTierLimits userId with
                | None -> Results.NotFound()
                | Some tier ->
                    let capacity = Djehuti.Api.PatreonService.getRemainingCapacity userId |> Option.defaultValue 0
                    Results.Ok({| tierName = tier.tierName; maxTasks = tier.maxConcurrentTasks; remainingCapacity = capacity |})
        )
    ) |> ignore

    // ── Patreon: Webhook Handler ──────────────────────────────────────────────

    app.MapPost(
        "/api/webhooks/patreon",
        Func<HttpContext, System.Threading.Tasks.Task<IResult>>(fun ctx ->
            task {
                use memStream = new System.IO.MemoryStream()
                do! ctx.Request.Body.CopyToAsync(memStream)
                let body : byte array = memStream.ToArray()
                let bodyStr = System.Text.Encoding.UTF8.GetString(body)

                match ctx.Request.Headers.TryGetValue("X-Patreon-Signature") with
                | false, _ -> return Results.Unauthorized()
                | true, headerSigs ->
                    let headerSig = headerSigs |> Seq.tryHead |> Option.defaultValue ""

                    // Verify HMAC-MD5 signature
                    let webhookSecret = System.Environment.GetEnvironmentVariable("PATREON_WEBHOOK_SECRET") |> Option.ofObj |> Option.defaultValue ""
                    if System.String.IsNullOrWhiteSpace(webhookSecret) then
                        return Results.Problem(detail = "Webhook secret not configured", statusCode = 500, title = "Error")
                    else
                        use hmac = new System.Security.Cryptography.HMACMD5(System.Text.Encoding.UTF8.GetBytes(webhookSecret))
                        let hashBytes : byte array = hmac.ComputeHash(body)
                        let computedSig : string = System.Convert.ToHexString(hashBytes).ToLower()

                        if not (compareStringsConstantTime computedSig headerSig) then
                            return Results.Unauthorized()
                        else
                            try
                                let json : System.Text.Json.JsonDocument = System.Text.Json.JsonDocument.Parse(bodyStr)
                                let root = json.RootElement

                                let eventType : string =
                                    match root.TryGetProperty("type") with
                                    | true, prop -> prop.GetString()
                                    | false, _ -> ""

                                let data =
                                    match root.TryGetProperty("data") with
                                    | true, prop -> Some prop
                                    | false, _ -> None

                                match data with
                                | None -> return Results.BadRequest("Missing data field in webhook payload")
                                | Some data ->
                                    let patreonUuid : string =
                                        match data.TryGetProperty("id") with
                                        | true, prop -> prop.GetString()
                                        | false, _ -> ""

                                    if System.String.IsNullOrWhiteSpace(patreonUuid) then
                                        return Results.BadRequest("Missing member ID in webhook payload")
                                    else
                                        let tierId : string option =
                                            try
                                                match data.TryGetProperty("relationships") with
                                                | false, _ -> None
                                                | true, rels ->
                                                    match rels.TryGetProperty("currently_entitled_tiers") with
                                                    | false, _ -> None
                                                    | true, tiers ->
                                                        match tiers.TryGetProperty("data") with
                                                        | false, _ -> None
                                                        | true, tierData ->
                                                            if tierData.ValueKind = System.Text.Json.JsonValueKind.Array && tierData.GetArrayLength() > 0 then
                                                                Some (tierData.[0].GetProperty("id").GetString())
                                                            else
                                                                None
                                            with _ -> None

                                        // Translate Patreon's numeric tier ID to our internal slug
                                        let internalTierId : string option =
                                            match tierId with
                                            | None -> None
                                            | Some patreonTierId ->
                                                use lookupConn = Database.openConnection()
                                                use lookupCmd = new Npgsql.NpgsqlCommand(
                                                    "SELECT tier_id FROM patreon_tiers WHERE patreon_id = @pid", lookupConn)
                                                lookupCmd.Parameters.AddWithValue("pid", patreonTierId) |> ignore
                                                let result = lookupCmd.ExecuteScalar()
                                                if result <> null then Some (result :?> string) else None

                                        match eventType with
                                        | "members:pledge:create" | "members:pledge:update" ->
                                            use conn = Database.openConnection()
                                            use cmd = new Npgsql.NpgsqlCommand("""
                                                UPDATE users SET patreon_uuid = @uuid, patreon_tier_id = @tier
                                                WHERE patreon_uuid = @uuid
                                            """, conn)
                                            cmd.Parameters.AddWithValue("uuid", patreonUuid) |> ignore
                                            cmd.Parameters.AddWithValue("tier", (match internalTierId with Some t -> t :> obj | None -> System.DBNull.Value :> obj)) |> ignore
                                            cmd.ExecuteNonQuery() |> ignore
                                            return Results.Ok({| status = "updated" |})

                                        | "members:pledge:delete" ->
                                            use conn = Database.openConnection()
                                            use cmd = new Npgsql.NpgsqlCommand("""
                                                UPDATE users SET patreon_tier_id = NULL
                                                WHERE patreon_uuid = @uuid
                                            """, conn)
                                            cmd.Parameters.AddWithValue("uuid", patreonUuid) |> ignore
                                            cmd.ExecuteNonQuery() |> ignore
                                            return Results.Ok({| status = "deleted" |})

                                        | _ -> return Results.Ok({| status = "received", eventType = eventType |})
                            with ex ->
                                return Results.Problem(detail = $"Exception: {ex.Message}", statusCode = 400, title = "Invalid payload")
            }
        )
    ) |> ignore

    // ── Admin: Site Metrics ──────────────────────────────────────────────────

    // ── Anonymous page-view beacon (called by React app on every page load) ────
    app.MapPost(
        "/api/track/pageview",
        Func<HttpContext, IResult>(fun ctx ->
            try
                let isAnon = not (ctx.Request.Headers.ContainsKey("Authorization"))
                // Use X-Real-IP from nginx; fall back to socket address
                let ip =
                    match ctx.Request.Headers.TryGetValue("X-Real-IP") with
                    | true, v when v.Count > 0 -> v.[0]
                    | _ -> ctx.Connection.RemoteIpAddress |> Option.ofObj |> Option.map (fun a -> a.ToString()) |> Option.defaultValue "unknown"
                let ipBytes  = System.Text.Encoding.UTF8.GetBytes(ip)
                let ipHash   = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ipBytes)).ToLowerInvariant()
                let path     = ctx.Request.Query.["path"].ToString()
                let referrer = ctx.Request.Query.["ref"].ToString()
                if isAnon then
                    MetricsRepository.recordPageView ipHash ip path referrer
                Results.Ok()
            with _ -> Results.Ok()))  // always 200 — never fail a page load for analytics
    |> ignore

    app.MapGet(
        "/api/stats",
        Func<IResult>(fun () ->
            try
                let m = MetricsRepository.getSiteMetrics ()
                Results.Ok({| Members = m.Totals.All.Users; Posts = m.Totals.All.Posts; Threads = m.Totals.All.Threads; Badges = m.Totals.All.Achievements |})
            with ex -> Results.Problem(detail = ex.Message, statusCode = 500, title = "Stats error"))) |> ignore

    app.MapGet(
        "/api/admin/metrics",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                try Results.Ok(MetricsRepository.getSiteMetrics ())
                with ex -> Results.Problem(detail = ex.Message, statusCode = 500, title = "Metrics error")
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapGet(
        "/api/admin/metrics/timeseries/{metric}",
        Func<HttpContext, string, IResult>(fun ctx metric ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                try Results.Ok(MetricsRepository.getMetricTimeSeries metric)
                with ex -> Results.Problem(detail = ex.Message, statusCode = 500, title = "Timeseries error")
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapGet(
        "/api/admin/metrics/user/{id}",
        Func<HttpContext, Guid, IResult>(fun ctx userId ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                match MetricsRepository.getUserDrilldown userId with
                | Some data -> Results.Ok(data)
                | None      -> Results.NotFound()
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapGet(
        "/api/admin/metrics/anonymous",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                try Results.Ok(MetricsRepository.getAnonymousMetrics ())
                with ex ->
                    eprintfn "[AnonMetrics] EXCEPTION: %s\n%s" ex.Message ex.StackTrace
                    Results.Problem(detail = ex.Message, statusCode = 500, title = "Anonymous metrics error")
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    app.MapPost(
        "/api/admin/metrics/anonymous/refresh-logs",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                try
                    // Run in background so request returns immediately
                    System.Threading.Tasks.Task.Run(fun () -> NginxLogParser.runRefresh() |> ignore) |> ignore
                    Results.Ok({| message = "Log refresh started — check metrics in ~60 seconds" |})
                with ex -> Results.Problem(detail = ex.Message, statusCode = 500, title = "Log refresh error")
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    // ── Public API (X-Api-Key) ──────────────────────────────────────────────────

    let requireApiKey (ctx: HttpContext) =
        match ctx.Request.Headers.TryGetValue("X-Api-Key") with
        | true, v when v.Count > 0 -> ApiKeyRepository.validateKey v.[0]
        | _ -> None

    // Admin: list my API keys
    app.MapGet(
        "/api/admin/api-keys",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                match Guid.TryParse(claims.UserId) with
                | false, _ -> Results.Problem(detail = "Invalid user id", statusCode = 500, title = "Auth error")
                | true, ownerId ->
                Results.Ok(ApiKeyRepository.listKeys ownerId)
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    // Admin: generate new key
    app.MapPost(
        "/api/admin/api-keys",
        Func<HttpContext, IResult>(fun ctx ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                match Guid.TryParse(claims.UserId) with
                | false, _ -> Results.Problem(detail = "Invalid user id", statusCode = 500, title = "Auth error")
                | true, ownerId ->
                let name =
                    match ctx.Request.Query.TryGetValue("name") with
                    | true, v when v.Count > 0 && not (String.IsNullOrWhiteSpace v.[0]) -> v.[0]
                    | _ -> "API Key"
                try
                    let (plaintext, record) = ApiKeyRepository.generateKey name ownerId
                    Results.Ok({| key = plaintext; record = record |})
                with ex -> Results.Problem(detail = ex.Message, statusCode = 500, title = "Key generation failed")
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    // Admin: revoke a key
    app.MapDelete(
        "/api/admin/api-keys/{id}",
        Func<HttpContext, string, IResult>(fun ctx id ->
            match tryGetAuthClaims ctx with
            | Some claims when Permissions.isAdmin claims.Role ->
                match Guid.TryParse(claims.UserId), Guid.TryParse(id) with
                | (true, ownerId), (true, keyId) ->
                    if ApiKeyRepository.revokeKey keyId ownerId then Results.NoContent()
                    else Results.NotFound("Key not found")
                | _ -> Results.BadRequest("Invalid key id")
            | Some _ -> Results.Forbid()
            | None   -> Results.Unauthorized())
    ) |> ignore

    // Public: list datasets
    app.MapGet(
        "/api/public/v1/datasets",
        Func<HttpContext, IResult>(fun ctx ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some _ -> Results.Ok(DataLibrary.catalog ()))
    ) |> ignore

    // Public: get dataset by id
    app.MapGet(
        "/api/public/v1/datasets/{id}",
        Func<HttpContext, string, IResult>(fun ctx id ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some _ ->
                match DataLibrary.tryReadDataSet id with
                | Ok json   -> Results.Text(json, "application/json")
                | Error msg -> Results.NotFound(msg))
    ) |> ignore

    // Public: analyze a stored dataset (no AI)
    app.MapPost(
        "/api/public/v1/datasets/{id}/analyze",
        Func<HttpContext, string, IResult>(fun ctx id ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some _ ->
                match DataLibrary.tryReadDataSet id with
                | Error msg -> Results.NotFound(msg)
                | Ok json ->
                    try Results.Ok(Dto.analyze json)
                    with
                    | :? ArgumentException as ex -> Results.BadRequest(ex.Message)
                    | ex -> Results.Problem(detail = ex.Message, statusCode = 500, title = "Analysis failed"))
    ) |> ignore

    // ── Public Community API (X-Api-Key) ────────────────────────────────────────

    // GET /api/public/v1/community/forums — all categories + forums
    app.MapGet(
        "/api/public/v1/community/forums",
        Func<HttpContext, IResult>(fun ctx ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some _ ->
                let cats = ForumRepository.getCategories ()
                let result =
                    cats |> List.map (fun c ->
                        {| category = c
                           forums = ForumRepository.getForumsByCategory c.Id |})
                Results.Ok(result))
    ) |> ignore

    // GET /api/public/v1/community/threads?forumId=&search=&page=&pageSize=
    app.MapGet(
        "/api/public/v1/community/threads",
        Func<HttpContext, IResult>(fun ctx ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some _ ->
                let q = ctx.Request.Query
                let forumId =
                    match q.TryGetValue("forumId") with
                    | true, v when v.Count > 0 ->
                        match Guid.TryParse(v.[0]) with true, g -> Some g | _ -> None
                    | _ -> None
                let search =
                    match q.TryGetValue("search") with
                    | true, v when v.Count > 0 && not (String.IsNullOrWhiteSpace v.[0]) -> Some v.[0]
                    | _ -> None
                let page     = match q.TryGetValue("page")     with true, v when v.Count > 0 -> (try int v.[0] with _ -> 1)  | _ -> 1
                let pageSize = match q.TryGetValue("pageSize") with true, v when v.Count > 0 -> (try int v.[0] with _ -> 20) | _ -> 20
                Results.Ok(ForumRepository.searchThreads forumId search page (min pageSize 100)))
    ) |> ignore

    // GET /api/public/v1/community/threads/{id}
    app.MapGet(
        "/api/public/v1/community/threads/{id}",
        Func<HttpContext, string, IResult>(fun ctx id ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some _ ->
                match Guid.TryParse(id) with
                | false, _ -> Results.BadRequest("Invalid thread id")
                | true, tid ->
                    match ForumRepository.getThreadById tid with
                    | None -> Results.NotFound("Thread not found")
                    | Some t -> Results.Ok(t))
    ) |> ignore

    // GET /api/public/v1/community/threads/{id}/posts?page=&pageSize=
    app.MapGet(
        "/api/public/v1/community/threads/{id}/posts",
        Func<HttpContext, string, IResult>(fun ctx id ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some _ ->
                match Guid.TryParse(id) with
                | false, _ -> Results.BadRequest("Invalid thread id")
                | true, tid ->
                    let q        = ctx.Request.Query
                    let page     = match q.TryGetValue("page")     with true, v when v.Count > 0 -> (try int v.[0] with _ -> 1)  | _ -> 1
                    let pageSize = match q.TryGetValue("pageSize") with true, v when v.Count > 0 -> (try int v.[0] with _ -> 50) | _ -> 50
                    Results.Ok(ForumRepository.getPostsByThread tid page (min pageSize 200)))
    ) |> ignore

    // POST /api/public/v1/community/threads — create thread as key owner
    app.MapPost(
        "/api/public/v1/community/threads",
        Func<HttpContext, IResult>(fun ctx ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some ownerId ->
                let body =
                    try
                        use sr = new System.IO.StreamReader(ctx.Request.Body)
                        let json = sr.ReadToEnd()
                        let doc  = System.Text.Json.JsonDocument.Parse(json)
                        let get (k: string) =
                            let mutable el = Unchecked.defaultof<System.Text.Json.JsonElement>
                            if doc.RootElement.TryGetProperty(k, &el) then el.GetString() else null
                        Some (get "forumId", get "title", get "content")
                    with _ -> None
                match body with
                | None -> Results.BadRequest("Could not parse request body")
                | Some (forumIdStr, title, content) ->
                    if String.IsNullOrWhiteSpace title   then Results.BadRequest("title is required")
                    elif String.IsNullOrWhiteSpace content then Results.BadRequest("content is required")
                    else
                        match Guid.TryParse(forumIdStr) with
                        | false, _ -> Results.BadRequest("forumId is required and must be a valid GUID")
                        | true, forumId ->
                            match ForumRepository.createThread forumId ownerId title content with
                            | None    -> Results.Problem(detail = "Forum not found or thread creation failed", statusCode = 500, title = "Create failed")
                            | Some t  -> Results.Ok(t))
    ) |> ignore

    // POST /api/public/v1/community/threads/{id}/posts — reply as key owner
    app.MapPost(
        "/api/public/v1/community/threads/{id}/posts",
        Func<HttpContext, string, IResult>(fun ctx id ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some ownerId ->
                match Guid.TryParse(id) with
                | false, _ -> Results.BadRequest("Invalid thread id")
                | true, tid ->
                    let content =
                        try
                            use sr = new System.IO.StreamReader(ctx.Request.Body)
                            let json = System.Text.Json.JsonDocument.Parse(sr.ReadToEnd())
                            let mutable el = Unchecked.defaultof<System.Text.Json.JsonElement>
                            if json.RootElement.TryGetProperty("content", &el) then el.GetString() else null
                        with _ -> null
                    if String.IsNullOrWhiteSpace content then Results.BadRequest("content is required")
                    else
                        match ForumRepository.createPost tid ownerId content with
                        | None -> Results.Problem(detail = "Thread not found or post creation failed", statusCode = 500, title = "Create failed")
                        | Some p -> Results.Ok(p))
    ) |> ignore

    // GET /api/public/v1/community/sections — blog sections
    app.MapGet(
        "/api/public/v1/community/sections",
        Func<HttpContext, IResult>(fun ctx ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some _ -> Results.Ok(BlogRepository.getSections ()))
    ) |> ignore

    // GET /api/public/v1/community/articles?search=&page=&pageSize=
    app.MapGet(
        "/api/public/v1/community/articles",
        Func<HttpContext, IResult>(fun ctx ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some _ ->
                let q = ctx.Request.Query
                let search =
                    match q.TryGetValue("search") with
                    | true, v when v.Count > 0 && not (String.IsNullOrWhiteSpace v.[0]) -> Some v.[0]
                    | _ -> None
                let page     = match q.TryGetValue("page")     with true, v when v.Count > 0 -> (try int v.[0] with _ -> 1)  | _ -> 1
                let pageSize = match q.TryGetValue("pageSize") with true, v when v.Count > 0 -> (try int v.[0] with _ -> 20) | _ -> 20
                Results.Ok(BlogRepository.getPublishedArticles None search None page (min pageSize 100)))
    ) |> ignore

    // GET /api/public/v1/community/articles/{id}
    app.MapGet(
        "/api/public/v1/community/articles/{id}",
        Func<HttpContext, string, IResult>(fun ctx id ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some _ ->
                match BlogRepository.getArticleById (Guid.Parse id) with
                | None -> Results.NotFound("Article not found")
                | Some a -> Results.Ok(a))
    ) |> ignore

    // POST /api/public/v1/community/articles — create + publish as key owner
    app.MapPost(
        "/api/public/v1/community/articles",
        Func<HttpContext, IResult>(fun ctx ->
            match requireApiKey ctx with
            | None -> Results.Unauthorized()
            | Some ownerId ->
                try
                    use sr = new System.IO.StreamReader(ctx.Request.Body)
                    let doc = System.Text.Json.JsonDocument.Parse(sr.ReadToEnd())
                    let get (k: string) =
                        let mutable el = Unchecked.defaultof<System.Text.Json.JsonElement>
                        if doc.RootElement.TryGetProperty(k, &el) then el.GetString() else null
                    let title    = get "title"
                    let content  = get "content"
                    let subtitle = get "subtitle" |> Option.ofObj
                    let excerpt  = get "excerpt"  |> Option.ofObj
                    let sectionIdStr = get "sectionId"
                    if String.IsNullOrWhiteSpace title   then Results.BadRequest("title is required")
                    elif String.IsNullOrWhiteSpace content then Results.BadRequest("content is required")
                    else
                        let sectionId =
                            match Guid.TryParse(sectionIdStr) with
                            | true, g -> g
                            | _       -> BlogRepository.getOrCreateDefaultSection().Id
                        match BlogRepository.createArticle sectionId ownerId title subtitle content None excerpt "public" with
                        | None -> Results.Problem(detail = "Article creation failed", statusCode = 500, title = "Create failed")
                        | Some draft ->
                            // publish immediately — bypass moderation queue
                            match BlogRepository.setArticleStatus draft.Id "published" with
                            | None -> Results.Ok(draft)
                            | Some published -> Results.Ok(published)
                with ex -> Results.Problem(detail = ex.Message, statusCode = 500, title = "Create failed"))
    ) |> ignore

    app.Run()
    0
