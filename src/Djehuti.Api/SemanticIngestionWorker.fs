module Djehuti.Api.SemanticIngestionWorker

open System
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Djehuti.Api

let private envFlag (name: string) (fallback: bool) =
    match Environment.GetEnvironmentVariable(name) with
    | null
    | "" -> fallback
    | value ->
        match Boolean.TryParse(value.Trim()) with
        | true, parsed -> parsed
        | _ -> fallback

let private envInt (name: string) (fallback: int) =
    match Environment.GetEnvironmentVariable(name) with
    | null
    | "" -> fallback
    | value ->
        match Int32.TryParse(value.Trim()) with
        | true, parsed when parsed >= 0 -> parsed
        | _ -> fallback

let private hasWork (summary: SemanticGraphRepository.SemanticBackgroundSyncSummary) =
    summary.ForumThreadsRequested > 0
    || summary.BlogArticlesRequested > 0
    || summary.MudRoomsRequested > 0
    || summary.MudItemsRequested > 0
    || summary.MudRecipesRequested > 0

[<CLIMutable>]
type SemanticAutomationStatus =
    { SyncEnabled: bool
      SyncIntervalSeconds: int
      AutoSplitEnabled: bool
      AutoSplitIntervalSeconds: int
      AutoSplitLimit: int
      AutoSplitMinChunkCount: int
      AutoSplitScopeKind: string option
      GraphBackfillLimit: int
      ConsecutiveDbFailures: int
      LastSyncAt: DateTimeOffset option
      LastSplitAt: DateTimeOffset option
      LastGraphBackfillCount: int
      LastAutoSplitCreatedCount: int
      LastAutoSplitProposalCount: int }

let mutable private currentStatus =
    { SyncEnabled = true
      SyncIntervalSeconds = 300
      AutoSplitEnabled = false
      AutoSplitIntervalSeconds = 3600
      AutoSplitLimit = 12
      AutoSplitMinChunkCount = 3
      AutoSplitScopeKind = None
      GraphBackfillLimit = 8
      ConsecutiveDbFailures = 0
      LastSyncAt = None
      LastSplitAt = None
      LastGraphBackfillCount = 0
      LastAutoSplitCreatedCount = 0
      LastAutoSplitProposalCount = 0 }

let getStatus () = currentStatus

type SemanticIngestionWorker(logger: ILogger<SemanticIngestionWorker>) =
    inherit BackgroundService()
    let mutable consecutiveDbFailures = 0
    let mutable lastAutoSplitAt : DateTimeOffset option = None

    override _.ExecuteAsync(ct) =
        task {
            logger.LogInformation("SemanticIngestionWorker starting")

            let enabled = envFlag "SEMANTIC_SYNC_ENABLED" true
            let intervalSeconds = Math.Max(envInt "SEMANTIC_SYNC_INTERVAL_SECONDS" 300, 30)
            let forumLimit = envInt "SEMANTIC_SYNC_FORUM_LIMIT" 24
            let blogLimit = envInt "SEMANTIC_SYNC_BLOG_LIMIT" 24
            let mudRoomLimit = envInt "SEMANTIC_SYNC_MUD_ROOM_LIMIT" 32
            let mudItemLimit = envInt "SEMANTIC_SYNC_MUD_ITEM_LIMIT" 64
            let mudRecipeLimit = envInt "SEMANTIC_SYNC_MUD_RECIPE_LIMIT" 32
            let graphBackfillLimit = envInt "SEMANTIC_GRAPH_BACKFILL_LIMIT" 8
            let autoSplitEnabled = envFlag "SEMANTIC_AUTO_SPLIT_ENABLED" false
            let autoSplitIntervalSeconds = Math.Max(envInt "SEMANTIC_AUTO_SPLIT_INTERVAL_SECONDS" 3600, 60)
            let autoSplitLimit = Math.Max(envInt "SEMANTIC_AUTO_SPLIT_LIMIT" 12, 1)
            let autoSplitMinChunkCount = Math.Max(envInt "SEMANTIC_AUTO_SPLIT_MIN_CHUNK_COUNT" 3, 1)
            let autoSplitScopeKind =
                match Environment.GetEnvironmentVariable("SEMANTIC_AUTO_SPLIT_SCOPE_KIND") with
                | null
                | "" -> None
                | value -> Some(value.Trim().ToLowerInvariant())

            currentStatus <-
                { currentStatus with
                    SyncEnabled = enabled
                    SyncIntervalSeconds = intervalSeconds
                    AutoSplitEnabled = autoSplitEnabled
                    AutoSplitIntervalSeconds = autoSplitIntervalSeconds
                    AutoSplitLimit = autoSplitLimit
                    AutoSplitMinChunkCount = autoSplitMinChunkCount
                    AutoSplitScopeKind = autoSplitScopeKind
                    GraphBackfillLimit = graphBackfillLimit }

            while not ct.IsCancellationRequested do
                try
                    if enabled then
                        let now = DateTimeOffset.UtcNow
                        let summary =
                            SemanticGraphRepository.runBackgroundSync
                                forumLimit
                                blogLimit
                                mudRoomLimit
                                mudItemLimit
                                mudRecipeLimit

                        let shouldAutoSplit =
                            autoSplitEnabled
                            &&
                            match lastAutoSplitAt with
                            | None -> true
                            | Some lastRun -> (now - lastRun).TotalSeconds >= float autoSplitIntervalSeconds

                        let autoSplitCreated, autoSplitProposalsApplied =
                            if shouldAutoSplit then
                                let created, proposalsApplied =
                                    SemanticGraphRepository.applyTokenSplitProposals autoSplitLimit autoSplitMinChunkCount autoSplitScopeKind

                                if proposalsApplied > 0 then
                                    logger.LogInformation(
                                        "Semantic auto-split pass created {CreatedCount} variants across {ProposalCount} proposals",
                                        created,
                                        proposalsApplied)

                                lastAutoSplitAt <- Some now
                                created, proposalsApplied
                            else
                                0, 0

                        let graphBackfilled =
                            let initialBackfill = SemanticGraphRepository.backfillGraphChunks graphBackfillLimit
                            if autoSplitCreated > 0 || autoSplitProposalsApplied > 0 then
                                initialBackfill + SemanticGraphRepository.backfillGraphChunks graphBackfillLimit
                            else
                                initialBackfill

                        if hasWork summary then
                            logger.LogInformation(
                                "Semantic sync pass complete. Forum {ForumIndexed}/{ForumRequested}, blog {BlogIndexed}/{BlogRequested}, MUD rooms {RoomsIndexed}/{RoomsRequested}, MUD items {ItemsIndexed}/{ItemsRequested}, MUD recipes {RecipesIndexed}/{RecipesRequested}",
                                summary.ForumThreadsIndexed,
                                summary.ForumThreadsRequested,
                                summary.BlogArticlesIndexed,
                                summary.BlogArticlesRequested,
                                summary.MudRoomsIndexed,
                                summary.MudRoomsRequested,
                                summary.MudItemsIndexed,
                                summary.MudItemsRequested,
                                summary.MudRecipesIndexed,
                                summary.MudRecipesRequested)

                        if graphBackfilled > 0 then
                            logger.LogInformation(
                                "Semantic graph backfill rebuilt {ChunkCount} chunk graphs",
                                graphBackfilled)

                        consecutiveDbFailures <- 0
                        currentStatus <-
                            { currentStatus with
                                ConsecutiveDbFailures = 0
                                LastSyncAt = Some now
                                LastSplitAt =
                                    match lastAutoSplitAt with
                                    | Some timestamp when autoSplitCreated > 0 || autoSplitProposalsApplied > 0 -> Some timestamp
                                    | _ -> currentStatus.LastSplitAt
                                LastGraphBackfillCount = graphBackfilled
                                LastAutoSplitCreatedCount = autoSplitCreated
                                LastAutoSplitProposalCount = autoSplitProposalsApplied }

                    do! Task.Delay(TimeSpan.FromSeconds(float intervalSeconds), ct)
                with
                | :? TaskCanceledException -> ()
                | ex ->
                    if WorkerResilience.isDatabaseException ex then
                        consecutiveDbFailures <- consecutiveDbFailures + 1
                        currentStatus <- { currentStatus with ConsecutiveDbFailures = consecutiveDbFailures }
                        let delay = WorkerResilience.backoffDelay consecutiveDbFailures
                        logger.LogWarning(ex, "SemanticIngestionWorker database failure. Backing off for {DelaySeconds} seconds (attempt {Attempt})", int delay.TotalSeconds, consecutiveDbFailures)
                        do! Task.Delay(delay, ct)
                    else
                        logger.LogError(ex, "SemanticIngestionWorker iteration error")
                        do! Task.Delay(WorkerResilience.shortRecoveryDelay, ct)

            return ()
        }
