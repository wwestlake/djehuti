module Djehuti.Core.Tests

open System
open Djehuti.Core
open Xunit

[<Fact>]
let ``turn creation records strategy and contamination depth`` () =
    let prompt = Domain.prompt "p1" "Explain entropy in plain language."
    let response = Domain.response "r1" "Entropy measures how spread out possibilities are."

    let turn =
        Domain.turn
            "t1"
            "s1"
            3
            prompt
            response
            DateTimeOffset.UnixEpoch
            InterleavedWithHistory
            2

    Assert.Equal(InterleavedWithHistory, turn.Strategy)
    Assert.Equal(Contaminated 2, turn.ContaminationDepth)

[<Fact>]
let ``decomposition exposes words sentences lines and frequencies`` () =
    let parts = Decompose.parts "Hello world. Hello again.\nSecond line."

    Assert.Equal(6, parts.Words.Length)
    Assert.Equal(3, parts.Sentences.Length)
    Assert.Equal(2, parts.Lines.Length)
    Assert.Equal(2, parts.WordFrequencies["hello"])

[<Fact>]
let ``comparison metrics compare typed prompt response values`` () =
    let prompt = Domain.prompt "p1" "alpha beta beta"
    let response = Domain.response "r1" "alpha gamma"
    let comparison = PromptToResponse(prompt, response)

    let metrics = TextMetrics.comparisonMetrics "comparison-1" comparison

    Assert.Equal(PromptResponseComparison, metrics.Kind)
    Assert.Equal(3, metrics.Left.WordCount)
    Assert.Equal(2, metrics.Right.WordCount)
    Assert.Equal(-1, metrics.WordCountDelta)
    Assert.Equal(1, metrics.SharedWordCount)
    Assert.InRange(metrics.JaccardSimilarity, 0.333, 0.334)
    Assert.InRange(metrics.CosineSimilarity, 0.316, 0.317)

[<Fact>]
let ``corpus metrics aggregate prompt and response comparisons`` () =
    let promptA = Domain.prompt "p1" "summarize the lesson"
    let promptB = Domain.prompt "p2" "summarize the chapter"
    let responseA = Domain.response "r1" "the lesson concerns measurement"
    let responseB = Domain.response "r2" "the chapter concerns measurement"

    let comparisons =
        [ PromptToPrompt(promptA, promptB)
          PromptToResponse(promptA, responseA)
          ResponseToResponse(responseA, responseB) ]

    let corpus = TextMetrics.corpusMetrics comparisons

    Assert.Equal(3, corpus.ComparisonCount)
    Assert.Equal(1, corpus.PromptPromptCount)
    Assert.Equal(1, corpus.PromptResponseCount)
    Assert.Equal(1, corpus.ResponseResponseCount)
    Assert.Equal(3, corpus.MetricsByComparison.Length)
    Assert.True(corpus.AverageCosineSimilarity > 0.0)

[<Fact>]
let ``window feasibility refuses local measurement when token floor exceeds upper bound`` () =
    let estimate value methodName =
        { Value = value
          Method = ManualCalibration methodName
          SampleSize = 12
          Assumptions = [] }

    let calibration =
        { Id = CalibrationRecordId "c1"
          ContextClassId = ContextClassId "general"
          EstimatedAt = DateTimeOffset.UnixEpoch
          ValidFrom = DateTimeOffset.UnixEpoch
          ValidUntil = None
          NoiseFloorEpsilon = Some(estimate 0.01 "epsilon")
          LambdaMinEpsilon = Some(estimate 0.10 "lambda-min")
          LocalValidityRadiusDelta = Some(estimate 0.25 "delta")
          LambdaMaxDelta = Some(estimate 0.20 "lambda-max")
          TokenGranularityLambdaQuantum = Some(estimate 0.30 "lambda-quantum") }

    match Measurement.checkWindowFeasibility calibration with
    | Infeasible(lowerBound, upperBound, recommendation) ->
        Assert.Equal(0.30, lowerBound, 3)
        Assert.Equal(0.20, upperBound, 3)
        Assert.Contains("global calibration", recommendation)
    | result -> failwithf "Expected infeasible result, got %A" result

[<Fact>]
let ``effective mass is marked as marginal estimate from perturbation velocity`` () =
    let perturbationVelocity =
        Domain.measured
            0.25
            MarginalEstimate
            [ FromShockTrial(ShockTrialId "shock-1") ]
            [ ContinuityOfPerturbationVelocity ]

    let effectiveMass = Measurement.effectiveMassFromPerturbationVelocity perturbationVelocity

    Assert.Equal(4.0, effectiveMass.Value)
    Assert.Equal(MarginalEstimate, effectiveMass.Basis)
    Assert.Contains(ContinuityOfPerturbationVelocity, effectiveMass.Assumptions)

[<Fact>]
let ``dissipation check flags positive energy deltas`` () =
    let measured value =
        Domain.measured value GlobalCalibrationEstimate [] [ GlobalCalibrationFallback ]

    let state turnId total =
        { TurnId = TurnId turnId
          PotentialEnergy = measured 0.0
          EffectiveMassExpectation = measured 1.0
          Velocity = measured 1.0
          TotalEnergy = measured total }

    match Measurement.checkDissipation [ state "t1" 10.0; state "t2" 10.5 ] with
    | Violation(_, _, delta) -> Assert.Equal(0.5, delta, 3)
    | result -> failwithf "Expected violation result, got %A" result

[<Fact>]
let ``storage computation saves and loads turns through context`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)
    let prompt = Domain.prompt "p1" "First prompt"
    let response = Domain.response "r1" "First response"

    let turn =
        Domain.turn
            "t1"
            "s1"
            0
            prompt
            response
            DateTimeOffset.UnixEpoch
            Natural
            0

    let operation =
        storage {
            do! StorageOps.saveTurn turn
            return! StorageOps.getTurn turn.Id
        }

    let result =
        Storage.run context operation
        |> Async.RunSynchronously

    match result with
    | Ok(Some loaded) -> Assert.Equal(turn.Id, loaded.Id)
    | other -> failwithf "Expected saved turn, got %A" other

[<Fact>]
let ``in memory turn store lists session turns by sequence index`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)
    let prompt = Domain.prompt "p1" "Prompt"
    let response = Domain.response "r1" "Response"

    let turn sequenceIndex =
        Domain.turn
            $"t{sequenceIndex}"
            "s1"
            sequenceIndex
            prompt
            response
            DateTimeOffset.UnixEpoch
            Natural
            0

    let operation =
        storage {
            do! StorageOps.saveTurn (turn 2)
            do! StorageOps.saveTurn (turn 0)
            do! StorageOps.saveTurn (turn 1)
            return! StorageOps.listTurnsForSession (SessionId "s1")
        }

    let result =
        Storage.run context operation
        |> Async.RunSynchronously

    match result with
    | Ok turns ->
        let indexes = turns |> List.map _.SequenceIndex
        Assert.Equal<int list>([ 0; 1; 2 ], indexes)
    | Error error -> failwithf "Expected ordered turns, got %A" error

[<Fact>]
let ``latest calibration lookup returns newest record for context class`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)

    let record id day =
        { Id = CalibrationRecordId id
          ContextClassId = ContextClassId "general"
          EstimatedAt = DateTimeOffset.UnixEpoch.AddDays(day)
          ValidFrom = DateTimeOffset.UnixEpoch.AddDays(day)
          ValidUntil = None
          NoiseFloorEpsilon = None
          LambdaMinEpsilon = None
          LocalValidityRadiusDelta = None
          LambdaMaxDelta = None
          TokenGranularityLambdaQuantum = None }

    let operation =
        storage {
            do! StorageOps.saveCalibrationRecord (record "old" 1.0)
            do! StorageOps.saveCalibrationRecord (record "new" 2.0)
            return! StorageOps.latestCalibrationForContextClass (ContextClassId "general")
        }

    let result =
        Storage.run context operation
        |> Async.RunSynchronously

    match result with
    | Ok(Some latest) -> Assert.Equal(CalibrationRecordId "new", latest.Id)
    | other -> failwithf "Expected latest calibration, got %A" other

[<Fact>]
let ``storage computation short circuits after error`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)
    let prompt = Domain.prompt "p1" "Prompt"
    let response = Domain.response "r1" "Response"

    let turn =
        Domain.turn
            "t1"
            "s1"
            0
            prompt
            response
            DateTimeOffset.UnixEpoch
            Natural
            0

    let operation =
        storage {
            do! Storage.error (ValidationFailure "stop")
            do! StorageOps.saveTurn turn
            return! StorageOps.getTurn turn.Id
        }

    let result =
        Storage.run context operation
        |> Async.RunSynchronously

    let loaded =
        Storage.run context (StorageOps.getTurn turn.Id)
        |> Async.RunSynchronously

    Assert.Equal(Error(ValidationFailure "stop"), result)
    Assert.Equal(Ok None, loaded)

let private testSource =
    { Id = DataSourceId "source-1"
      Kind = ReplayFile
      Name = "test replay"
      Metadata = Map.empty }

let private interaction sessionId index =
    { TurnId = TurnId $"turn-{index}"
      SessionId = SessionId sessionId
      ModelId = ModelId "model-1"
      Clock =
        { SequenceIndex = LogicalTime index
          ObservedAt = None }
      Prompt = Domain.prompt $"prompt-{index}" $"Prompt {index}"
      Response = Domain.response $"response-{index}" $"Response {index}"
      Strategy = Natural
      PriorShockCount = 0
      Source = testSource
      Metadata = Map.empty }

[<Fact>]
let ``ingestion accepts ordered interaction records using integer logical time`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)

    let operation =
        Ingestion.ingestEvents
            [ InteractionObserved(interaction "s-ingest" 0)
              InteractionObserved(interaction "s-ingest" 1) ]

    let result =
        Storage.run context operation
        |> Async.RunSynchronously

    let storedTurns =
        Storage.run context (StorageOps.listTurnsForSession (SessionId "s-ingest"))
        |> Async.RunSynchronously

    match result, storedTurns with
    | Ok summary, Ok turns ->
        Assert.Equal(2, summary.InteractionsObserved)
        Assert.Empty(summary.Gaps)
        Assert.Equal<int list>([ 0; 1 ], turns |> List.map _.SequenceIndex)
    | other -> failwithf "Expected successful ingestion, got %A" other

[<Fact>]
let ``ingestion preserves source identity as turn metadata`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)
    let record = interaction "s-source" 0

    let result =
        Storage.run context (Ingestion.ingestEvent (InteractionObserved record))
        |> Async.RunSynchronously

    let loaded =
        Storage.run context (StorageOps.getTurn record.TurnId)
        |> Async.RunSynchronously

    match result, loaded with
    | Ok _, Ok(Some turn) ->
        Assert.Equal("source-1", turn.Metadata["data_source_id"])
        Assert.Equal("replay-file", turn.Metadata["data_source_kind"])
        Assert.Equal("0", turn.Metadata["logical_time"])
    | other -> failwithf "Expected source metadata, got %A" other

[<Fact>]
let ``ingestion reports gaps without rejecting later logical time`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)

    let operation =
        Ingestion.ingestEvents
            [ InteractionObserved(interaction "s-gap" 0)
              InteractionObserved(interaction "s-gap" 3) ]

    let result =
        Storage.run context operation
        |> Async.RunSynchronously

    match result with
    | Ok summary ->
        Assert.Equal(2, summary.InteractionsObserved)
        let gap = Assert.Single(summary.Gaps)
        Assert.Equal(0, gap.PreviousSequenceIndex)
        Assert.Equal(3, gap.CurrentSequenceIndex)
    | Error error -> failwithf "Expected gap summary, got %A" error

[<Fact>]
let ``ingestion rejects duplicate logical time in a session`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)

    let operation =
        Ingestion.ingestEvents
            [ InteractionObserved(interaction "s-duplicate" 0)
              InteractionObserved({ interaction "s-duplicate" 0 with TurnId = TurnId "turn-duplicate" }) ]

    let result =
        Storage.run context operation
        |> Async.RunSynchronously

    match result with
    | Error(Conflict("Turn", _, reason)) -> Assert.Contains("logical time", reason)
    | other -> failwithf "Expected duplicate conflict, got %A" other

[<Fact>]
let ``ingestion rejects out of order logical time`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)

    let operation =
        Ingestion.ingestEvents
            [ InteractionObserved(interaction "s-out-of-order" 2)
              InteractionObserved(interaction "s-out-of-order" 1) ]

    let result =
        Storage.run context operation
        |> Async.RunSynchronously

    match result with
    | Error(ValidationFailure message) -> Assert.Contains("earlier than existing sequence", message)
    | other -> failwithf "Expected ordering validation failure, got %A" other
