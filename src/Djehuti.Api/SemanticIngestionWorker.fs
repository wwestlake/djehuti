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

[<CLIMutable>]
type SemanticAutomationRunResult =
    { ForumThreadsRequested: int
      ForumThreadsIndexed: int
      BlogArticlesRequested: int
      BlogArticlesIndexed: int
      MudRoomsRequested: int
      MudRoomsIndexed: int
      MudItemsRequested: int
      MudItemsIndexed: int
      MudRecipesRequested: int
      MudRecipesIndexed: int
      GraphBackfilled: int
      AutoSplitCreatedCount: int
      AutoSplitProposalCount: int }

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

let private updateConfiguredStatus
    (enabled: bool)
    (intervalSeconds: int)
    (autoSplitEnabled: bool)
    (autoSplitIntervalSeconds: int)
    (autoSplitLimit: int)
    (autoSplitMinChunkCount: int)
    (autoSplitScopeKind: string option)
    (graphBackfillLimit: int) =
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

let runAutomationPass (forceAutoSplit: bool) (lastAutoSplitAt: DateTimeOffset option) =
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

    updateConfiguredStatus
        enabled
        intervalSeconds
        autoSplitEnabled
        autoSplitIntervalSeconds
        autoSplitLimit
        autoSplitMinChunkCount
        autoSplitScopeKind
        graphBackfillLimit

    let now = DateTimeOffset.UtcNow
    let summary =
        SemanticGraphRepository.runBackgroundSync
            forumLimit
            blogLimit
            mudRoomLimit
            mudItemLimit
            mudRecipeLimit

    let shouldAutoSplit =
        forceAutoSplit
        || (
            autoSplitEnabled
            &&
            match lastAutoSplitAt with
            | None -> true
            | Some lastRun -> (now - lastRun).TotalSeconds >= float autoSplitIntervalSeconds
        )

    let autoSplitCreated, autoSplitProposalsApplied, updatedLastAutoSplitAt =
        if shouldAutoSplit then
            let created, proposalsApplied =
                SemanticGraphRepository.applyTokenSplitProposals autoSplitLimit autoSplitMinChunkCount autoSplitScopeKind
            created, proposalsApplied, Some now
        else
            0, 0, lastAutoSplitAt

    let graphBackfilled =
        let initialBackfill = SemanticGraphRepository.backfillGraphChunks graphBackfillLimit
        if autoSplitCreated > 0 || autoSplitProposalsApplied > 0 then
            initialBackfill + SemanticGraphRepository.backfillGraphChunks graphBackfillLimit
        else
            initialBackfill

    currentStatus <-
        { currentStatus with
            ConsecutiveDbFailures = 0
            LastSyncAt = Some now
            LastSplitAt =
                match updatedLastAutoSplitAt with
                | Some timestamp when autoSplitCreated > 0 || autoSplitProposalsApplied > 0 || forceAutoSplit -> Some timestamp
                | _ -> currentStatus.LastSplitAt
            LastGraphBackfillCount = graphBackfilled
            LastAutoSplitCreatedCount = autoSplitCreated
            LastAutoSplitProposalCount = autoSplitProposalsApplied }

    updatedLastAutoSplitAt,
    { ForumThreadsRequested = summary.ForumThreadsRequested
      ForumThreadsIndexed = summary.ForumThreadsIndexed
      BlogArticlesRequested = summary.BlogArticlesRequested
      BlogArticlesIndexed = summary.BlogArticlesIndexed
      MudRoomsRequested = summary.MudRoomsRequested
      MudRoomsIndexed = summary.MudRoomsIndexed
      MudItemsRequested = summary.MudItemsRequested
      MudItemsIndexed = summary.MudItemsIndexed
      MudRecipesRequested = summary.MudRecipesRequested
      MudRecipesIndexed = summary.MudRecipesIndexed
      GraphBackfilled = graphBackfilled
      AutoSplitCreatedCount = autoSplitCreated
      AutoSplitProposalCount = autoSplitProposalsApplied }

type SemanticIngestionWorker(logger: ILogger<SemanticIngestionWorker>) =
    inherit BackgroundService()
    let mutable consecutiveDbFailures = 0
    let mutable lastAutoSplitAt : DateTimeOffset option = None

    override _.ExecuteAsync(ct) =
        task {
            logger.LogInformation("SemanticIngestionWorker starting")

            while not ct.IsCancellationRequested do
                try
                    let enabled = envFlag "SEMANTIC_SYNC_ENABLED" true
                    let intervalSeconds = Math.Max(envInt "SEMANTIC_SYNC_INTERVAL_SECONDS" 300, 30)

                    if enabled then
                        let updatedLastAutoSplitAt, result =
                            runAutomationPass false lastAutoSplitAt
                        lastAutoSplitAt <- updatedLastAutoSplitAt

                        if result.AutoSplitProposalCount > 0 then
                            logger.LogInformation(
                                "Semantic auto-split pass created {CreatedCount} variants across {ProposalCount} proposals",
                                result.AutoSplitCreatedCount,
                                result.AutoSplitProposalCount)

                        let hasSyncWork =
                            result.ForumThreadsRequested > 0
                            || result.BlogArticlesRequested > 0
                            || result.MudRoomsRequested > 0
                            || result.MudItemsRequested > 0
                            || result.MudRecipesRequested > 0

                        if hasSyncWork then
                            logger.LogInformation(
                                "Semantic sync pass complete. Forum {ForumIndexed}/{ForumRequested}, blog {BlogIndexed}/{BlogRequested}, MUD rooms {RoomsIndexed}/{RoomsRequested}, MUD items {ItemsIndexed}/{ItemsRequested}, MUD recipes {RecipesIndexed}/{RecipesRequested}",
                                result.ForumThreadsIndexed,
                                result.ForumThreadsRequested,
                                result.BlogArticlesIndexed,
                                result.BlogArticlesRequested,
                                result.MudRoomsIndexed,
                                result.MudRoomsRequested,
                                result.MudItemsIndexed,
                                result.MudItemsRequested,
                                result.MudRecipesIndexed,
                                result.MudRecipesRequested)

                        if result.GraphBackfilled > 0 then
                            logger.LogInformation(
                                "Semantic graph backfill rebuilt {ChunkCount} chunk graphs",
                                result.GraphBackfilled)

                        consecutiveDbFailures <- 0

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
