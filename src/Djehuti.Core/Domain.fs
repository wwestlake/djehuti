namespace Djehuti.Core

open System

type SessionId = SessionId of string
type TurnId = TurnId of string
type PromptId = PromptId of string
type ResponseId = ResponseId of string
type ShockTrialId = ShockTrialId of string
type ForkedReplicationBatchId = ForkedReplicationBatchId of string
type CalibrationRecordId = CalibrationRecordId of string
type ContextClassId = ContextClassId of string
type ModelId = ModelId of string

type Prompt =
    { Id: PromptId
      Text: string
      Metadata: Map<string, string> }

type Response =
    { Id: ResponseId
      Text: string
      Metadata: Map<string, string> }

type TextSample =
    | PromptSample of Prompt
    | ResponseSample of Response

type TextComparison =
    | PromptToResponse of Prompt * Response
    | PromptToPrompt of Prompt * Prompt
    | ResponseToResponse of Response * Response
    | StateTransition of TurnId * TurnId

type SamplingStrategy =
    | Natural
    | Shock
    | InterleavedWithHistory

type ContaminationDepth =
    | Clean
    | Contaminated of priorShockCount: int

type AssumptionFlag =
    | ContinuityOfPerturbationVelocity
    | IsotropicEmbeddingCurvatureApproximation
    | CouplingHypothesis of name: string
    | ProviderSuppliedMetadata of name: string
    | GlobalCalibrationFallback
    | LocalCalibrationAssumption of contextClass: ContextClassId
    | TokenGranularityAggregation of statistic: string

type MeasurementBasis =
    | DirectObservation
    | MarginalEstimate
    | GlobalCalibrationEstimate
    | LocalCalibrationEstimate of ContextClassId
    | HypothesisDependent of AssumptionFlag list
    | ContaminatedTrajectory of ContaminationDepth
    | Refused of reason: string

type MeasurementSource =
    | FromTurn of TurnId
    | FromShockTrial of ShockTrialId
    | FromForkedReplicationBatch of ForkedReplicationBatchId
    | FromCalibrationRecord of CalibrationRecordId
    | FromTextComparison of comparisonId: string

type MeasuredValue =
    { Value: float
      Basis: MeasurementBasis
      Sources: MeasurementSource list
      Assumptions: AssumptionFlag list }

type ObservableComponent =
    { Name: string
      Measurement: MeasuredValue }

type ObservableVector =
    { TurnId: TurnId
      Alpha: MeasuredValue option
      Beta: MeasuredValue option
      Gamma: MeasuredValue option
      Delta: MeasuredValue option
      Velocity: MeasuredValue option
      Curvature: MeasuredValue option
      TorsionalResistance: MeasuredValue option
      Zeta4: MeasuredValue option }

type Turn =
    { Id: TurnId
      SessionId: SessionId
      SequenceIndex: int
      Prompt: Prompt
      Response: Response
      Timestamp: DateTimeOffset
      Strategy: SamplingStrategy
      ContaminationDepth: ContaminationDepth
      Metadata: Map<string, string> }

type Session =
    { Id: SessionId
      ModelId: ModelId
      StartedAt: DateTimeOffset
      EndedAt: DateTimeOffset option
      SystemPrompt: Prompt option
      Turns: Turn list }

type ContextPrefix =
    { SessionId: SessionId
      ThroughSequenceIndex: int
      PromptResponseHistory: (Prompt * Response) list }

type ShockMode =
    | TargetLambda of lambda: float
    | MinimalTokenEdit

type ShockTrial =
    { Id: ShockTrialId
      ContextPrefix: ContextPrefix
      ShockPrompt: Prompt
      Response: Response
      IntensityLambda: float option
      Mode: ShockMode
      ResultingPerturbationVelocity: MeasuredValue option
      ForkedReplicationBatchId: ForkedReplicationBatchId option
      AppliedInline: bool }

type ForkedReplicationMember =
    | NaturalContinuation of Turn
    | ShockContinuation of ShockTrial

type ForkedReplicationBatch =
    { Id: ForkedReplicationBatchId
      ContextClassId: ContextClassId
      SharedPrefix: ContextPrefix
      Members: ForkedReplicationMember list }

type CalibrationMethod =
    | NoiseFloorFromNullTrials
    | LocalValidityFromHomogeneityScan
    | TokenGranularityFromSingleTokenEdits of aggregationStatistic: string
    | ManualCalibration of description: string

type CalibrationEstimate =
    { Value: float
      Method: CalibrationMethod
      SampleSize: int
      Assumptions: AssumptionFlag list }

type CalibrationRecord =
    { Id: CalibrationRecordId
      ContextClassId: ContextClassId
      EstimatedAt: DateTimeOffset
      ValidFrom: DateTimeOffset
      ValidUntil: DateTimeOffset option
      NoiseFloorEpsilon: CalibrationEstimate option
      LambdaMinEpsilon: CalibrationEstimate option
      LocalValidityRadiusDelta: CalibrationEstimate option
      LambdaMaxDelta: CalibrationEstimate option
      TokenGranularityLambdaQuantum: CalibrationEstimate option }

type WindowFeasibility =
    | Feasible of lowerBound: float * upperBound: float
    | Infeasible of lowerBound: float * upperBound: float * recommendation: string
    | MissingCalibration of missingFields: string list

type TorsionalResistanceKind =
    | MeasuredEscapeThreshold
    | QualitativeEstimate
    | ArchitecturallyInheritedEstimate

type AttractorEvent =
    { TurnId: TurnId
      Description: string
      TorsionalResistance: MeasuredValue option
      TorsionalResistanceKind: TorsionalResistanceKind
      Assumptions: AssumptionFlag list }

module Domain =
    let private requireId fieldName value =
        if String.IsNullOrWhiteSpace value then
            invalidArg fieldName "Identifier must not be blank."

        value

    let private cleanText value =
        if isNull value then String.Empty else value

    let prompt id text : Prompt =
        { Id = PromptId(requireId (nameof id) id)
          Text = cleanText text
          Metadata = Map.empty }

    let response id text : Response =
        { Id = ResponseId(requireId (nameof id) id)
          Text = cleanText text
          Metadata = Map.empty }

    let promptWithMetadata id text metadata =
        { prompt id text with Metadata = metadata }

    let responseWithMetadata id text metadata =
        { response id text with Metadata = metadata }

    let contaminationDepth strategy priorShockCount =
        match strategy, priorShockCount with
        | Natural, 0 -> Clean
        | _, count when count <= 0 -> Clean
        | _, count -> Contaminated count

    let turn id sessionId sequenceIndex prompt response timestamp strategy priorShockCount =
        if sequenceIndex < 0 then
            invalidArg (nameof sequenceIndex) "Sequence index must not be negative."

        { Id = TurnId(requireId (nameof id) id)
          SessionId = SessionId(requireId (nameof sessionId) sessionId)
          SequenceIndex = sequenceIndex
          Prompt = prompt
          Response = response
          Timestamp = timestamp
          Strategy = strategy
          ContaminationDepth = contaminationDepth strategy priorShockCount
          Metadata = Map.empty }

    let measured value basis sources assumptions =
        { Value = value
          Basis = basis
          Sources = sources
          Assumptions = assumptions }

    let refused reason =
        { Value = nan
          Basis = Refused reason
          Sources = []
          Assumptions = [] }
