module Djehuti.Api.SemanticIngestionWorker

open System
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

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

type SemanticIngestionWorker(logger: ILogger<SemanticIngestionWorker>) =
    inherit BackgroundService()

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

            while not ct.IsCancellationRequested do
                try
                    if enabled then
                        let summary =
                            SemanticGraphRepository.runBackgroundSync
                                forumLimit
                                blogLimit
                                mudRoomLimit
                                mudItemLimit
                                mudRecipeLimit

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

                    do! Task.Delay(TimeSpan.FromSeconds(float intervalSeconds), ct)
                with
                | :? TaskCanceledException -> ()
                | ex ->
                    logger.LogError(ex, "SemanticIngestionWorker iteration error")
                    do! Task.Delay(TimeSpan.FromSeconds(30.0), ct)

            return ()
        }
