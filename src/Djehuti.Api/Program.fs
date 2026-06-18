open System
open System.IO
open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Djehuti.Core

[<CLIMutable>]
type AnalyzeRequest =
    { DatasetJson: string }

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
type AnalyzeResponse =
    { Summary: SummaryDto
      Turns: TurnMetricDto list
      Velocities: VelocityPointDto list
      Constants: ConstantDto list
      Reports: MeasurementReportDto list
      Warnings: string list }

[<CLIMutable>]
type DataSetCatalogItem =
    { Id: string
      Name: string
      Description: string
      File: string
      SourceKind: string
      TurnCount: int
      DeclaredTurnCount: Nullable<int>
      Status: string }

module DataLibrary =
    let private serializerOptions =
        JsonSerializerOptions(PropertyNameCaseInsensitive = true)

    let private normalizePath (path: string) =
        Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))

    let private candidateRoots (contentRoot: string) =
        [ Path.Combine(contentRoot, "data", "datasets")
          Path.Combine(contentRoot, "..", "..", "data", "datasets")
          Path.Combine(contentRoot, "..", "..", "..", "data", "datasets")
          Path.Combine(AppContext.BaseDirectory, "data", "datasets")
          Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "datasets") ]
        |> List.map normalizePath
        |> List.distinct

    let private tryRoot contentRoot =
        candidateRoots contentRoot
        |> List.tryFind Directory.Exists

    let catalog contentRoot =
        match tryRoot contentRoot with
        | None -> []
        | Some root ->
            let manifestPath = Path.Combine(root, "manifest.json")
            if File.Exists manifestPath then
                File.ReadAllText manifestPath
                |> fun json -> JsonSerializer.Deserialize<DataSetCatalogItem array>(json, serializerOptions)
                |> Option.ofObj
                |> Option.map Array.toList
                |> Option.defaultValue []
            else
                []

    let tryReadDataSet contentRoot id =
        match tryRoot contentRoot with
        | None -> Error "Data library folder was not found."
        | Some root ->
            catalog contentRoot
            |> List.tryFind (fun item -> String.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
            |> function
                | None -> Error $"Dataset '{id}' was not found."
                | Some item ->
                    let rootPath = normalizePath root
                    let datasetPath = normalizePath (Path.Combine(rootPath, item.File))
                    if not (datasetPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)) then
                        Error "Dataset path is outside the data library."
                    elif not (File.Exists datasetPath) then
                        Error $"Dataset file '{item.File}' was not found."
                    else
                        Ok(File.ReadAllText datasetPath)

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

    let sourceIdFromMetadata (turn: Turn) =
        turn.Metadata
        |> Map.tryFind "data_source_id"
        |> Option.defaultValue ""

    let analyze (datasetJson: string) =
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
            let turns =
                dataSet.Interactions
                |> List.tryHead
                |> Option.map _.SessionId
                |> Option.map (fun sessionId ->
                    StorageOps.listTurnsForSession sessionId
                    |> Storage.run context
                    |> Async.RunSynchronously)
                |> Option.defaultValue (Ok [])

            match turns with
            | Error error -> failwith $"Turn load failed: {error}"
            | Ok turns ->
                let comparisons =
                    turns
                    |> List.map (fun turn -> PromptToResponse(turn.Prompt, turn.Response))

                let corpus = TextMetrics.corpusMetrics comparisons

                let velocities =
                    turns
                    |> List.pairwise
                    |> List.map (fun (previousTurn, currentTurn) ->
                        currentTurn.SequenceIndex, Measurement.velocityFromTurnPair CosineDistance previousTurn currentTurn)

                let velocityByIndex =
                    velocities
                    |> Map.ofList

                let observableVectors =
                    turns
                    |> List.mapi (fun index turn ->
                        let previousTurn =
                            if index = 0 then None else Some turns[index - 1]

                        Measurement.observableVectorFromTurn CosineDistance previousTurn turn)

                let reports =
                    observableVectors
                    |> List.map (fun vector ->
                        Measurement.reportObservableVector (unwrapTurnId vector.TurnId) vector
                        |> reportDto)

                let turnRows =
                    (turns, corpus.MetricsByComparison)
                    ||> List.zip
                    |> List.map (fun (turn, metric) ->
                        let velocity =
                            velocityByIndex
                            |> Map.tryFind turn.SequenceIndex
                            |> Option.map (fun value -> Nullable value.Value)
                            |> Option.defaultValue (Nullable())

                        { SequenceIndex = turn.SequenceIndex
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
                    |> List.map (fun (sequenceIndex, measured) ->
                        { SequenceIndex = sequenceIndex
                          Value = measured.Value
                          Basis = basisText measured.Basis })

                let constants =
                    dataSet.Constants
                    |> Map.toList
                    |> List.map (fun (key, value) -> constantDto key value)

                let sessionId =
                    turns
                    |> List.tryHead
                    |> Option.map (fun turn -> unwrapSessionId turn.SessionId)
                    |> Option.defaultValue ""

                let modelId =
                    turns
                    |> List.tryHead
                    |> Option.map (fun turn ->
                        dataSet.Interactions
                        |> List.tryFind (fun record -> record.TurnId = turn.Id)
                        |> Option.map (fun record -> unwrapModelId record.ModelId)
                        |> Option.defaultValue "")
                    |> Option.defaultValue ""

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
                  Warnings =
                    [ if ingestionSummary.Gaps.Length > 0 then
                        yield $"{ingestionSummary.Gaps.Length} logical-time gap(s) detected." ] }

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

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

    app.UseCors() |> ignore

    app.MapGet("/api/health", Func<string>(fun () -> "ok")) |> ignore

    app.MapGet(
        "/api/datasets",
        Func<IWebHostEnvironment, IResult>(fun environment ->
            DataLibrary.catalog environment.ContentRootPath
            |> Results.Ok)
    )
    |> ignore

    app.MapGet(
        "/api/datasets/{id}",
        Func<string, IWebHostEnvironment, IResult>(fun id environment ->
            match DataLibrary.tryReadDataSet environment.ContentRootPath id with
            | Ok json -> Results.Text(json, "application/json")
            | Error error -> Results.NotFound(error))
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
                with ex ->
                    Results.BadRequest(ex.Message))
    )
    |> ignore

    app.Run()
    0
