namespace Djehuti.Core

type DistanceMetric =
    | JaccardDistance
    | CosineDistance
    | NormalizedEditDistance

type DistributionSummary =
    { Count: int
      Mean: float
      Variance: float }

type MarginalSummary =
    { ContextClassId: ContextClassId
      NaturalVelocity: DistributionSummary option
      PerturbationVelocity: DistributionSummary option
      EffectiveMass: DistributionSummary option
      Basis: MeasurementBasis
      Sources: MeasurementSource list }

type EnergyState =
    { TurnId: TurnId
      PotentialEnergy: MeasuredValue
      EffectiveMassExpectation: MeasuredValue
      Velocity: MeasuredValue
      TotalEnergy: MeasuredValue }

type DissipationCheck =
    | NonIncreasing
    | Violation of previous: EnergyState * current: EnergyState * delta: float
    | InsufficientEnergyStates

type ObservableVectorInputs =
    { Alpha: MeasuredValue option
      Beta: MeasuredValue option
      Gamma: MeasuredValue option
      Delta: MeasuredValue option
      Velocity: MeasuredValue option
      Curvature: MeasuredValue option
      TorsionalResistance: MeasuredValue option
      Zeta4: MeasuredValue option }

type MarginalObservation =
    { Value: MeasuredValue
      Label: string }

type ForkedReplicationObservations =
    { ContextClassId: ContextClassId
      BatchId: ForkedReplicationBatchId
      NaturalVelocities: MarginalObservation list
      PerturbationVelocities: MarginalObservation list }

type ObservableDelta =
    { Name: string
      Left: MeasuredValue
      Right: MeasuredValue
      Difference: MeasuredValue }

type TrajectoryDelta =
    { LeftTurnId: TurnId
      RightTurnId: TurnId
      Components: ObservableDelta list
      Basis: MeasurementBasis
      Assumptions: AssumptionFlag list }

type TokenGranularityStatistic =
    | Minimum
    | Median
    | Mean

type LocalValiditySample =
    { Lambda: float
      VelocityChange: float
      CurvatureChange: float }

type CalibrationInputs =
    { Id: CalibrationRecordId
      ContextClassId: ContextClassId
      EstimatedAt: System.DateTimeOffset
      ValidFrom: System.DateTimeOffset
      ValidUntil: System.DateTimeOffset option
      NullTrialDisplacements: float list
      HomogeneityScan: LocalValiditySample list
      SingleTokenEditDisplacements: float list
      TokenGranularityStatistic: TokenGranularityStatistic
      LocalTolerance: float }

type LocalMeasurementDecision =
    | LocalMeasurementAllowed of basis: MeasurementBasis * lowerBound: float * upperBound: float
    | LocalMeasurementRefused of refusal: MeasuredValue * feasibility: WindowFeasibility

type MeasurementReportItem =
    { Name: string
      Value: float option
      Basis: MeasurementBasis
      Sources: MeasurementSource list
      Assumptions: AssumptionFlag list
      RefusalReason: string option }

type MeasurementReport =
    { Subject: string
      Items: MeasurementReportItem list
      Warnings: string list }

type NaturalContinuationPlan =
    { Label: string
      ContextPrefix: ContextPrefix
      BatchId: ForkedReplicationBatchId }

type ShockTrialPlan =
    { Label: string
      ContextPrefix: ContextPrefix
      BatchId: ForkedReplicationBatchId
      Mode: ShockMode
      Basis: MeasurementBasis
      Assumptions: AssumptionFlag list
      Refusal: MeasuredValue option }

type ForkedReplicationPlan =
    { BatchId: ForkedReplicationBatchId
      ContextClassId: ContextClassId
      SharedPrefix: ContextPrefix
      NaturalContinuations: NaturalContinuationPlan list
      ShockTrials: ShockTrialPlan list
      LocalMeasurementDecision: LocalMeasurementDecision
      Warnings: string list }

type NaturalContinuationResult =
    { PlanLabel: string
      Turn: Turn
      Velocity: MeasuredValue }

type ShockTrialResult =
    { PlanLabel: string
      Trial: ShockTrial
      PerturbationVelocity: MeasuredValue }

type ForkedReplicationResults =
    { Plan: ForkedReplicationPlan
      NaturalContinuations: NaturalContinuationResult list
      ShockTrials: ShockTrialResult list }

type ForkedReplicationReport =
    { BatchId: ForkedReplicationBatchId
      Summary: MarginalSummary
      Report: MeasurementReport
      NaturalObservationCount: int
      ShockObservationCount: int }

type PromptExecutionRequest =
    { Label: string
      ContextPrefix: ContextPrefix
      Prompt: Prompt
      Strategy: SamplingStrategy
      BatchId: ForkedReplicationBatchId option }

type PromptExecutionResult =
    { RequestLabel: string
      Response: Response
      ObservedAt: System.DateTimeOffset
      Metadata: Map<string, string> }

type ProtocolExecutionError =
    | ProtocolValidationFailure of message: string
    | ProtocolExecutionFailure of message: string

type IPromptExecutor =
    abstract Submit: PromptExecutionRequest -> Async<Result<PromptExecutionResult, ProtocolExecutionError>>

type ShockConstructionRequest =
    { Plan: ShockTrialPlan
      Metric: DistanceMetric
      CandidateTokens: string list }

type ShockConstructionResult =
    { Prompt: Prompt
      Lambda: float
      CandidateCount: int
      Basis: MeasurementBasis
      Assumptions: AssumptionFlag list }

type IShockPromptBuilder =
    abstract Build: ShockConstructionRequest -> Result<ShockConstructionResult, ProtocolExecutionError>

type ShockExecutionResult =
    | ShockExecuted of ShockTrialResult
    | ShockExecutionRefused of label: string * refusal: MeasuredValue
    | ShockExecutionFailed of label: string * error: ProtocolExecutionError

module Measurement =
    let private divide numerator denominator =
        if denominator = 0.0 then 0.0 else numerator / denominator

    let private summary values =
        match values with
        | [] ->
            { Count = 0
              Mean = 0.0
              Variance = 0.0 }
        | _ ->
            let count = values.Length
            let mean = values |> List.average

            let variance =
                values
                |> List.averageBy (fun value ->
                    let difference = value - mean
                    difference * difference)

            { Count = count
              Mean = mean
              Variance = variance }

    let private median values =
        match values |> List.sort with
        | [] -> 0.0
        | sorted ->
            let count = sorted.Length
            if count % 2 = 1 then
                sorted[count / 2]
            else
                (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0

    let private nonNegativeValues values =
        values
        |> List.filter (fun value ->
            not (System.Double.IsNaN value)
            && not (System.Double.IsInfinity value)
            && value >= 0.0)

    let distance metric leftText rightText =
        let leftParts = Decompose.parts leftText
        let rightParts = Decompose.parts rightText

        match metric with
        | JaccardDistance -> 1.0 - TextMetrics.jaccardSimilarity leftParts rightParts
        | CosineDistance -> 1.0 - TextMetrics.cosineSimilarity leftParts rightParts
        | NormalizedEditDistance -> 1.0 - TextMetrics.normalizedEditSimilarity leftText rightText

    let private contextBasisText (prefix: ContextPrefix) =
        prefix.PromptResponseHistory
        |> List.tryLast
        |> Option.map (fun (prompt, _) -> prompt.Text)
        |> Option.defaultValue ""

    let private tokenEditCandidates (candidateTokens: string list) (text: string) =
        let tokens = Decompose.words text
        let candidateTokens =
            candidateTokens
            |> List.map (fun token -> token.Trim())
            |> List.filter (System.String.IsNullOrWhiteSpace >> not)
            |> List.distinct

        let join (tokens: string list) = System.String.Join(" ", tokens)

        let deletions =
            tokens
            |> List.mapi (fun index _ ->
                tokens
                |> List.indexed
                |> List.choose (fun (candidateIndex, token) ->
                    if candidateIndex = index then None else Some token)
                |> join)

        let replacements =
            [ for index in 0 .. tokens.Length - 1 do
                  for candidate in candidateTokens do
                      yield
                          tokens
                          |> List.mapi (fun candidateIndex token ->
                              if candidateIndex = index then candidate else token)
                          |> join ]

        let insertions =
            [ for index in 0 .. tokens.Length do
                  for candidate in candidateTokens do
                      let before = tokens |> List.take index
                      let after = tokens |> List.skip index
                      yield join (before @ [ candidate ] @ after) ]

        [ yield! deletions
          yield! replacements
          yield! insertions ]
        |> List.map (fun value -> value.Trim())
        |> List.filter (System.String.IsNullOrWhiteSpace >> not)
        |> List.distinct

    let buildMinimalTokenEditShock request =
        match request.Plan.Refusal with
        | Some refusal -> Ok { Prompt = Domain.prompt $"{request.Plan.Label}-refused-shock" ""; Lambda = nan; CandidateCount = 0; Basis = refusal.Basis; Assumptions = refusal.Assumptions }
        | None ->
            let basisText = contextBasisText request.Plan.ContextPrefix
            let candidates = tokenEditCandidates request.CandidateTokens basisText

            let scored =
                candidates
                |> List.map (fun candidate -> candidate, distance request.Metric basisText candidate)
                |> List.filter (fun (_, lambda) -> lambda > 0.0)
                |> List.sortBy snd

            match scored with
            | [] ->
                Error(ProtocolValidationFailure "Minimal token-edit shock construction requires at least one nonzero displacement candidate.")
            | (candidate, lambda) :: _ ->
                Ok
                    { Prompt = Domain.prompt $"{request.Plan.Label}-shock-prompt" candidate
                      Lambda = lambda
                      CandidateCount = candidates.Length
                      Basis = request.Plan.Basis
                      Assumptions =
                        request.Plan.Assumptions
                        |> List.append [ TokenGranularityAggregation "minimal-token-edit" ]
                        |> List.distinct }

    let velocityFromTurnPair metric (previousTurn: Turn) (currentTurn: Turn) =
        let value = distance metric previousTurn.Response.Text currentTurn.Response.Text

        Domain.measured
            value
            DirectObservation
            [ FromTurn previousTurn.Id; FromTurn currentTurn.Id ]
            []

    let perturbationVelocity metric (prefix: ContextPrefix) (trial: ShockTrial) =
        let prefixText =
            prefix.PromptResponseHistory
            |> List.tryLast
            |> Option.map (fun (_, response) -> response.Text)
            |> Option.defaultValue ""

        let value = distance metric prefixText trial.Response.Text

        Domain.measured value MarginalEstimate [ FromShockTrial trial.Id ] [ ContinuityOfPerturbationVelocity ]

    let effectiveMassFromPerturbationVelocity (perturbationVelocity: MeasuredValue) =
        if perturbationVelocity.Value <= 0.0 then
            Domain.refused "Effective mass is undefined when perturbation velocity is zero or negative."
        else
            { perturbationVelocity with
                Value = 1.0 / perturbationVelocity.Value
                Assumptions =
                    perturbationVelocity.Assumptions
                    |> List.append [ ContinuityOfPerturbationVelocity ] }

    let private isRefused (measurement: MeasuredValue) =
        match measurement.Basis with
        | Refused _ -> true
        | _ -> false

    let private isUsable (measurement: MeasuredValue) =
        not (isRefused measurement)
        && not (System.Double.IsNaN measurement.Value)
        && not (System.Double.IsInfinity measurement.Value)

    let private combineAssumptions (measurements: MeasuredValue list) =
        measurements
        |> List.collect _.Assumptions
        |> List.distinct

    let private combineSources (measurements: MeasuredValue list) =
        measurements
        |> List.collect _.Sources
        |> List.distinct

    let private refusalReason (measurement: MeasuredValue) =
        match measurement.Basis with
        | Refused reason -> Some reason
        | _ -> None

    let reportItem name (measurement: MeasuredValue) : MeasurementReportItem =
        let value =
            if isUsable measurement then
                Some measurement.Value
            else
                None

        { Name = name
          Value = value
          Basis = measurement.Basis
          Sources = measurement.Sources
          Assumptions = measurement.Assumptions
          RefusalReason = refusalReason measurement }

    let observableVector turnId (inputs: ObservableVectorInputs) =
        { TurnId = turnId
          Alpha = inputs.Alpha
          Beta = inputs.Beta
          Gamma = inputs.Gamma
          Delta = inputs.Delta
          Velocity = inputs.Velocity
          Curvature = inputs.Curvature
          TorsionalResistance = inputs.TorsionalResistance
          Zeta4 = inputs.Zeta4 }

    let observableVectorFromTurn metric (previousTurn: Turn option) (turn: Turn) =
        let comparison =
            TextMetrics.comparisonMetrics
                $"{turn.Id}-prompt-response"
                (PromptToResponse(turn.Prompt, turn.Response))

        let direct value =
            Domain.measured value DirectObservation [ FromTurn turn.Id ] []

        let velocity =
            previousTurn
            |> Option.map (fun previous -> velocityFromTurnPair metric previous turn)

        observableVector
            turn.Id
            { Alpha = Some(direct comparison.CosineSimilarity)
              Beta = Some(direct comparison.JaccardSimilarity)
              Gamma = Some(direct comparison.NormalizedEditSimilarity)
              Delta = Some(direct (float comparison.WordCountDelta))
              Velocity = velocity
              Curvature = None
              TorsionalResistance = None
              Zeta4 = None }

    let reportObservableVector subject (vector: ObservableVector) =
        let items =
            [ "alpha", vector.Alpha
              "beta", vector.Beta
              "gamma", vector.Gamma
              "delta", vector.Delta
              "v", vector.Velocity
              "kappa", vector.Curvature
              "tau", vector.TorsionalResistance
              "zeta4", vector.Zeta4 ]
            |> List.choose (fun (name, value) ->
                value |> Option.map (reportItem name))

        let warnings =
            items
            |> List.choose (fun item ->
                item.RefusalReason
                |> Option.map (fun reason -> $"{item.Name} refused: {reason}"))

        { Subject = subject
          Items = items
          Warnings = warnings }

    let checkWindowFeasibility (calibration: CalibrationRecord) =
        let missing =
            [ "lambda_min(epsilon)", calibration.LambdaMinEpsilon
              "lambda_max(delta)", calibration.LambdaMaxDelta
              "lambda_quantum", calibration.TokenGranularityLambdaQuantum ]
            |> List.choose (fun (name, value) ->
                match value with
                | Some _ -> None
                | None -> Some name)

        match missing with
        | _ :: _ -> MissingCalibration missing
        | [] ->
            let lambdaMin = calibration.LambdaMinEpsilon.Value.Value
            let lambdaMax = calibration.LambdaMaxDelta.Value.Value
            let lambdaQuantum = calibration.TokenGranularityLambdaQuantum.Value.Value
            let lowerBound = max lambdaMin lambdaQuantum

            if lowerBound <= lambdaMax then
                Feasible(lowerBound, lambdaMax)
            else
                Infeasible(
                    lowerBound,
                    lambdaMax,
                    "Local measurement is infeasible at this context; use global calibration fallback."
                )

    let private statisticName statistic =
        match statistic with
        | Minimum -> "minimum"
        | Median -> "median"
        | Mean -> "mean"

    let private aggregate statistic values =
        let values = nonNegativeValues values

        match statistic, values with
        | _, [] -> None
        | Minimum, _ -> Some(List.min values)
        | Median, _ -> Some(median values)
        | Mean, _ -> Some(List.average values)

    let buildCalibrationRecord inputs =
        let estimate value method sampleSize assumptions =
            { Value = value
              Method = method
              SampleSize = sampleSize
              Assumptions = assumptions }

        let nullDisplacements = nonNegativeValues inputs.NullTrialDisplacements
        let tokenDisplacements = nonNegativeValues inputs.SingleTokenEditDisplacements

        let noiseFloor =
            nullDisplacements
            |> aggregate Mean
            |> Option.map (fun value ->
                estimate value NoiseFloorFromNullTrials nullDisplacements.Length [])

        let lambdaMin =
            noiseFloor
            |> Option.map (fun epsilon ->
                estimate
                    (epsilon.Value * 2.0)
                    (ManualCalibration "Derived lower shock bound from twice the observed noise floor.")
                    epsilon.SampleSize
                    epsilon.Assumptions)

        let localValidity =
            inputs.HomogeneityScan
            |> List.filter (fun sample ->
                sample.Lambda >= 0.0
                && sample.VelocityChange <= inputs.LocalTolerance
                && sample.CurvatureChange <= inputs.LocalTolerance)
            |> List.sortBy _.Lambda
            |> List.tryLast
            |> Option.map (fun sample ->
                estimate
                    sample.Lambda
                    LocalValidityFromHomogeneityScan
                    inputs.HomogeneityScan.Length
                    [ IsotropicEmbeddingCurvatureApproximation ])

        let tokenGranularity =
            tokenDisplacements
            |> aggregate inputs.TokenGranularityStatistic
            |> Option.map (fun value ->
                let statistic = statisticName inputs.TokenGranularityStatistic

                estimate
                    value
                    (TokenGranularityFromSingleTokenEdits statistic)
                    tokenDisplacements.Length
                    [ TokenGranularityAggregation statistic ])

        { Id = inputs.Id
          ContextClassId = inputs.ContextClassId
          EstimatedAt = inputs.EstimatedAt
          ValidFrom = inputs.ValidFrom
          ValidUntil = inputs.ValidUntil
          NoiseFloorEpsilon = noiseFloor
          LambdaMinEpsilon = lambdaMin
          LocalValidityRadiusDelta = localValidity
          LambdaMaxDelta = localValidity
          TokenGranularityLambdaQuantum = tokenGranularity }

    let decideLocalMeasurement calibration =
        let feasibility = checkWindowFeasibility calibration

        match feasibility with
        | Feasible(lowerBound, upperBound) ->
            LocalMeasurementAllowed(LocalCalibrationEstimate calibration.ContextClassId, lowerBound, upperBound)
        | Infeasible _
        | MissingCalibration _ ->
            LocalMeasurementRefused(
                Domain.refused "Local measurement refused because calibration does not satisfy the Window Inequality.",
                feasibility
            )

    let planForkedReplicationBatch
        batchId
        contextClassId
        sharedPrefix
        calibration
        naturalSampleCount
        shockSampleCount
        shockMode
        =
        let decision = decideLocalMeasurement calibration

        let count name value =
            if value < 0 then
                invalidArg name "Sample count must not be negative."
            else
                value

        let naturalSampleCount = count (nameof naturalSampleCount) naturalSampleCount
        let shockSampleCount = count (nameof shockSampleCount) shockSampleCount

        let naturalContinuations =
            [ 1..naturalSampleCount ]
            |> List.map (fun index ->
                { Label = $"natural-{index}"
                  ContextPrefix = sharedPrefix
                  BatchId = batchId })

        let shockBasis, shockRefusal, shockWarnings =
            match decision with
            | LocalMeasurementAllowed(basis, _, _) -> basis, None, []
            | LocalMeasurementRefused(refusal, feasibility) ->
                Refused "Shock trial planning refused because local calibration is infeasible or incomplete.",
                Some refusal,
                [ $"Shock trials require calibration before local measurement; feasibility was {feasibility}." ]

        let shockTrials =
            [ 1..shockSampleCount ]
            |> List.map (fun index ->
                { Label = $"shock-{index}"
                  ContextPrefix = sharedPrefix
                  BatchId = batchId
                  Mode = shockMode
                  Basis = shockBasis
                  Assumptions = [ ContinuityOfPerturbationVelocity ]
                  Refusal = shockRefusal })

        let sampleWarnings =
            [ if naturalSampleCount = 0 then
                  yield "No natural continuation samples were requested; natural velocity marginal cannot be estimated."
              if shockSampleCount = 0 then
                  yield "No shock samples were requested; perturbation and effective-mass marginals cannot be estimated." ]

        { BatchId = batchId
          ContextClassId = contextClassId
          SharedPrefix = sharedPrefix
          NaturalContinuations = naturalContinuations
          ShockTrials = shockTrials
          LocalMeasurementDecision = decision
          Warnings = sampleWarnings @ shockWarnings }

    let executeShockTrial metric (builder: IShockPromptBuilder) (executor: IPromptExecutor) candidateTokens (plan: ShockTrialPlan) =
        async {
            match plan.Refusal with
            | Some refusal -> return ShockExecutionRefused(plan.Label, refusal)
            | None ->
                let constructionRequest =
                    { Plan = plan
                      Metric = metric
                      CandidateTokens = candidateTokens }

                match builder.Build constructionRequest with
                | Error error -> return ShockExecutionFailed(plan.Label, error)
                | Ok construction ->
                    let executionRequest =
                        { Label = plan.Label
                          ContextPrefix = plan.ContextPrefix
                          Prompt = construction.Prompt
                          Strategy = Shock
                          BatchId = Some plan.BatchId }

                    let! execution = executor.Submit executionRequest

                    match execution with
                    | Error error -> return ShockExecutionFailed(plan.Label, error)
                    | Ok result ->
                        let trial =
                            { Id = ShockTrialId $"{plan.BatchId}-{plan.Label}"
                              ContextPrefix = plan.ContextPrefix
                              ShockPrompt = construction.Prompt
                              Response = result.Response
                              IntensityLambda = Some construction.Lambda
                              Mode = plan.Mode
                              ResultingPerturbationVelocity = None
                              ForkedReplicationBatchId = Some plan.BatchId
                              AppliedInline = false }

                        let perturbation = perturbationVelocity metric plan.ContextPrefix trial

                        let trial =
                            { trial with ResultingPerturbationVelocity = Some perturbation }

                        return
                            ShockExecuted
                                { PlanLabel = plan.Label
                                  Trial = trial
                                  PerturbationVelocity = perturbation }
        }

    let marginalEstimate (batch: ForkedReplicationBatch) =
        let naturalVelocities =
            batch.Members
            |> List.choose (function
                | NaturalContinuation turn ->
                    match turn.Metadata.TryFind "velocity" with
                    | Some value ->
                        match System.Double.TryParse value with
                        | true, parsed -> Some parsed
                        | false, _ -> None
                    | None -> None
                | ShockContinuation _ -> None)

        let perturbationVelocities =
            batch.Members
            |> List.choose (function
                | ShockContinuation trial ->
                    trial.ResultingPerturbationVelocity
                    |> Option.map _.Value
                | NaturalContinuation _ -> None)

        let effectiveMassValues =
            perturbationVelocities
            |> List.choose (fun value ->
                if value <= 0.0 then None else Some(1.0 / value))

        { ContextClassId = batch.ContextClassId
          NaturalVelocity =
            if naturalVelocities.IsEmpty then None else Some(summary naturalVelocities)
          PerturbationVelocity =
            if perturbationVelocities.IsEmpty then None else Some(summary perturbationVelocities)
          EffectiveMass =
            if effectiveMassValues.IsEmpty then None else Some(summary effectiveMassValues)
          Basis = MarginalEstimate
          Sources = [ FromForkedReplicationBatch batch.Id ] }

    let marginalEstimateFromObservations (observations: ForkedReplicationObservations) =
        let values (items: MarginalObservation list) =
            items
            |> List.choose (fun observation ->
                if isUsable observation.Value then
                    Some observation.Value.Value
                else
                    None)

        let naturalVelocities = values observations.NaturalVelocities
        let perturbationVelocities = values observations.PerturbationVelocities

        let effectiveMassValues =
            perturbationVelocities
            |> List.choose (fun value ->
                if value <= 0.0 then None else Some(1.0 / value))

        { ContextClassId = observations.ContextClassId
          NaturalVelocity =
            if naturalVelocities.IsEmpty then None else Some(summary naturalVelocities)
          PerturbationVelocity =
            if perturbationVelocities.IsEmpty then None else Some(summary perturbationVelocities)
          EffectiveMass =
            if effectiveMassValues.IsEmpty then None else Some(summary effectiveMassValues)
          Basis = MarginalEstimate
          Sources = [ FromForkedReplicationBatch observations.BatchId ] }

    let aggregateForkedReplicationResults (results: ForkedReplicationResults) =
        let naturalObservations =
            results.NaturalContinuations
            |> List.map (fun result ->
                { Label = result.PlanLabel
                  Value = result.Velocity })

        let shockObservations =
            results.ShockTrials
            |> List.map (fun result ->
                { Label = result.PlanLabel
                  Value = result.PerturbationVelocity })

        let observations =
            { ContextClassId = results.Plan.ContextClassId
              BatchId = results.Plan.BatchId
              NaturalVelocities = naturalObservations
              PerturbationVelocities = shockObservations }

        let marginal = marginalEstimateFromObservations observations

        let reportItems =
            [ yield!
                  naturalObservations
                  |> List.map (fun observation -> reportItem $"natural velocity:{observation.Label}" observation.Value)
              yield!
                  shockObservations
                  |> List.map (fun observation -> reportItem $"perturbation velocity:{observation.Label}" observation.Value) ]

        let warnings =
            [ yield! results.Plan.Warnings
              if results.NaturalContinuations.Length <> results.ShockTrials.Length then
                  yield "Natural and shock sample counts differ; marginals are intentionally reported independently, not as per-instance joint pairs."
              yield "Forked replication aggregation reports independent marginal distributions only; no natural/shock per-instance joint pairing is synthesized." ]

        { BatchId = results.Plan.BatchId
          Summary = marginal
          Report =
            { Subject = $"forked replication batch {results.Plan.BatchId}"
              Items = reportItems
              Warnings = warnings }
          NaturalObservationCount = results.NaturalContinuations.Length
          ShockObservationCount = results.ShockTrials.Length }

    let computeEnergy turnId (potentialEnergy: MeasuredValue) (effectiveMassExpectation: MeasuredValue) (velocity: MeasuredValue) =
        let inputs = [ potentialEnergy; effectiveMassExpectation; velocity ]

        let total =
            if inputs |> List.exists (isUsable >> not) then
                Domain.refused "Energy cannot be computed from refused, NaN, or infinite input measurements."
            else
                let value =
                    potentialEnergy.Value + (0.5 * effectiveMassExpectation.Value * velocity.Value * velocity.Value)

                let assumptions = combineAssumptions inputs

                let basis =
                    match effectiveMassExpectation.Basis with
                    | DirectObservation ->
                        HypothesisDependent [ CouplingHypothesis "Energy uses calibrated effective mass expectation, not a joint per-instance m_eff(t)." ]
                    | basis -> basis

                Domain.measured value basis (combineSources inputs) assumptions

        { TurnId = turnId
          PotentialEnergy = potentialEnergy
          EffectiveMassExpectation = effectiveMassExpectation
          Velocity = velocity
          TotalEnergy = total }

    let checkDissipation states =
        match states with
        | []
        | [ _ ] -> InsufficientEnergyStates
        | first :: rest ->
            ((NonIncreasing, first), rest)
            ||> List.fold (fun (status, previous) current ->
                match status with
                | Violation _ -> status, current
                | InsufficientEnergyStates -> status, current
                | NonIncreasing ->
                    let delta = current.TotalEnergy.Value - previous.TotalEnergy.Value

                    if delta <= 0.0 then
                        NonIncreasing, current
                    else
                        Violation(previous, current, delta), current)
            |> fst

    let private componentValues (vector: ObservableVector) =
        [ "alpha", vector.Alpha
          "beta", vector.Beta
          "gamma", vector.Gamma
          "delta", vector.Delta
          "v", vector.Velocity
          "kappa", vector.Curvature
          "tau", vector.TorsionalResistance
          "zeta4", vector.Zeta4 ]

    let deltaPsi (left: ObservableVector) (right: ObservableVector) =
        let components =
            List.zip (componentValues left) (componentValues right)
            |> List.choose (fun ((name, leftValue), (_, rightValue)) ->
                match leftValue, rightValue with
                | Some leftMeasurement, Some rightMeasurement when isUsable leftMeasurement && isUsable rightMeasurement ->
                    let assumptions =
                        [ leftMeasurement; rightMeasurement ]
                        |> combineAssumptions
                        |> List.append [ CouplingHypothesis "Delta Psi compares externally observed trajectories; it does not imply shared internal model state." ]
                        |> List.distinct

                    let difference =
                        Domain.measured
                            (rightMeasurement.Value - leftMeasurement.Value)
                            DirectObservation
                            (combineSources [ leftMeasurement; rightMeasurement ])
                            assumptions

                    Some
                        { Name = name
                          Left = leftMeasurement
                          Right = rightMeasurement
                          Difference = difference }
                | _ -> None)

        { LeftTurnId = left.TurnId
          RightTurnId = right.TurnId
          Components = components
          Basis = DirectObservation
          Assumptions =
            components
            |> List.collect (fun item -> item.Difference.Assumptions)
            |> List.distinct }
