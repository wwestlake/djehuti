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

type TrajectoryPoint =
    { TurnId: TurnId
      SequenceIndex: int
      Coordinates: Map<string, MeasuredValue> }

type HermiteSegment =
    { StartPoint: TrajectoryPoint
      EndPoint: TrajectoryPoint
      StartTangent: Map<string, MeasuredValue>
      EndTangent: Map<string, MeasuredValue>
      Assumptions: AssumptionFlag list }

type FormalIdentityKind =
    | StabilityCriterion
    | CumulativeLeakageFunctional
    | TorsionalAccumulation

type FormalIdentityDiagnostic =
    { Kind: FormalIdentityKind
      Value: MeasuredValue
      Inputs: string list
      Description: string }

type AttractorDetectionThresholds =
    { StabilityMarginMaximum: float
      TorsionalAccumulationMinimum: float }

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

type NaturalExecutionResult =
    | NaturalExecuted of NaturalContinuationResult
    | NaturalExecutionFailed of label: string * error: ProtocolExecutionError

type NaturalPromptBuildRequest =
    { Plan: NaturalContinuationPlan }

type INaturalPromptBuilder =
    abstract Build: NaturalPromptBuildRequest -> Result<Prompt, ProtocolExecutionError>

type ForkedReplicationExecution =
    { Plan: ForkedReplicationPlan
      Results: ForkedReplicationResults
      NaturalExecutionResults: NaturalExecutionResult list
      ShockExecutionResults: ShockExecutionResult list
      Warnings: string list }

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

    let buildMinimalTokenEditShock (request: ShockConstructionRequest) =
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

    let trajectoryPointFromObservableVector sequenceIndex (vector: ObservableVector) =
        let coordinates =
            [ "alpha", vector.Alpha
              "beta", vector.Beta
              "gamma", vector.Gamma
              "delta", vector.Delta
              "v", vector.Velocity
              "kappa", vector.Curvature
              "tau", vector.TorsionalResistance
              "zeta4", vector.Zeta4 ]
            |> List.choose (fun (name, value) ->
                value
                |> Option.bind (fun measurement ->
                    if isUsable measurement then Some(name, measurement) else None))
            |> Map.ofList

        { TurnId = vector.TurnId
          SequenceIndex = sequenceIndex
          Coordinates = coordinates }

    let private zeta4Assumption =
        CouplingHypothesis "Zeta4 is computed as a four-component diagnostic norm over alpha, beta, gamma, and delta."

    let zeta4FromObservableVector (vector: ObservableVector) =
        match vector.Alpha, vector.Beta, vector.Gamma, vector.Delta with
        | Some alpha, Some beta, Some gamma, Some delta
            when [ alpha; beta; gamma; delta ] |> List.forall isUsable ->
            let value =
                [ alpha.Value; beta.Value; gamma.Value; delta.Value ]
                |> List.sumBy (fun value -> value * value)
                |> sqrt

            Domain.measured
                value
                (HypothesisDependent [ zeta4Assumption ])
                (combineSources [ alpha; beta; gamma; delta ])
                ([ zeta4Assumption ] @ combineAssumptions [ alpha; beta; gamma; delta ] |> List.distinct)
        | _ ->
            Domain.refused "Zeta4 requires usable alpha, beta, gamma, and delta components."

    let observableVectorWithZeta4 (vector: ObservableVector) =
        { vector with Zeta4 = Some(zeta4FromObservableVector vector) }

    let private sharedCoordinateNames points =
        match points with
        | [] -> Set.empty
        | first :: rest ->
            rest
            |> List.fold
                (fun names point -> Set.intersect names (point.Coordinates |> Map.keys |> Set.ofSeq))
                (first.Coordinates |> Map.keys |> Set.ofSeq)

    let private coordinateVector names point =
        names
        |> List.map (fun name -> point.Coordinates[name].Value)

    let private subtract left right =
        List.zip left right
        |> List.map (fun (l, r) -> l - r)

    let private add left right =
        List.zip left right
        |> List.map (fun (l, r) -> l + r)

    let private scale factor vector =
        vector |> List.map ((*) factor)

    let private magnitude vector =
        vector
        |> List.sumBy (fun value -> value * value)
        |> sqrt

    let private distanceBetween left right =
        subtract left right |> magnitude

    let discreteCurvature previous current next =
        let names =
            sharedCoordinateNames [ previous; current; next ]
            |> Set.toList

        if names.Length < 2 then
            Domain.refused "Curvature requires at least two shared usable observable coordinates across three trajectory points."
        else
            let a = coordinateVector names previous
            let b = coordinateVector names current
            let c = coordinateVector names next
            let ab = distanceBetween a b
            let bc = distanceBetween b c
            let ac = distanceBetween a c

            if ab = 0.0 || bc = 0.0 || ac = 0.0 then
                Domain.refused "Curvature is undefined for repeated or zero-distance trajectory points."
            else
                let s = (ab + bc + ac) / 2.0
                let areaSquared = max (s * (s - ab) * (s - bc) * (s - ac)) 0.0
                let area = sqrt areaSquared
                let curvature = (4.0 * area) / (ab * bc * ac)

                Domain.measured
                    curvature
                    (HypothesisDependent [ IsotropicEmbeddingCurvatureApproximation ])
                    [ FromTurn previous.TurnId; FromTurn current.TurnId; FromTurn next.TurnId ]
                    [ IsotropicEmbeddingCurvatureApproximation ]

    let private tangentBetween names previous next =
        let previousValues = coordinateVector names previous
        let nextValues = coordinateVector names next
        let dt = float (max (next.SequenceIndex - previous.SequenceIndex) 1)
        subtract nextValues previousValues |> scale (1.0 / dt)

    let hermiteSegment previous startPoint endPoint next =
        let names =
            sharedCoordinateNames [ previous; startPoint; endPoint; next ]
            |> Set.toList

        if names.IsEmpty then
            Error(ProtocolValidationFailure "Hermite interpolation requires at least one shared usable observable coordinate.")
        else
            let startTangentValues = tangentBetween names previous startPoint
            let endTangentValues = tangentBetween names endPoint next

            let measuredTangent point name value =
                Domain.measured
                    value
                    (HypothesisDependent [ CouplingHypothesis "Cubic Hermite interpolation is a contour-style diagnostic approximation over observed states." ])
                    [ FromTurn point.TurnId ]
                    [ CouplingHypothesis "Cubic Hermite interpolation is a contour-style diagnostic approximation over observed states." ]

            Ok
                { StartPoint = startPoint
                  EndPoint = endPoint
                  StartTangent =
                    List.zip names startTangentValues
                    |> List.map (fun (name, value) -> name, measuredTangent startPoint name value)
                    |> Map.ofList
                  EndTangent =
                    List.zip names endTangentValues
                    |> List.map (fun (name, value) -> name, measuredTangent endPoint name value)
                    |> Map.ofList
                  Assumptions = [ CouplingHypothesis "Cubic Hermite interpolation is a contour-style diagnostic approximation over observed states." ] }

    let interpolateHermite u segment =
        let u = max 0.0 (min 1.0 u)
        let h00 = 2.0 * u * u * u - 3.0 * u * u + 1.0
        let h10 = u * u * u - 2.0 * u * u + u
        let h01 = -2.0 * u * u * u + 3.0 * u * u
        let h11 = u * u * u - u * u
        let names =
            sharedCoordinateNames [ segment.StartPoint; segment.EndPoint ]
            |> Set.intersect (segment.StartTangent |> Map.keys |> Set.ofSeq)
            |> Set.intersect (segment.EndTangent |> Map.keys |> Set.ofSeq)
            |> Set.toList

        let coordinates =
            names
            |> List.map (fun name ->
                let p0 = segment.StartPoint.Coordinates[name]
                let p1 = segment.EndPoint.Coordinates[name]
                let m0 = segment.StartTangent[name]
                let m1 = segment.EndTangent[name]
                let value = h00 * p0.Value + h10 * m0.Value + h01 * p1.Value + h11 * m1.Value

                name,
                Domain.measured
                    value
                    (HypothesisDependent segment.Assumptions)
                    (p0.Sources @ p1.Sources @ m0.Sources @ m1.Sources |> List.distinct)
                    segment.Assumptions)
            |> Map.ofList

        { TurnId = segment.StartPoint.TurnId
          SequenceIndex = segment.StartPoint.SequenceIndex
          Coordinates = coordinates }

    let private diagnosticAssumption name =
        CouplingHypothesis $"{name} is a formal diagnostic from the ISD coupling hypotheses and is not yet empirically validated."

    let identity1StabilityCriterion (point: TrajectoryPoint) =
        let assumption = diagnosticAssumption "Identity 1 stability criterion"

        match point.Coordinates.TryFind "alpha", point.Coordinates.TryFind "v" with
        | Some alpha, Some velocity when isUsable alpha && isUsable velocity ->
            { Kind = StabilityCriterion
              Value =
                Domain.measured
                    (alpha.Value - velocity.Value)
                    (HypothesisDependent [ assumption ])
                    (combineSources [ alpha; velocity ])
                    ([ assumption ] @ combineAssumptions [ alpha; velocity ] |> List.distinct)
              Inputs = [ "alpha"; "v" ]
              Description = "Diagnostic stability margin alpha - v." }
        | _ ->
            { Kind = StabilityCriterion
              Value = Domain.refused "Identity 1 requires usable alpha and velocity coordinates."
              Inputs = [ "alpha"; "v" ]
              Description = "Diagnostic stability margin alpha - v." }

    let identity2CumulativeLeakage (points: TrajectoryPoint list) =
        let assumption = diagnosticAssumption "Identity 2 cumulative leakage functional"

        let deltas =
            points
            |> List.choose (fun point -> point.Coordinates.TryFind "delta")

        if deltas.Length <> points.Length || deltas |> List.exists (isUsable >> not) then
            { Kind = CumulativeLeakageFunctional
              Value = Domain.refused "Identity 2 requires usable delta coordinates for every trajectory point."
              Inputs = [ "delta" ]
              Description = "Diagnostic cumulative leakage as sum(abs(delta))." }
        else
            { Kind = CumulativeLeakageFunctional
              Value =
                Domain.measured
                    (deltas |> List.sumBy (fun delta -> abs delta.Value))
                    (HypothesisDependent [ assumption ])
                    (combineSources deltas)
                    ([ assumption ] @ combineAssumptions deltas |> List.distinct)
              Inputs = [ "delta" ]
              Description = "Diagnostic cumulative leakage as sum(abs(delta))." }

    let identity3TorsionalAccumulation (points: TrajectoryPoint list) =
        let assumption = diagnosticAssumption "Identity 3 torsional accumulation"

        let torsionOrCurvature =
            points
            |> List.choose (fun point ->
                point.Coordinates.TryFind "tau"
                |> Option.orElseWith (fun () -> point.Coordinates.TryFind "kappa"))

        if torsionOrCurvature.Length <> points.Length || torsionOrCurvature |> List.exists (isUsable >> not) then
            { Kind = TorsionalAccumulation
              Value = Domain.refused "Identity 3 requires usable tau coordinates, or kappa fallback coordinates, for every trajectory point."
              Inputs = [ "tau"; "kappa" ]
              Description = "Diagnostic torsional accumulation as sum(abs(tau)) with kappa fallback." }
        else
            { Kind = TorsionalAccumulation
              Value =
                Domain.measured
                    (torsionOrCurvature |> List.sumBy (fun value -> abs value.Value))
                    (HypothesisDependent [ assumption ])
                    (combineSources torsionOrCurvature)
                    ([ assumption ] @ combineAssumptions torsionOrCurvature |> List.distinct)
              Inputs = [ "tau"; "kappa" ]
              Description = "Diagnostic torsional accumulation as sum(abs(tau)) with kappa fallback." }

    let formalIdentityDiagnostics points =
        match points with
        | [] -> []
        | first :: _ ->
            [ identity1StabilityCriterion first
              identity2CumulativeLeakage points
              identity3TorsionalAccumulation points ]

    let measuredTorsionalResistanceFromEscapeThreshold turnId escapeThreshold =
        if System.Double.IsNaN escapeThreshold || System.Double.IsInfinity escapeThreshold || escapeThreshold <= 0.0 then
            Domain.refused "Measured torsional resistance requires a positive finite escape threshold."
        else
            Domain.measured
                escapeThreshold
                DirectObservation
                [ FromTurn turnId ]
                []

    let estimatedTorsionalResistance turnId kind value description =
        let assumption =
            match kind with
            | MeasuredEscapeThreshold -> None
            | QualitativeEstimate -> Some(CouplingHypothesis $"Qualitative torsional resistance estimate: {description}")
            | ArchitecturallyInheritedEstimate -> Some(CouplingHypothesis $"Architecturally inherited torsional resistance estimate: {description}")

        match kind, assumption with
        | MeasuredEscapeThreshold, _ -> measuredTorsionalResistanceFromEscapeThreshold turnId value
        | _, Some flag ->
            if System.Double.IsNaN value || System.Double.IsInfinity value || value < 0.0 then
                Domain.refused "Estimated torsional resistance requires a non-negative finite value."
            else
                Domain.measured
                    value
                    (HypothesisDependent [ flag ])
                    [ FromTurn turnId ]
                    [ flag ]
        | _ -> Domain.refused "Unsupported torsional resistance estimate."

    let attractorEventFromTorsionalResistance turnId description kind torsionalResistance =
        { TurnId = turnId
          Description = description
          TorsionalResistance = Some torsionalResistance
          TorsionalResistanceKind = kind
          Assumptions = torsionalResistance.Assumptions }

    let detectAttractorApproach thresholds (points: TrajectoryPoint list) =
        match points with
        | [] -> None
        | [ _ ] -> None
        | _ ->
            let lastPoint = points |> List.last
            let stability = identity1StabilityCriterion lastPoint
            let torsion = identity3TorsionalAccumulation points

            if isUsable stability.Value
               && isUsable torsion.Value
               && stability.Value.Value <= thresholds.StabilityMarginMaximum
               && torsion.Value.Value >= thresholds.TorsionalAccumulationMinimum then
                let assumption =
                    CouplingHypothesis "Attractor approach detection is a diagnostic signature based on stability margin and torsional accumulation thresholds."

                let tau =
                    Domain.measured
                        torsion.Value.Value
                        (HypothesisDependent [ assumption ])
                        torsion.Value.Sources
                        ([ assumption ] @ torsion.Value.Assumptions |> List.distinct)

                Some
                    { TurnId = lastPoint.TurnId
                      Description =
                        sprintf
                            "Attractor-approach signature: stability margin %.3f, torsional accumulation %.3f."
                            stability.Value.Value
                            torsion.Value.Value
                      TorsionalResistance = Some tau
                      TorsionalResistanceKind = QualitativeEstimate
                      Assumptions = tau.Assumptions }
            else
                None

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
          // The current calibration protocol exposes one local-validity upper bound; keep
          // LambdaMaxDelta tied to it until a distinct homogeneity scan bound is added.
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

    let buildDefaultNaturalContinuationPrompt (request: NaturalPromptBuildRequest) =
        let promptText =
            request.Plan.ContextPrefix.PromptResponseHistory
            |> List.tryLast
            |> Option.map (fun (prompt, _) -> prompt.Text)
            |> Option.defaultValue ""

        if System.String.IsNullOrWhiteSpace promptText then
            Error(ProtocolValidationFailure "Natural continuation prompt requires a non-empty context prefix.")
        else
            Ok(Domain.prompt $"{request.Plan.Label}-natural-prompt" promptText)

    let executeNaturalContinuation metric (builder: INaturalPromptBuilder) (executor: IPromptExecutor) (plan: NaturalContinuationPlan) =
        async {
            let buildRequest: NaturalPromptBuildRequest = { Plan = plan }

            match builder.Build buildRequest with
            | Error error -> return NaturalExecutionFailed(plan.Label, error)
            | Ok prompt ->
                let executionRequest =
                    { Label = plan.Label
                      ContextPrefix = plan.ContextPrefix
                      Prompt = prompt
                      Strategy = Natural
                      BatchId = Some plan.BatchId }

                let! execution = executor.Submit executionRequest

                match execution with
                | Error error -> return NaturalExecutionFailed(plan.Label, error)
                | Ok result ->
                    let previousPrompt, previousResponse =
                        plan.ContextPrefix.PromptResponseHistory
                        |> List.tryLast
                        |> Option.defaultValue (prompt, Domain.response $"{plan.Label}-empty-prefix-response" "")

                    let previousTurn =
                        Domain.turn
                            $"{plan.Label}-previous"
                            (let (SessionId value) = plan.ContextPrefix.SessionId in value)
                            plan.ContextPrefix.ThroughSequenceIndex
                            previousPrompt
                            previousResponse
                            result.ObservedAt
                            Natural
                            0

                    let currentTurn =
                        Domain.turn
                            $"{plan.BatchId}-{plan.Label}"
                            (let (SessionId value) = plan.ContextPrefix.SessionId in value)
                            (plan.ContextPrefix.ThroughSequenceIndex + 1)
                            prompt
                            result.Response
                            result.ObservedAt
                            Natural
                            0

                    let velocity = velocityFromTurnPair metric previousTurn currentTurn

                    return
                        NaturalExecuted
                            { PlanLabel = plan.Label
                              Turn = currentTurn
                              Velocity = velocity }
        }

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

    let runForkedReplicationPlan
        metric
        (naturalBuilder: INaturalPromptBuilder)
        (shockBuilder: IShockPromptBuilder)
        (executor: IPromptExecutor)
        candidateTokens
        (plan: ForkedReplicationPlan)
        =
        async {
            let! naturalResults =
                plan.NaturalContinuations
                |> List.map (executeNaturalContinuation metric naturalBuilder executor)
                |> Async.Parallel

            let! shockResults =
                plan.ShockTrials
                |> List.map (executeShockTrial metric shockBuilder executor candidateTokens)
                |> Async.Parallel

            let successfulNaturals =
                naturalResults
                |> Array.toList
                |> List.choose (function
                    | NaturalExecuted result -> Some result
                    | NaturalExecutionFailed _ -> None)

            let successfulShocks =
                shockResults
                |> Array.toList
                |> List.choose (function
                    | ShockExecuted result -> Some result
                    | ShockExecutionRefused _
                    | ShockExecutionFailed _ -> None)

            let warnings =
                [ yield! plan.Warnings
                  yield!
                      naturalResults
                      |> Array.choose (function
                          | NaturalExecutionFailed(label, error) -> Some $"Natural continuation {label} failed: {error}."
                          | NaturalExecuted _ -> None)
                  yield!
                      shockResults
                      |> Array.choose (function
                          | ShockExecutionRefused(label, refusal) -> Some $"Shock trial {label} refused: {refusal.Basis}."
                          | ShockExecutionFailed(label, error) -> Some $"Shock trial {label} failed: {error}."
                          | ShockExecuted _ -> None) ]

            let results =
                { Plan = plan
                  NaturalContinuations = successfulNaturals
                  ShockTrials = successfulShocks }

            return
                { Plan = plan
                  Results = results
                  NaturalExecutionResults = naturalResults |> Array.toList
                  ShockExecutionResults = shockResults |> Array.toList
                  Warnings = warnings }
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
