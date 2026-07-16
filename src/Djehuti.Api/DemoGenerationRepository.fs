module Djehuti.Api.DemoGenerationRepository

open System
open System.Text.Json
open System.Text.Json.Serialization

type DemoActionJson =
    { [<JsonPropertyName("type")>] Type: string
      [<JsonPropertyName("params")>] Params: JsonElement option }

type DemoStepJson =
    { [<JsonPropertyName("id")>] Id: string
      [<JsonPropertyName("action")>] Action: DemoActionJson
      [<JsonPropertyName("annotation")>] Annotation: string option
      [<JsonPropertyName("pointerTarget")>] PointerTarget: string option
      [<JsonPropertyName("duration")>] Duration: int }

type DemoScriptJson =
    { [<JsonPropertyName("title")>] Title: string
      [<JsonPropertyName("description")>] Description: string option
      [<JsonPropertyName("steps")>] Steps: DemoStepJson list }

// System prompt for Claude to generate demo scripts
let demoGenerationSystemPrompt (appName: string) =
    $"""You are an expert at creating interactive product demos. Your task is to generate a JSON demo script for the {appName} application.

A demo script guides users through a workflow with visual annotations and pointers.

**Demo Script Structure:**
- Each step has an action, annotation (explanation), and pointer target (what to highlight)
- Annotations appear in bubbles pointing to UI elements
- Actions control what happens: activate panes, wait, or custom handlers
- Duration is milliseconds to show each annotation

**Available Actions:**
- "none" - just show annotation, no action
- "activatePane" - switch to a pane
  params: {{"paneKind": "files|graph|editor|chat|flow|data|console"}}
- "wait" - pause execution
  params: {{"ms": 2000}}

**Pointer Targets:**
- "pane:graph" - highlight a pane by its kind
- "element:import-btn" - highlight element with data-demo-target attribute
- null - no pointer, just annotation

**Guidelines:**
1. Create 5-10 steps total for a complete demo
2. Each annotation should be 1-2 sentences, conversational tone
3. Pacing: 2000-3000ms for each step (user reading time)
4. Start with intro annotation explaining what they'll see
5. End with a summary or next steps
6. Use pointer targets to guide attention
7. Sequence should follow a logical workflow

**Response Format:**
Return ONLY a valid JSON object with title, optional description, and steps array. No markdown, no explanation, just the JSON.

Example structure:
{{
  "title": "Getting Started",
  "description": "A brief introduction",
  "steps": [
    {{
      "id": "intro-1",
      "action": {{"type": "none"}},
      "annotation": "Welcome! Let me show you...",
      "pointerTarget": null,
      "duration": 3000
    }},
    {{
      "id": "activate-files",
      "action": {{"type": "activatePane", "params": {{"paneKind": "files"}}}},
      "annotation": "This is the Files pane where you manage your data.",
      "pointerTarget": "pane:files",
      "duration": 2500
    }}
  ]
}}"""

// Call Claude API to generate a demo script
let generateDemoScript (prompt: string) (appName: string) : Async<Result<DemoScriptJson, string>> = async {
    try
        // TODO: Implement Claude API call
        // For now, return a placeholder
        let systemPrompt = demoGenerationSystemPrompt appName

        // This would call your Claude API integration
        // For now, returning error to show the integration point
        return Error "Claude API integration not yet configured"
    with ex ->
        return Error $"Failed to generate demo: {ex.Message}"
}

// Validate generated script
let validateScript (script: DemoScriptJson) : Result<DemoScriptJson, string list> =
    let mutable errors = []

    if String.IsNullOrWhiteSpace script.Title then
        errors <- "Title is required" :: errors

    if script.Steps |> List.isEmpty then
        errors <- "At least one step is required" :: errors

    for step in script.Steps do
        if String.IsNullOrWhiteSpace step.Id then
            errors <- "Step ID is required" :: errors

        if String.IsNullOrWhiteSpace step.Action.Type then
            errors <- "Action type is required" :: errors

        let validTypes = ["none"; "activatePane"; "wait"]
        if not (List.contains step.Action.Type validTypes) then
            errors <- $"Invalid action type: {step.Action.Type}" :: errors

        if step.Duration < 500 then
            errors <- $"Step duration must be at least 500ms" :: errors

    match errors with
    | [] -> Ok script
    | errors -> Error (List.rev errors)
