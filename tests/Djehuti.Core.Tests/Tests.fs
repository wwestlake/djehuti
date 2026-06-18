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
let ``calibration builder derives window bounds from observed calibration samples`` () =
    let calibration =
        Measurement.buildCalibrationRecord
            { Id = CalibrationRecordId "cal-built"
              ContextClassId = ContextClassId "general"
              EstimatedAt = DateTimeOffset.UnixEpoch
              ValidFrom = DateTimeOffset.UnixEpoch
              ValidUntil = None
              NullTrialDisplacements = [ 0.01; 0.02; 0.03 ]
              HomogeneityScan =
                [ { Lambda = 0.10; VelocityChange = 0.02; CurvatureChange = 0.02 }
                  { Lambda = 0.25; VelocityChange = 0.04; CurvatureChange = 0.03 }
                  { Lambda = 0.40; VelocityChange = 0.20; CurvatureChange = 0.03 } ]
              SingleTokenEditDisplacements = [ 0.06; 0.08; 0.10 ]
              TokenGranularityStatistic = Median
              LocalTolerance = 0.05 }

    Assert.Equal(Some 0.02, calibration.NoiseFloorEpsilon |> Option.map _.Value)
    Assert.Equal(Some 0.04, calibration.LambdaMinEpsilon |> Option.map _.Value)
    Assert.Equal(Some 0.25, calibration.LambdaMaxDelta |> Option.map _.Value)
    Assert.Equal(Some 0.08, calibration.TokenGranularityLambdaQuantum |> Option.map _.Value)

    match Measurement.decideLocalMeasurement calibration with
    | LocalMeasurementAllowed(LocalCalibrationEstimate(ContextClassId "general"), lowerBound, upperBound) ->
        Assert.Equal(0.08, lowerBound, 3)
        Assert.Equal(0.25, upperBound, 3)
    | other -> failwithf "Expected allowed local measurement, got %A" other

[<Fact>]
let ``local measurement decision refuses missing calibration`` () =
    let calibration =
        { Id = CalibrationRecordId "cal-missing"
          ContextClassId = ContextClassId "general"
          EstimatedAt = DateTimeOffset.UnixEpoch
          ValidFrom = DateTimeOffset.UnixEpoch
          ValidUntil = None
          NoiseFloorEpsilon = None
          LambdaMinEpsilon = None
          LocalValidityRadiusDelta = None
          LambdaMaxDelta = None
          TokenGranularityLambdaQuantum = None }

    match Measurement.decideLocalMeasurement calibration with
    | LocalMeasurementRefused(refusal, MissingCalibration missingFields) ->
        Assert.Contains("lambda_min(epsilon)", missingFields)
        match refusal.Basis with
        | Refused reason -> Assert.Contains("Window Inequality", reason)
        | other -> failwithf "Expected refused basis, got %A" other
    | other -> failwithf "Expected refused local measurement, got %A" other

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
let ``observable vector from turn preserves direct prompt response measurements`` () =
    let prompt = Domain.prompt "p-vector" "alpha beta beta"
    let response = Domain.response "r-vector" "alpha gamma"

    let turn =
        Domain.turn
            "t-vector"
            "s-vector"
            0
            prompt
            response
            DateTimeOffset.UnixEpoch
            Natural
            0

    let vector = Measurement.observableVectorFromTurn CosineDistance None turn

    Assert.Equal(turn.Id, vector.TurnId)

    match vector.Alpha, vector.Beta, vector.Gamma, vector.Delta, vector.Velocity with
    | Some alpha, Some beta, Some gamma, Some delta, None ->
        Assert.Equal(DirectObservation, alpha.Basis)
        Assert.InRange(alpha.Value, 0.316, 0.317)
        Assert.InRange(beta.Value, 0.333, 0.334)
        Assert.True(gamma.Value > 0.0)
        Assert.Equal(-1.0, delta.Value)
    | other -> failwithf "Expected alpha/beta/gamma/delta and no first-turn velocity, got %A" other

[<Fact>]
let ``energy computation refuses refused or invalid measurement inputs`` () =
    let measured value =
        Domain.measured value GlobalCalibrationEstimate [] [ GlobalCalibrationFallback ]

    let state =
        Measurement.computeEnergy
            (TurnId "t-energy-refused")
            (measured 1.0)
            (Domain.refused "No calibrated effective mass expectation is available.")
            (measured 0.25)

    match state.TotalEnergy.Basis with
    | Refused reason -> Assert.Contains("Energy cannot be computed", reason)
    | other -> failwithf "Expected refused energy, got %A" other

    Assert.True(Double.IsNaN state.TotalEnergy.Value)

[<Fact>]
let ``typed marginal estimate ignores refused observations and preserves marginal basis`` () =
    let measured value =
        Domain.measured value MarginalEstimate [ FromForkedReplicationBatch(ForkedReplicationBatchId "batch-typed") ] []

    let observations =
        { ContextClassId = ContextClassId "general"
          BatchId = ForkedReplicationBatchId "batch-typed"
          NaturalVelocities =
            [ { Label = "natural-a"; Value = measured 0.2 }
              { Label = "natural-refused"; Value = Domain.refused "bad natural sample" }
              { Label = "natural-b"; Value = measured 0.4 } ]
          PerturbationVelocities =
            [ { Label = "shock-a"; Value = measured 0.25 }
              { Label = "shock-b"; Value = measured 0.5 } ] }

    let summary = Measurement.marginalEstimateFromObservations observations

    Assert.Equal(MarginalEstimate, summary.Basis)

    match summary.NaturalVelocity, summary.PerturbationVelocity, summary.EffectiveMass with
    | Some natural, Some perturbation, Some mass ->
        Assert.Equal(2, natural.Count)
        Assert.Equal(0.3, natural.Mean, 3)
        Assert.Equal(2, perturbation.Count)
        Assert.Equal(0.375, perturbation.Mean, 3)
        Assert.Equal(2, mass.Count)
        Assert.Equal(3.0, mass.Mean, 3)
    | other -> failwithf "Expected all marginal summaries, got %A" other

[<Fact>]
let ``delta psi compares shared observable components between vectors`` () =
    let measured turnId value =
        Domain.measured value DirectObservation [ FromTurn(TurnId turnId) ] []

    let vector turnId alpha beta =
        Measurement.observableVector
            (TurnId turnId)
            { Alpha = Some(measured turnId alpha)
              Beta = Some(measured turnId beta)
              Gamma = None
              Delta = None
              Velocity = None
              Curvature = None
              TorsionalResistance = None
              Zeta4 = None }

    let delta = Measurement.deltaPsi (vector "left" 0.2 0.5) (vector "right" 0.7 0.1)

    Assert.Equal(TurnId "left", delta.LeftTurnId)
    Assert.Equal(TurnId "right", delta.RightTurnId)
    Assert.Equal(2, delta.Components.Length)
    Assert.Contains(delta.Components, fun item ->
        item.Name = "alpha" && Math.Abs(item.Difference.Value - 0.5) < 0.001)
    Assert.True(
        delta.Assumptions
        |> List.contains (CouplingHypothesis "Delta Psi compares externally observed trajectories; it does not imply shared internal model state.")
    )

[<Fact>]
let ``observable vector report preserves refused measurements without numeric values`` () =
    let vector =
        Measurement.observableVector
            (TurnId "reported-turn")
            { Alpha = Some(Domain.measured 0.8 DirectObservation [ FromTurn(TurnId "reported-turn") ] [])
              Beta = Some(Domain.refused "beta requires unavailable calibration")
              Gamma = None
              Delta = None
              Velocity = None
              Curvature = None
              TorsionalResistance = None
              Zeta4 = None }

    let report = Measurement.reportObservableVector "reported-turn" vector

    Assert.Equal("reported-turn", report.Subject)
    Assert.Equal(2, report.Items.Length)

    let beta =
        report.Items
        |> List.find (fun item -> item.Name = "beta")

    Assert.Equal(None, beta.Value)
    Assert.Equal(Some "beta requires unavailable calibration", beta.RefusalReason)
    Assert.Contains("beta refused", Assert.Single(report.Warnings))

let private feasibleCalibration =
    Measurement.buildCalibrationRecord
        { Id = CalibrationRecordId "cal-feasible"
          ContextClassId = ContextClassId "general"
          EstimatedAt = DateTimeOffset.UnixEpoch
          ValidFrom = DateTimeOffset.UnixEpoch
          ValidUntil = None
          NullTrialDisplacements = [ 0.01; 0.02; 0.03 ]
          HomogeneityScan =
            [ { Lambda = 0.10; VelocityChange = 0.02; CurvatureChange = 0.02 }
              { Lambda = 0.25; VelocityChange = 0.04; CurvatureChange = 0.03 } ]
          SingleTokenEditDisplacements = [ 0.06; 0.08; 0.10 ]
          TokenGranularityStatistic = Median
          LocalTolerance = 0.05 }

let private testPrefix =
    { SessionId = SessionId "protocol-session"
      ThroughSequenceIndex = 4
      PromptResponseHistory =
        [ Domain.prompt "prefix-p0" "Prompt zero", Domain.response "prefix-r0" "Response zero"
          Domain.prompt "prefix-p1" "Prompt one", Domain.response "prefix-r1" "Response one" ] }

[<Fact>]
let ``forked replication planner creates separate natural and shock tasks when calibration is feasible`` () =
    let plan =
        Measurement.planForkedReplicationBatch
            (ForkedReplicationBatchId "batch-plan")
            (ContextClassId "general")
            testPrefix
            feasibleCalibration
            3
            2
            (TargetLambda 0.12)

    Assert.Equal(3, plan.NaturalContinuations.Length)
    Assert.Equal(2, plan.ShockTrials.Length)
    Assert.All(plan.ShockTrials, fun trial ->
        Assert.Equal(None, trial.Refusal)
        Assert.Equal(TargetLambda 0.12, trial.Mode)
        Assert.Equal(LocalCalibrationEstimate(ContextClassId "general"), trial.Basis))

[<Fact>]
let ``forked replication planner refuses shock tasks when calibration is missing`` () =
    let missingCalibration =
        { Id = CalibrationRecordId "cal-missing-protocol"
          ContextClassId = ContextClassId "general"
          EstimatedAt = DateTimeOffset.UnixEpoch
          ValidFrom = DateTimeOffset.UnixEpoch
          ValidUntil = None
          NoiseFloorEpsilon = None
          LambdaMinEpsilon = None
          LocalValidityRadiusDelta = None
          LambdaMaxDelta = None
          TokenGranularityLambdaQuantum = None }

    let plan =
        Measurement.planForkedReplicationBatch
            (ForkedReplicationBatchId "batch-refused")
            (ContextClassId "general")
            testPrefix
            missingCalibration
            1
            2
            MinimalTokenEdit

    Assert.Equal(1, plan.NaturalContinuations.Length)
    Assert.Equal(2, plan.ShockTrials.Length)
    Assert.All(plan.ShockTrials, fun trial ->
        Assert.True(trial.Refusal.IsSome)
        match trial.Basis with
        | Refused reason -> Assert.Contains("calibration", reason)
        | other -> failwithf "Expected refused shock basis, got %A" other)
    Assert.Contains(plan.Warnings, fun warning -> warning.Contains("calibration"))

[<Fact>]
let ``forked replication aggregation reports independent marginals without joint pairing`` () =
    let plan =
        Measurement.planForkedReplicationBatch
            (ForkedReplicationBatchId "batch-results")
            (ContextClassId "general")
            testPrefix
            feasibleCalibration
            2
            3
            (TargetLambda 0.12)

    let measured label value source =
        Domain.measured value MarginalEstimate [ source ] [ ContinuityOfPerturbationVelocity ]

    let turn index =
        Domain.turn
            $"natural-result-{index}"
            "protocol-session"
            (5 + index)
            (Domain.prompt $"natural-p-{index}" $"Natural prompt {index}")
            (Domain.response $"natural-r-{index}" $"Natural response {index}")
            DateTimeOffset.UnixEpoch
            Natural
            0

    let shockTrial index =
        { Id = ShockTrialId $"shock-result-{index}"
          ContextPrefix = testPrefix
          ShockPrompt = Domain.prompt $"shock-p-{index}" $"Shock prompt {index}"
          Response = Domain.response $"shock-r-{index}" $"Shock response {index}"
          IntensityLambda = Some 0.12
          Mode = TargetLambda 0.12
          ResultingPerturbationVelocity = None
          ForkedReplicationBatchId = Some(ForkedReplicationBatchId "batch-results")
          AppliedInline = false }

    let results =
        { Plan = plan
          NaturalContinuations =
            [ { PlanLabel = "natural-1"
                Turn = turn 1
                Velocity = measured "natural-1" 0.20 (FromTurn(TurnId "natural-result-1")) }
              { PlanLabel = "natural-2"
                Turn = turn 2
                Velocity = measured "natural-2" 0.40 (FromTurn(TurnId "natural-result-2")) } ]
          ShockTrials =
            [ { PlanLabel = "shock-1"
                Trial = shockTrial 1
                PerturbationVelocity = measured "shock-1" 0.25 (FromShockTrial(ShockTrialId "shock-result-1")) }
              { PlanLabel = "shock-2"
                Trial = shockTrial 2
                PerturbationVelocity = measured "shock-2" 0.50 (FromShockTrial(ShockTrialId "shock-result-2")) }
              { PlanLabel = "shock-3"
                Trial = shockTrial 3
                PerturbationVelocity = measured "shock-3" 1.00 (FromShockTrial(ShockTrialId "shock-result-3")) } ] }

    let report = Measurement.aggregateForkedReplicationResults results

    Assert.Equal(2, report.NaturalObservationCount)
    Assert.Equal(3, report.ShockObservationCount)

    match report.Summary.NaturalVelocity, report.Summary.PerturbationVelocity, report.Summary.EffectiveMass with
    | Some natural, Some perturbation, Some mass ->
        Assert.Equal(0.30, natural.Mean, 3)
        Assert.Equal(0.583, perturbation.Mean, 3)
        Assert.Equal(2.333, mass.Mean, 3)
    | other -> failwithf "Expected marginal summaries, got %A" other

    Assert.Contains(report.Report.Warnings, fun warning -> warning.Contains("no natural/shock per-instance joint pairing"))

[<Fact>]
let ``minimal token edit shock builder chooses a nonzero displacement candidate`` () =
    let plan =
        Measurement.planForkedReplicationBatch
            (ForkedReplicationBatchId "batch-shock-build")
            (ContextClassId "general")
            testPrefix
            feasibleCalibration
            0
            1
            MinimalTokenEdit

    let shockPlan = Assert.Single(plan.ShockTrials)

    let result =
        Measurement.buildMinimalTokenEditShock
            { Plan = shockPlan
              Metric = CosineDistance
              CandidateTokens = [ "orthogonal"; "entropy" ] }

    match result with
    | Ok constructed ->
        Assert.True(constructed.Lambda > 0.0)
        Assert.True(constructed.CandidateCount > 0)
        Assert.False(String.IsNullOrWhiteSpace constructed.Prompt.Text)
        Assert.Contains(TokenGranularityAggregation "minimal-token-edit", constructed.Assumptions)
    | Error error -> failwithf "Expected constructed shock prompt, got %A" error

[<Fact>]
let ``shock execution uses builder and executor without provider coupling`` () =
    let plan =
        Measurement.planForkedReplicationBatch
            (ForkedReplicationBatchId "batch-exec")
            (ContextClassId "general")
            testPrefix
            feasibleCalibration
            0
            1
            MinimalTokenEdit

    let shockPlan = Assert.Single(plan.ShockTrials)

    let builder =
        { new IShockPromptBuilder with
            member _.Build request =
                Measurement.buildMinimalTokenEditShock
                    { request with CandidateTokens = [ "orthogonal"; "entropy" ] } }

    let executor =
        { new IPromptExecutor with
            member _.Submit request =
                async {
                    return
                        Ok
                            { RequestLabel = request.Label
                              Response = Domain.response "executed-shock-response" $"Executed response to {request.Prompt.Text}"
                              ObservedAt = DateTimeOffset.UnixEpoch
                              Metadata = Map.ofList [ "executor", "fake" ] }
                } }

    let result =
        Measurement.executeShockTrial CosineDistance builder executor [ "orthogonal"; "entropy" ] shockPlan
        |> Async.RunSynchronously

    match result with
    | ShockExecuted executed ->
        Assert.Equal(shockPlan.Label, executed.PlanLabel)
        Assert.Equal(Some shockPlan.BatchId, executed.Trial.ForkedReplicationBatchId)
        Assert.True(executed.PerturbationVelocity.Value > 0.0)
        Assert.Equal(MarginalEstimate, executed.PerturbationVelocity.Basis)
    | other -> failwithf "Expected executed shock, got %A" other

[<Fact>]
let ``shock execution returns refused result for refused shock plan`` () =
    let missingCalibration =
        { Id = CalibrationRecordId "cal-exec-missing"
          ContextClassId = ContextClassId "general"
          EstimatedAt = DateTimeOffset.UnixEpoch
          ValidFrom = DateTimeOffset.UnixEpoch
          ValidUntil = None
          NoiseFloorEpsilon = None
          LambdaMinEpsilon = None
          LocalValidityRadiusDelta = None
          LambdaMaxDelta = None
          TokenGranularityLambdaQuantum = None }

    let plan =
        Measurement.planForkedReplicationBatch
            (ForkedReplicationBatchId "batch-exec-refused")
            (ContextClassId "general")
            testPrefix
            missingCalibration
            0
            1
            MinimalTokenEdit

    let shockPlan = Assert.Single(plan.ShockTrials)

    let builder =
        { new IShockPromptBuilder with
            member _.Build _ = failwith "Builder should not be called for a refused shock plan." }

    let executor =
        { new IPromptExecutor with
            member _.Submit _ = failwith "Executor should not be called for a refused shock plan." }

    let result =
        Measurement.executeShockTrial CosineDistance builder executor [ "orthogonal" ] shockPlan
        |> Async.RunSynchronously

    match result with
    | ShockExecutionRefused(label, refusal) ->
        Assert.Equal(shockPlan.Label, label)
        match refusal.Basis with
        | Refused reason -> Assert.Contains("Window Inequality", reason)
        | other -> failwithf "Expected refused measured value, got %A" other
    | other -> failwithf "Expected refused shock execution, got %A" other

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

let private sampleJson =
    """
{
  "source": {
    "id": "json-source",
    "kind": "replay-file",
    "name": "lab replay",
    "metadata": {
      "instrument": "djehuti"
    }
  },
  "constants": {
    "distanceMetric": "cosine",
    "epsilon": 0.01,
    "strictWindowCheck": true,
    "labels": ["calibration", "smoke"]
  },
  "interactions": [
    {
      "turnId": "json-turn-0",
      "sessionId": "json-session",
      "modelId": "model-json",
      "sequenceIndex": 0,
      "observedAt": "2026-06-17T12:00:00Z",
      "promptId": "json-prompt-0",
      "prompt": "Define entropy.",
      "responseId": "json-response-0",
      "response": "Entropy measures uncertainty.",
      "strategy": "natural",
      "metadata": {
        "run": "A"
      }
    },
    {
      "turnId": "json-turn-1",
      "sessionId": "json-session",
      "modelId": "model-json",
      "sequenceIndex": 1,
      "prompt": "Give an example.",
      "response": "A shuffled deck has high entropy."
    }
  ]
}
"""

[<Fact>]
let ``json reader parses source constants and interaction records`` () =
    let dataSet = JsonInterop.readDataSetFromString sampleJson

    Assert.Equal(DataSourceId "json-source", dataSet.Source.Id)
    Assert.Equal(ReplayFile, dataSet.Source.Kind)
    Assert.Equal(2, dataSet.Interactions.Length)

    match dataSet.Constants["epsilon"] with
    | JsonNumber value -> Assert.Equal(0.01, value, 3)
    | other -> failwithf "Expected numeric epsilon, got %A" other

    match dataSet.Constants["strictWindowCheck"] with
    | JsonBoolean true -> ()
    | other -> failwithf "Expected boolean strictWindowCheck, got %A" other

    Assert.Equal(LogicalTime 0, dataSet.Interactions.Head.Clock.SequenceIndex)
    Assert.Equal("Define entropy.", dataSet.Interactions.Head.Prompt.Text)

[<Fact>]
let ``json data source feeds ingestion pipeline`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)
    let source = JsonDataSource(sampleJson) :> IDataSource

    let result =
        Storage.run context (Ingestion.ingestDataSource source Threading.CancellationToken.None)
        |> Async.RunSynchronously

    let turns =
        Storage.run context (StorageOps.listTurnsForSession (SessionId "json-session"))
        |> Async.RunSynchronously

    match result, turns with
    | Ok summary, Ok storedTurns ->
        Assert.Equal(2, summary.InteractionsObserved)
        Assert.Equal<int list>([ 0; 1 ], storedTurns |> List.map _.SequenceIndex)
        Assert.Equal("json-source", storedTurns.Head.Metadata["data_source_id"])
    | other -> failwithf "Expected JSON ingestion success, got %A" other

[<Fact>]
let ``json writer round trips compatible dataset`` () =
    let dataSet = JsonInterop.readDataSetFromString sampleJson
    let json = JsonInterop.writeDataSetToString dataSet
    let roundTripped = JsonInterop.readDataSetFromString json

    Assert.Equal(dataSet.Source.Id, roundTripped.Source.Id)
    Assert.Equal(dataSet.Interactions.Length, roundTripped.Interactions.Length)
    Assert.Equal(dataSet.Interactions[1].Response.Text, roundTripped.Interactions[1].Response.Text)

    match roundTripped.Constants["labels"] with
    | JsonArray [ JsonString "calibration"; JsonString "smoke" ] -> ()
    | other -> failwithf "Expected labels array, got %A" other

let private promptResponseExampleDataJson =
    """
{
  "source": {
    "id": "prompt-response-example-data",
    "kind": "manual-transcript",
    "name": "Prompt response example data",
    "metadata": {
      "origin": "Google Doc: Prompt response example data"
    }
  },
  "constants": {
    "distanceMetric": "cosine",
    "conversationType": "technical-troubleshooting",
    "turnCount": 10
  },
  "interactions": [
    {
      "turnId": "example-turn-0",
      "sessionId": "example-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 0,
      "prompt": "I need to design a resource routing pipeline for a distributed simulation. What is the most efficient way to handle continuous material flow between three distinct processing nodes?",
      "response": "For a continuous material flow across three nodes, a directed acyclic graph (DAG) architecture using a message broker like Apache Kafka or RabbitMQ is highly efficient. You can treat each processing node as a consumer/producer, ensuring asynchronous data transfer and buffering against node downtime."
    },
    {
      "turnId": "example-turn-1",
      "sessionId": "example-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 1,
      "prompt": "Let's avoid heavy message brokers. I want to handle this at the application layer using direct memory access or shared state if possible. The nodes are running on the same local cluster.",
      "response": "If the nodes are on the same local cluster and you want to avoid broker overhead, implementing a shared memory architecture using memory-mapped files or a distributed caching layer like Redis is ideal. You could also use direct gRPC streaming between the nodes for low-latency, point-to-point communication."
    },
    {
      "turnId": "example-turn-2",
      "sessionId": "example-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 2,
      "prompt": "Let's go with gRPC streaming. Node A generates raw input, Node B filters it, and Node C aggregates it. Can you outline the gRPC service definitions for this?",
      "response": "Certainly. You will need two primary gRPC services. First, a FilterService hosted by Node B that accepts a stream of RawInput messages from Node A and returns a stream of FilteredData. Second, an AggregationService hosted by Node C that accepts a stream of FilteredData from Node B and returns periodic AggregateStatus updates."
    },
    {
      "turnId": "example-turn-3",
      "sessionId": "example-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 3,
      "prompt": "Wait, if Node B is returning a stream to Node A, how does the data get to Node C? Node A shouldn't be responsible for passing it along.",
      "response": "You are completely right, my apologies for the confusion. Node A should only act as a client streaming to Node B. Node B will act as both a server receiving from A and a client streaming the filtered results forward to Node C. Node C is the final server terminating the pipeline."
    },
    {
      "turnId": "example-turn-4",
      "sessionId": "example-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 4,
      "prompt": "Okay, write the protobuf file for the connection between Node B and Node C only.",
      "response": "syntax = proto3; package pipeline; service AggregationService { rpc StreamFilteredData (stream FilteredPayload) returns (AggregationAck); } message FilteredPayload { string item_id = 1; bytes processed_data = 2; int64 timestamp = 3; } message AggregationAck { bool success = 1; int32 items_processed = 2; }"
    },
    {
      "turnId": "example-turn-5",
      "sessionId": "example-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 5,
      "prompt": "Node C needs to return a continuous stream of the aggregate, not just a single acknowledgment. Change the return type to a stream.",
      "response": "syntax = proto3; package pipeline; service AggregationService { rpc StreamFilteredData (stream FilteredPayload) returns (stream AggregateUpdate); } message FilteredPayload { string item_id = 1; bytes processed_data = 2; int64 timestamp = 3; } message AggregateUpdate { double current_moving_average = 1; int64 latest_timestamp = 2; }"
    },
    {
      "turnId": "example-turn-6",
      "sessionId": "example-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 6,
      "prompt": "Actually, moving average won't work for this data type. It's categorical data, not numerical. How do we aggregate categories over a stream?",
      "response": "For categorical data, a moving average is indeed incorrect. Instead, you should aggregate by tracking the frequency distribution or counting occurrences over a sliding time window. The AggregateUpdate message should be updated to return a map of category strings to their respective integer counts."
    },
    {
      "turnId": "example-turn-7",
      "sessionId": "example-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 7,
      "prompt": "Update the protobuf to reflect a frequency map.",
      "response": "syntax = proto3; package pipeline; service AggregationService { rpc StreamFilteredData (stream FilteredPayload) returns (stream AggregateUpdate); } message FilteredPayload { string item_id = 1; string category = 2; int64 timestamp = 3; } message AggregateUpdate { map<string, int32> category_frequencies = 1; int64 window_end_timestamp = 2; }"
    },
    {
      "turnId": "example-turn-8",
      "sessionId": "example-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 8,
      "prompt": "We need to ensure Node B doesn't overwhelm Node C. How do we implement backpressure in gRPC if the map gets too large?",
      "response": "gRPC handles backpressure natively at the network level using HTTP/2 flow control. If Node C processes the frequency map too slowly, the HTTP/2 receive window fills up, which automatically signals Node B's TCP stack to pause sending until Node C catches up and opens the window again."
    },
    {
      "turnId": "example-turn-9",
      "sessionId": "example-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 9,
      "prompt": "So I don't need to write any custom application-level backpressure logic for the category map?",
      "response": "In most standard use cases, no. The native HTTP/2 flow control handles the network-level backpressure for you. However, if your categorical map requires complex memory management on Node C before it can clear the buffer, you might eventually need application-level throttling, but gRPC's defaults are the best starting point."
    }
  ]
}
"""

[<Fact>]
let ``example prompt response data ingests and produces metrics`` () =
    let context = InMemoryStorage.createContext (fun () -> DateTimeOffset.UnixEpoch)
    let source = JsonDataSource(promptResponseExampleDataJson) :> IDataSource

    let ingestionResult =
        Storage.run context (Ingestion.ingestDataSource source Threading.CancellationToken.None)
        |> Async.RunSynchronously

    let turnsResult =
        Storage.run context (StorageOps.listTurnsForSession (SessionId "example-session"))
        |> Async.RunSynchronously

    match ingestionResult, turnsResult with
    | Ok summary, Ok turns ->
        let comparisons =
            turns
            |> List.map (fun turn -> PromptToResponse(turn.Prompt, turn.Response))

        let corpus = TextMetrics.corpusMetrics comparisons

        let velocities =
            turns
            |> List.pairwise
            |> List.map (fun (previousTurn, currentTurn) ->
                Measurement.velocityFromTurnPair CosineDistance previousTurn currentTurn)

        Assert.Equal(10, summary.InteractionsObserved)
        Assert.Equal(10, turns.Length)
        Assert.Equal<int list>([ 0..9 ], turns |> List.map _.SequenceIndex)
        Assert.Equal(10, corpus.ComparisonCount)
        Assert.Equal(10, corpus.PromptResponseCount)
        Assert.Equal(9, velocities.Length)
        Assert.All(velocities, fun velocity ->
            Assert.Equal(DirectObservation, velocity.Basis)
            Assert.InRange(velocity.Value, 0.0, 1.0))
        Assert.True(corpus.AverageCosineSimilarity > 0.0)
        Assert.True(corpus.AverageWordCountDelta > 0.0)
    | other -> failwithf "Expected example data ingestion and metrics, got %A" other
