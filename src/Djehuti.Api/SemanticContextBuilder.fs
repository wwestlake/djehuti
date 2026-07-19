module Djehuti.Api.SemanticContextBuilder

open System
open Djehuti.Core
open Djehuti.Api

/// Builds AI context by querying LiteSemRag for all non-prompt content.
/// Structure: App account context (instructions, examples) + User context (history, domain knowledge)
type ContextSource =
    | AppAccount of appName: string * userId: Guid
    | User of userId: Guid
    | Both of appName: string * appUserId: Guid * userUserId: Guid

type ContextBuilderRequest =
    { Query: string
      Source: ContextSource
      ContextLimit: int
      SemanticLimit: int }

type ContextResult =
    { AppInstructions: string option
      UserHistory: string
      RelevantExamples: string list
      RelevantReferences: string list
      TotalTokens: int }

/// Retrieve app account instructions and context from semantic graph
let getAppContext (appName: string) (appUserId: Guid) (limit: int) : string option =
    try
        let hits = SemanticGraphRepository.searchChunks appName (Some $"app-{appName}") limit
        if hits.Count = 0 then None
        else
            hits
            |> Seq.map (fun hit -> hit.Content)
            |> String.concat "\n\n"
            |> Some
    with _ -> None

/// Retrieve user conversation history and relevant context from semantic graph
let getUserContext (userId: Guid) (query: string) (limit: int) : string =
    try
        // For now, filter by user_id in semantic_documents
        // Future: implement user_id filtering in searchChunks
        let hits = SemanticGraphRepository.searchChunks query None limit

        hits
        |> Seq.map (fun hit -> hit.Content)
        |> String.concat "\n\n"
    with _ -> ""

/// Build combined context for an AI app
let build (request: ContextBuilderRequest) : ContextResult =
    let appContext, appUserId =
        match request.Source with
        | AppAccount (appName, userId) ->
            getAppContext appName userId request.ContextLimit, Some userId
        | User _ -> None, None
        | Both (appName, appUserId, _) ->
            getAppContext appName appUserId request.ContextLimit, Some appUserId

    let userContext =
        match request.Source with
        | User userId | Both (_, _, userId) ->
            getUserContext userId request.Query request.SemanticLimit
        | AppAccount _ -> ""

    { AppInstructions = appContext
      UserHistory = userContext
      RelevantExamples = [] // TODO: extract and deduplicate examples from hits
      RelevantReferences = [] // TODO: extract references from hits
      TotalTokens = 0 } // TODO: estimate token count

/// Format context for prompt injection (not the prompt itself, just supporting context)
let formatForPrompt (context: ContextResult) : string =
    let parts =
        [ if Option.isSome context.AppInstructions then
            $"## App Context\n{context.AppInstructions.Value}"
          if not (String.IsNullOrWhiteSpace context.UserHistory) then
            $"## User Context\n{context.UserHistory}"
          if context.RelevantExamples.Length > 0 then
            $"## Examples\n{String.concat "\n\n" context.RelevantExamples}"
          if context.RelevantReferences.Length > 0 then
            $"## References\n{String.concat "\n\n" context.RelevantReferences}" ]
    String.concat "\n\n" parts
