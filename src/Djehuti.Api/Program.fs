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
                                let _verifyToken = Auth.generateSecureToken ()
                                // TODO: Store verification token in DB and send email
                                return Results.Ok({ Id = user.Id.ToString(); Email = user.Email; DisplayName = user.DisplayName; AvatarUrl = user.AvatarUrl; Bio = user.Bio; Pronouns = user.Pronouns; Location = user.Location; Role = user.Role; Status = user.Status; CreatedAt = user.CreatedAt })
                            | None ->
                                return Results.Problem(detail = "Failed to create user", statusCode = 500, title = "Registration failed")
            } |> Async.StartAsTask)
    )
    |> ignore

    app.MapPost(
        "/api/auth/login",
        Func<LoginRequest, System.Threading.Tasks.Task<IResult>>(fun request ->
            async {
                if String.IsNullOrWhiteSpace request.Email || String.IsNullOrWhiteSpace request.Password then
                    return Results.BadRequest("email and password are required")
                else
                    let! user = UserRepository.tryGetByEmail request.Email
                    match user with
                    | Some u when u.PasswordHash.IsSome && Auth.verifyPassword request.Password u.PasswordHash.Value ->
                        if u.Status <> "active" then
                            return Results.Problem(detail = $"Account is {u.Status}", statusCode = 403, title = "Login failed")
                        else
                            let _token = Auth.generateToken {
                                UserId = u.Id.ToString()
                                Email = u.Email
                                DisplayName = u.DisplayName
                                Role = u.Role
                                IssuedAt = DateTime.UtcNow
                                ExpiresAt = DateTime.UtcNow.AddHours(1.0)
                            }
                            // TODO: Set HttpOnly cookie with JWT
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

    app.Run()
    0
