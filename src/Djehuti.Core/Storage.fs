namespace Djehuti.Core

open System
open System.Collections.Generic

type DjehutiError =
    | NotFound of entity: string * id: string
    | Conflict of entity: string * id: string * reason: string
    | ValidationFailure of message: string
    | StorageFailure of message: string

type StorageResult<'a> = Result<'a, DjehutiError>

type ISessionStore =
    abstract Save: Session -> Async<StorageResult<unit>>
    abstract Get: SessionId -> Async<StorageResult<Session option>>

type ITurnStore =
    abstract Save: Turn -> Async<StorageResult<unit>>
    abstract Get: TurnId -> Async<StorageResult<Turn option>>
    abstract ListBySession: SessionId -> Async<StorageResult<Turn list>>

type IShockTrialStore =
    abstract Save: ShockTrial -> Async<StorageResult<unit>>
    abstract Get: ShockTrialId -> Async<StorageResult<ShockTrial option>>

type IForkedReplicationBatchStore =
    abstract Save: ForkedReplicationBatch -> Async<StorageResult<unit>>
    abstract Get: ForkedReplicationBatchId -> Async<StorageResult<ForkedReplicationBatch option>>

type ICalibrationRecordStore =
    abstract Save: CalibrationRecord -> Async<StorageResult<unit>>
    abstract Get: CalibrationRecordId -> Async<StorageResult<CalibrationRecord option>>
    abstract LatestForContextClass: ContextClassId -> Async<StorageResult<CalibrationRecord option>>

type IObservableVectorStore =
    abstract Save: ObservableVector -> Async<StorageResult<unit>>
    abstract GetByTurn: TurnId -> Async<StorageResult<ObservableVector option>>

type IAttractorEventStore =
    abstract Save: AttractorEvent -> Async<StorageResult<unit>>
    abstract ListByTurn: TurnId -> Async<StorageResult<AttractorEvent list>>

type StorageContext =
    { Sessions: ISessionStore
      Turns: ITurnStore
      ShockTrials: IShockTrialStore
      ForkedReplicationBatches: IForkedReplicationBatchStore
      CalibrationRecords: ICalibrationRecordStore
      ObservableVectors: IObservableVectorStore
      AttractorEvents: IAttractorEventStore
      Clock: unit -> DateTimeOffset }

type Storage<'a> =
    | Storage of (StorageContext -> Async<StorageResult<'a>>)

module Storage =
    let run context (Storage operation) =
        operation context

    let result value =
        Storage(fun _ -> async { return Ok value })

    let error error =
        Storage(fun _ -> async { return Error error })

    let bind binder storage =
        Storage(fun context ->
            async {
                let! result = run context storage

                match result with
                | Ok value -> return! run context (binder value)
                | Error error -> return Error error
            })

    let map mapper storage =
        bind (mapper >> result) storage

    let fromAsyncResult operation =
        Storage(fun _ -> operation)

    let ask =
        Storage(fun context -> async { return Ok context })

    let liftContext operation =
        Storage(fun context -> operation context)

type StorageBuilder() =
    member _.Return value = Storage.result value
    member _.ReturnFrom storage = storage
    member _.Bind(storage, binder) = Storage.bind binder storage
    member _.Zero() = Storage.result ()
    member _.Delay(factory) = Storage.bind factory (Storage.result ())

[<AutoOpen>]
module StorageComputationExpression =
    let storage = StorageBuilder()

module StorageOps =
    let saveSession session =
        Storage.liftContext (fun context -> context.Sessions.Save session)

    let getSession id =
        Storage.liftContext (fun context -> context.Sessions.Get id)

    let saveTurn turn =
        Storage.liftContext (fun context -> context.Turns.Save turn)

    let getTurn id =
        Storage.liftContext (fun context -> context.Turns.Get id)

    let listTurnsForSession id =
        Storage.liftContext (fun context -> context.Turns.ListBySession id)

    let saveShockTrial trial =
        Storage.liftContext (fun context -> context.ShockTrials.Save trial)

    let getShockTrial id =
        Storage.liftContext (fun context -> context.ShockTrials.Get id)

    let saveForkedReplicationBatch batch =
        Storage.liftContext (fun context -> context.ForkedReplicationBatches.Save batch)

    let getForkedReplicationBatch id =
        Storage.liftContext (fun context -> context.ForkedReplicationBatches.Get id)

    let saveCalibrationRecord record =
        Storage.liftContext (fun context -> context.CalibrationRecords.Save record)

    let getCalibrationRecord id =
        Storage.liftContext (fun context -> context.CalibrationRecords.Get id)

    let latestCalibrationForContextClass id =
        Storage.liftContext (fun context -> context.CalibrationRecords.LatestForContextClass id)

    let saveObservableVector vector =
        Storage.liftContext (fun context -> context.ObservableVectors.Save vector)

    let getObservableVectorByTurn id =
        Storage.liftContext (fun context -> context.ObservableVectors.GetByTurn id)

    let saveAttractorEvent event =
        Storage.liftContext (fun context -> context.AttractorEvents.Save event)

    let listAttractorEventsByTurn id =
        Storage.liftContext (fun context -> context.AttractorEvents.ListByTurn id)

module InMemoryStorage =
    let private ok value =
        async { return Ok value }

    let private withLock gate operation =
        lock gate operation

    type private SessionStore() =
        let gate = obj()
        let sessions = Dictionary<SessionId, Session>()

        interface ISessionStore with
            member _.Save session =
                withLock gate (fun () -> sessions[session.Id] <- session)
                |> ok

            member _.Get id =
                withLock gate (fun () ->
                    match sessions.TryGetValue id with
                    | true, session -> Some session
                    | false, _ -> None)
                |> ok

    type private TurnStore() =
        let gate = obj()
        let turns = Dictionary<TurnId, Turn>()

        interface ITurnStore with
            member _.Save turn =
                withLock gate (fun () -> turns[turn.Id] <- turn)
                |> ok

            member _.Get id =
                withLock gate (fun () ->
                    match turns.TryGetValue id with
                    | true, turn -> Some turn
                    | false, _ -> None)
                |> ok

            member _.ListBySession sessionId =
                withLock gate (fun () ->
                    turns.Values
                    |> Seq.filter (fun turn -> turn.SessionId = sessionId)
                    |> Seq.sortBy _.SequenceIndex
                    |> Seq.toList)
                |> ok

    type private ShockTrialStore() =
        let gate = obj()
        let trials = Dictionary<ShockTrialId, ShockTrial>()

        interface IShockTrialStore with
            member _.Save trial =
                withLock gate (fun () -> trials[trial.Id] <- trial)
                |> ok

            member _.Get id =
                withLock gate (fun () ->
                    match trials.TryGetValue id with
                    | true, trial -> Some trial
                    | false, _ -> None)
                |> ok

    type private ForkedReplicationBatchStore() =
        let gate = obj()
        let batches = Dictionary<ForkedReplicationBatchId, ForkedReplicationBatch>()

        interface IForkedReplicationBatchStore with
            member _.Save batch =
                withLock gate (fun () -> batches[batch.Id] <- batch)
                |> ok

            member _.Get id =
                withLock gate (fun () ->
                    match batches.TryGetValue id with
                    | true, batch -> Some batch
                    | false, _ -> None)
                |> ok

    type private CalibrationRecordStore() =
        let gate = obj()
        let records = Dictionary<CalibrationRecordId, CalibrationRecord>()

        interface ICalibrationRecordStore with
            member _.Save record =
                withLock gate (fun () -> records[record.Id] <- record)
                |> ok

            member _.Get id =
                withLock gate (fun () ->
                    match records.TryGetValue id with
                    | true, record -> Some record
                    | false, _ -> None)
                |> ok

            member _.LatestForContextClass contextClassId =
                withLock gate (fun () ->
                    records.Values
                    |> Seq.filter (fun record -> record.ContextClassId = contextClassId)
                    |> Seq.sortByDescending _.EstimatedAt
                    |> Seq.tryHead)
                |> ok

    type private ObservableVectorStore() =
        let gate = obj()
        let vectors = Dictionary<TurnId, ObservableVector>()

        interface IObservableVectorStore with
            member _.Save vector =
                withLock gate (fun () -> vectors[vector.TurnId] <- vector)
                |> ok

            member _.GetByTurn turnId =
                withLock gate (fun () ->
                    match vectors.TryGetValue turnId with
                    | true, vector -> Some vector
                    | false, _ -> None)
                |> ok

    type private AttractorEventStore() =
        let gate = obj()
        let events = ResizeArray<AttractorEvent>()

        interface IAttractorEventStore with
            member _.Save event =
                withLock gate (fun () -> events.Add event)
                |> ok

            member _.ListByTurn turnId =
                withLock gate (fun () ->
                    events
                    |> Seq.filter (fun event -> event.TurnId = turnId)
                    |> Seq.toList)
                |> ok

    let createContext clock =
        { Sessions = SessionStore()
          Turns = TurnStore()
          ShockTrials = ShockTrialStore()
          ForkedReplicationBatches = ForkedReplicationBatchStore()
          CalibrationRecords = CalibrationRecordStore()
          ObservableVectors = ObservableVectorStore()
          AttractorEvents = AttractorEventStore()
          Clock = clock }
