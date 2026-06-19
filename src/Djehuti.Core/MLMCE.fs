namespace Djehuti.Core

open System

type ParticipantId = ParticipantId of string
type ModeratorEventId = ModeratorEventId of string
type InterferometerBatchId = InterferometerBatchId of string

type Participant =
    { Id: ParticipantId
      ModelId: ModelId
      RoleLabel: string }

type TurnTakingMode =
    | Sequential
    | Prompted
    | Broadcast

type MLMCESessionKind =
    | SequentialDialogue
    | ForkedInterferometerRun

type InterventionThresholds =
    { StabilityCriterionMargin: float
      LeakageBudgetFraction: float
      TorsionalAccumulationCeiling: float
      AttractorWindow: int
      DivergenceThreshold: float }

type ModeratorTriggerCondition =
    | AttractorApproach
    | LeakageThreshold
    | StabilityCriterion
    | TorsionalAccumulation
    | Manual

type TerminationCondition =
    | MaximumTurnCountReached
    | IrreversibleAttractorCapture
    | LeakageBudgetExhausted
    | ManualTermination

type MLMCESession =
    { Id: SessionId
      Participants: Participant list
      ModeratorModelId: ModelId
      ModeratorInitializationProfileVersion: string
      TurnTakingMode: TurnTakingMode
      SeedPrompt: Prompt
      InterventionThresholds: InterventionThresholds
      SessionKind: MLMCESessionKind
      StartedAt: DateTimeOffset
      EndedAt: DateTimeOffset option
      TerminationCondition: TerminationCondition option }

type ParticipantTurn =
    { Turn: Turn
      ParticipantId: ParticipantId
      PromptSourceParticipantId: ParticipantId option
      PrecededByModeratorIntervention: bool }

type ModeratorEvent =
    { Id: ModeratorEventId
      SessionId: SessionId
      SequenceIndex: int
      TriggerCondition: ModeratorTriggerCondition
      ObservableVectorAtTrigger: ObservableVector
      Thresholds: InterventionThresholds
      ShockPrompt: Prompt
      TargetParticipantId: ParticipantId
      PostShockVelocity: MeasuredValue option }

type InterferometerDeltaAtTurn =
    { SequenceIndex: int
      LeftSessionId: SessionId
      RightSessionId: SessionId
      Delta: TrajectoryDelta
      DivergentComponents: string list }

type InterferometerBatch =
    { Id: InterferometerBatchId
      SharedSeedPrompt: Prompt
      SessionIds: SessionId list
      DeltaPsiMatrix: InterferometerDeltaAtTurn list }

module MLMCE =
    let private requireText fieldName value =
        if String.IsNullOrWhiteSpace value then
            invalidArg fieldName "Value must not be blank."

        value

    let defaultThresholds =
        { StabilityCriterionMargin = 0.10
          LeakageBudgetFraction = 0.80
          TorsionalAccumulationCeiling = 1.0
          AttractorWindow = 3
          DivergenceThreshold = 0.25 }

    let participant id modelId roleLabel =
        { Id = ParticipantId(requireText (nameof id) id)
          ModelId = ModelId(requireText (nameof modelId) modelId)
          RoleLabel = requireText (nameof roleLabel) roleLabel }

    let session id participants moderatorModelId moderatorProfileVersion mode seedPrompt thresholds kind startedAt =
        if List.isEmpty participants then
            invalidArg (nameof participants) "MLMCE session requires at least one participant."

        if thresholds.AttractorWindow < 1 then
            invalidArg (nameof thresholds) "Attractor window must be at least one turn."

        { Id = SessionId(requireText (nameof id) id)
          Participants = participants
          ModeratorModelId = ModelId(requireText (nameof moderatorModelId) moderatorModelId)
          ModeratorInitializationProfileVersion = requireText (nameof moderatorProfileVersion) moderatorProfileVersion
          TurnTakingMode = mode
          SeedPrompt = seedPrompt
          InterventionThresholds = thresholds
          SessionKind = kind
          StartedAt = startedAt
          EndedAt = None
          TerminationCondition = None }

    let terminate condition endedAt (session: MLMCESession) =
        { session with
            EndedAt = Some endedAt
            TerminationCondition = Some condition }

    let seedTurn (session: MLMCESession) (participant: Participant) response timestamp =
        let (SessionId sessionId) = session.Id
        let (ParticipantId participantId) = participant.Id

        Domain.turn
            $"seed-{sessionId}-{participantId}"
            sessionId
            0
            session.SeedPrompt
            response
            timestamp
            Seed
            0

    let participantTurn turn participantId promptSourceParticipantId precededByModeratorIntervention =
        { Turn = turn
          ParticipantId = participantId
          PromptSourceParticipantId = promptSourceParticipantId
          PrecededByModeratorIntervention = precededByModeratorIntervention }

    let moderatorEvent id sessionId sequenceIndex condition vector thresholds shockPrompt targetParticipantId postShockVelocity =
        if sequenceIndex < 0 then
            invalidArg (nameof sequenceIndex) "Moderator event sequence index must not be negative."

        { Id = ModeratorEventId(requireText (nameof id) id)
          SessionId = sessionId
          SequenceIndex = sequenceIndex
          TriggerCondition = condition
          ObservableVectorAtTrigger = vector
          Thresholds = thresholds
          ShockPrompt = shockPrompt
          TargetParticipantId = targetParticipantId
          PostShockVelocity = postShockVelocity }

    let private vectorsBySequence (items: (SessionId * int * ObservableVector) list) =
        items
        |> List.groupBy (fun (_, sequenceIndex, _) -> sequenceIndex)
        |> List.map (fun (sequenceIndex, values) ->
            sequenceIndex,
            values |> List.map (fun (sessionId, _, vector) -> sessionId, vector))

    let private divergentComponents threshold (delta: TrajectoryDelta) =
        delta.Components
        |> List.choose (fun part ->
            if abs part.Difference.Value >= threshold then
                Some part.Name
            else
                None)

    let computeInterferometerDeltas threshold sessionVectors =
        vectorsBySequence sessionVectors
        |> List.collect (fun (sequenceIndex, vectors) ->
            [ for leftIndex in 0 .. vectors.Length - 1 do
                  for rightIndex in leftIndex + 1 .. vectors.Length - 1 do
                      let leftSessionId, left = vectors[leftIndex]
                      let rightSessionId, right = vectors[rightIndex]
                      let delta = Measurement.deltaPsi left right

                      { SequenceIndex = sequenceIndex
                        LeftSessionId = leftSessionId
                        RightSessionId = rightSessionId
                        Delta = delta
                        DivergentComponents = divergentComponents threshold delta } ])

    let interferometerBatch id sharedSeedPrompt sessionIds deltaPsiMatrix =
        if List.length sessionIds < 2 then
            invalidArg (nameof sessionIds) "Interferometer batch requires at least two sessions."

        { Id = InterferometerBatchId(requireText (nameof id) id)
          SharedSeedPrompt = sharedSeedPrompt
          SessionIds = sessionIds
          DeltaPsiMatrix = deltaPsiMatrix }
