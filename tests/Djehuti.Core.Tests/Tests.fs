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
