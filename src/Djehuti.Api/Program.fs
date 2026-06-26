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

    let app = builder.Build()

    Database.runMigrations ()

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
        Func<RegisterRequest, System.Threading.Tasks.Task<IResult>>(fun request ->
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
        Func<HttpContext, {| name: string; description: string |}, System.Threading.Tasks.Task<IResult>>(fun ctx body ->
            async {
                match tryGetAuthClaims ctx with
                | Some claims when Permissions.isAdmin claims.Role ->
                    let desc = if String.IsNullOrWhiteSpace body.description then None else Some body.description
                    return match ForumRepository.createCategory body.name desc with
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
                        | Some _ ->
                            match Guid.TryParse(claims.UserId) with
                            | false, _ -> return Results.Unauthorized()
                            | true, userId ->
                                return match ForumRepository.createPost tid userId body.content with
                                       | Some p -> Results.Created($"/api/forum/posts/{p.Id}", p)
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
        Func<string, string, string, int, int, IResult>(fun sectionId search tag page pageSize ->
            let sid = match Guid.TryParse(sectionId) with | true, g -> Some g | _ -> None
            let q   = if String.IsNullOrWhiteSpace search then None else Some search
            let t   = if String.IsNullOrWhiteSpace tag    then None else Some tag
            let p   = if page < 1 then 1 else page
            let ps  = if pageSize < 1 || pageSize > 50 then 20 else pageSize
            Results.Ok(BlogRepository.getPublishedArticles sid q t p ps))
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

    app.Run()
    0
