namespace Djehuti.Core

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

type LogicalTime =
    | LogicalTime of int

type ObservationClock =
    { SequenceIndex: LogicalTime
      ObservedAt: DateTimeOffset option }

type DataSourceId = DataSourceId of string

type DataSourceKind =
    | LiveProvider
    | ReplayFile
    | BenchmarkHarness
    | ManualTranscript
    | MessageQueue
    | UnknownSource of string

type DataSourceDescriptor =
    { Id: DataSourceId
      Kind: DataSourceKind
      Name: string
      Metadata: Map<string, string> }

type InteractionRecord =
    { TurnId: TurnId
      SessionId: SessionId
      ModelId: ModelId
      Clock: ObservationClock
      Prompt: Prompt
      Response: Response
      Strategy: SamplingStrategy
      PriorShockCount: int
      Source: DataSourceDescriptor
      Metadata: Map<string, string> }

type IngestionEvent =
    | SessionStarted of Session
    | InteractionObserved of InteractionRecord
    | ShockTrialObserved of ShockTrial
    | ForkedReplicationBatchObserved of ForkedReplicationBatch
    | CalibrationObserved of CalibrationRecord
    | ObservableVectorObserved of ObservableVector
    | AttractorEventObserved of AttractorEvent
    | SessionEnded of SessionId * endedAt: DateTimeOffset option

type IngestionGap =
    { SessionId: SessionId
      PreviousSequenceIndex: int
      CurrentSequenceIndex: int }

type IngestionSummary =
    { SessionsStarted: int
      InteractionsObserved: int
      ShockTrialsObserved: int
      ForkedReplicationBatchesObserved: int
      CalibrationsObserved: int
      ObservableVectorsObserved: int
      AttractorEventsObserved: int
      SessionsEnded: int
      Gaps: IngestionGap list }

type IDataSource =
    abstract Descriptor: DataSourceDescriptor
    abstract Read: CancellationToken -> IAsyncEnumerable<IngestionEvent>

module Ingestion =
    let emptySummary =
        { SessionsStarted = 0
          InteractionsObserved = 0
          ShockTrialsObserved = 0
          ForkedReplicationBatchesObserved = 0
          CalibrationsObserved = 0
          ObservableVectorsObserved = 0
          AttractorEventsObserved = 0
          SessionsEnded = 0
          Gaps = [] }

    let private sourceIdText (DataSourceId value) =
        value

    let private sourceKindText kind =
        match kind with
        | LiveProvider -> "live-provider"
        | ReplayFile -> "replay-file"
        | BenchmarkHarness -> "benchmark-harness"
        | ManualTranscript -> "manual-transcript"
        | MessageQueue -> "message-queue"
        | UnknownSource value -> value

    let private sequenceValue (LogicalTime value) =
        value

    let private mergeMetadata source sequenceIndex metadata =
        metadata
        |> Map.add "data_source_id" (sourceIdText source.Id)
        |> Map.add "data_source_kind" (sourceKindText source.Kind)
        |> Map.add "data_source_name" source.Name
        |> Map.add "logical_time" (string sequenceIndex)

    let private incrementSessionStarted summary =
        { summary with SessionsStarted = summary.SessionsStarted + 1 }

    let private incrementInteraction gap summary =
        { summary with
            InteractionsObserved = summary.InteractionsObserved + 1
            Gaps =
                match gap with
                | Some value -> value :: summary.Gaps
                | None -> summary.Gaps }

    let private incrementShockTrial summary =
        { summary with ShockTrialsObserved = summary.ShockTrialsObserved + 1 }

    let private incrementForkedBatch summary =
        { summary with ForkedReplicationBatchesObserved = summary.ForkedReplicationBatchesObserved + 1 }

    let private incrementCalibration summary =
        { summary with CalibrationsObserved = summary.CalibrationsObserved + 1 }

    let private incrementObservableVector summary =
        { summary with ObservableVectorsObserved = summary.ObservableVectorsObserved + 1 }

    let private incrementAttractorEvent summary =
        { summary with AttractorEventsObserved = summary.AttractorEventsObserved + 1 }

    let private incrementSessionEnded summary =
        { summary with SessionsEnded = summary.SessionsEnded + 1 }

    let private validateSequence sessionId sequenceIndex =
        storage {
            let! existingTurns = StorageOps.listTurnsForSession sessionId

            match existingTurns |> List.tryFind (fun turn -> turn.SequenceIndex = sequenceIndex) with
            | Some _ ->
                return!
                    Storage.error (
                        Conflict(
                            "Turn",
                            $"{sessionId}/{sequenceIndex}",
                            "A turn with this session and logical time already exists."
                        )
                    )
            | None ->
                match existingTurns |> List.tryLast with
                | Some previous when sequenceIndex < previous.SequenceIndex ->
                    return!
                        Storage.error (
                            ValidationFailure(
                                $"Incoming turn sequence {sequenceIndex} is earlier than existing sequence {previous.SequenceIndex}."
                            )
                        )
                | Some previous when sequenceIndex > previous.SequenceIndex + 1 ->
                    return
                        Some
                            { SessionId = sessionId
                              PreviousSequenceIndex = previous.SequenceIndex
                              CurrentSequenceIndex = sequenceIndex }
                | _ -> return None
        }

    let private ensureSession (record: InteractionRecord) =
        storage {
            let! session = StorageOps.getSession record.SessionId

            match session with
            | Some _ -> return ()
            | None ->
                let timestamp = record.Clock.ObservedAt |> Option.defaultWith (fun () -> DateTimeOffset.MinValue)

                do!
                    StorageOps.saveSession
                        { Id = record.SessionId
                          ModelId = record.ModelId
                          StartedAt = timestamp
                          EndedAt = None
                          SystemPrompt = None
                          Turns = [] }
        }

    let private turnFromRecord fallbackTimestamp (record: InteractionRecord) =
        let sequenceIndex = sequenceValue record.Clock.SequenceIndex
        let timestamp = record.Clock.ObservedAt |> Option.defaultValue fallbackTimestamp

        { Id = record.TurnId
          SessionId = record.SessionId
          SequenceIndex = sequenceIndex
          Prompt = record.Prompt
          Response = record.Response
          Timestamp = timestamp
          Strategy = record.Strategy
          ContaminationDepth = Domain.contaminationDepth record.Strategy record.PriorShockCount
          Metadata = mergeMetadata record.Source sequenceIndex record.Metadata }

    let ingestEvent event =
        storage {
            match event with
            | SessionStarted session ->
                do! StorageOps.saveSession session
                return incrementSessionStarted emptySummary

            | InteractionObserved record ->
                let sequenceIndex = sequenceValue record.Clock.SequenceIndex

                if sequenceIndex < 0 then
                    return!
                        Storage.error (
                            ValidationFailure $"Logical time must not be negative. Received {sequenceIndex}."
                        )
                else
                    do! ensureSession record
                    let! gap = validateSequence record.SessionId sequenceIndex
                    let! context = Storage.ask
                    let turn = turnFromRecord (context.Clock()) record
                    do! StorageOps.saveTurn turn
                    return incrementInteraction gap emptySummary

            | ShockTrialObserved trial ->
                do! StorageOps.saveShockTrial trial
                return incrementShockTrial emptySummary

            | ForkedReplicationBatchObserved batch ->
                do! StorageOps.saveForkedReplicationBatch batch
                return incrementForkedBatch emptySummary

            | CalibrationObserved record ->
                do! StorageOps.saveCalibrationRecord record
                return incrementCalibration emptySummary

            | ObservableVectorObserved vector ->
                do! StorageOps.saveObservableVector vector
                return incrementObservableVector emptySummary

            | AttractorEventObserved event ->
                do! StorageOps.saveAttractorEvent event
                return incrementAttractorEvent emptySummary

            | SessionEnded(sessionId, endedAt) ->
                let! stored = StorageOps.getSession sessionId

                match stored with
                | None -> return! Storage.error (NotFound("Session", string sessionId))
                | Some session ->
                    let! context = Storage.ask
                    do! StorageOps.saveSession { session with EndedAt = Some(defaultArg endedAt (context.Clock())) }
                    return incrementSessionEnded emptySummary
        }

    let combineSummaries left right =
        { SessionsStarted = left.SessionsStarted + right.SessionsStarted
          InteractionsObserved = left.InteractionsObserved + right.InteractionsObserved
          ShockTrialsObserved = left.ShockTrialsObserved + right.ShockTrialsObserved
          ForkedReplicationBatchesObserved = left.ForkedReplicationBatchesObserved + right.ForkedReplicationBatchesObserved
          CalibrationsObserved = left.CalibrationsObserved + right.CalibrationsObserved
          ObservableVectorsObserved = left.ObservableVectorsObserved + right.ObservableVectorsObserved
          AttractorEventsObserved = left.AttractorEventsObserved + right.AttractorEventsObserved
          SessionsEnded = left.SessionsEnded + right.SessionsEnded
          Gaps = left.Gaps @ right.Gaps }

    let ingestEvents events =
        events
        |> Seq.fold
            (fun accumulated event ->
                storage {
                    let! summary = accumulated
                    let! eventSummary = ingestEvent event
                    return combineSummaries summary eventSummary
                })
            (Storage.result emptySummary)

    let ingestAsyncEvents (events: IAsyncEnumerable<IngestionEvent>) (cancellationToken: CancellationToken) =
        Storage(fun context ->
            async {
                let enumerator = events.GetAsyncEnumerator cancellationToken

                let rec loop summary =
                    async {
                        let! hasNext =
                            enumerator.MoveNextAsync().AsTask()
                            |> Async.AwaitTask

                        if hasNext then
                            let! eventResult =
                                ingestEvent enumerator.Current
                                |> Storage.run context

                            match eventResult with
                            | Ok eventSummary ->
                                return! loop (combineSummaries summary eventSummary)
                            | Error error -> return Error error
                        else
                            return Ok summary
                    }

                let! result = loop emptySummary
                do!
                    enumerator.DisposeAsync().AsTask()
                    |> Async.AwaitTask

                return result
            })

    let ingestDataSource (source: IDataSource) cancellationToken =
        ingestAsyncEvents (source.Read cancellationToken) cancellationToken
