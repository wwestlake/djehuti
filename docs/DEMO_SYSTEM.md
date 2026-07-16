# Demo System Architecture

## Overview
The Demo Mode system enables AI-controlled demonstrations of DjeLab with annotated explanations. Demos play fluently without showing individual button clicks—instead, explanatory bubbles appear with pointers to highlight what's happening.

## Core Components

### DemoMode Service (`Services/DemoMode.cs`)
Manages demo state, execution, and step sequencing.

**Key methods:**
- `LoadScript(DemoScript)` — Load a demo script
- `StartDemo()` — Begin playback
- `PauseDemo()` / `ResumeDemo()` — Pause/resume
- `StopDemo()` — Stop and reset
- `AdvanceStep()` — Jump to next step
- `RegisterActionHandler(type, handler)` — Register custom action handlers

**Events:**
- `OnStateChanged` — Fires when demo state changes (UI re-renders)
- `OnActionExecuting` — Fires before action executes

### DemoAnnotation Component (`Components/DemoAnnotation.razor`)
Renders the annotation overlay with bubble + pointer.

**Features:**
- Bubble displays current annotation text and step counter
- Pointer line and highlight box track target element
- Auto-repositions bubble to stay in viewport
- Smooth fade-in animations

### DemoController Component (`Components/DemoController.razor`)
Playback controls in fixed position (bottom-right).

**Controls:**
- Play / Resume button
- Pause button
- Next step button
- Stop button

## Demo Script Format

JSON file defining a sequence of steps:

```json
{
  "title": "Demo Title",
  "description": "Optional description",
  "steps": [
    {
      "id": "unique-step-id",
      "action": {
        "type": "activatePane",
        "params": { "paneKind": "files" }
      },
      "annotation": "This is the Files pane...",
      "pointerTarget": "pane:files",
      "duration": 2500
    }
  ]
}
```

### Action Types

- **none** — No action, just show annotation
- **activatePane** — Switch to a pane
  - Params: `paneKind` (files, graph, editor, chat, flow, data, console)
- **wait** — Pause execution
  - Params: `ms` (milliseconds)
- *Custom actions* — Register via `RegisterActionHandler()`

### Pointer Targets

- `pane:files` — Highlight the Files pane
- `pane:graph` — Highlight the Graph pane
- `element:import-btn` — Highlight element with `data-demo-target="import-btn"`
- `null` — No pointer, just annotation bubble

## JavaScript Helpers (`wwwroot/js/demo-helpers.js`)

- `dj_getElementRect(selector)` — Get bounding box of element
- `dj_getViewportWidth()` / `dj_getViewportHeight()` — Get viewport size
- `dj_recordDemoStep(stepData)` — Hook for future recording feature

## Usage

### Playing a Demo Programmatically

```csharp
@inject DemoMode DemoMode

<button @onclick="PlayDemo">Start Demo</button>

@code {
    async Task PlayDemo()
    {
        var script = new DemoScript
        {
            Title = "My Demo",
            Steps = new() {
                new DemoStep {
                    Action = new DemoAction { Type = "activatePane", Params = new() { ["paneKind"] = "files" } },
                    Annotation = "Click Files to browse your data",
                    PointerTarget = "pane:files",
                    DurationMs = 2000
                }
            }
        };

        DemoMode.LoadScript(script);
        await DemoMode.StartDemo();
    }
}
```

### Registering Custom Actions

```csharp
DemoMode.RegisterActionHandler("importData", async action =>
{
    if (action.Params.TryGetValue("filename", out var file))
    {
        // Perform import
        await FilesClient.ImportAsync(file.ToString());
    }
});

// Then in demo script:
{
    "action": {
        "type": "importData",
        "params": { "filename": "data.csv" }
    }
}
```

## Phase 2 (Future)

- **Demo Recording** — Click a "Record" button to capture user actions as a demo script
- **AI Generation** — API endpoint that generates demo scripts from natural language
- **Export** — Save annotated demos as MP4 for YouTube
- **Pacing Control** — UI for adjusting annotation duration per step
- **Branching Demos** — Conditional steps based on user choice or state

## Testing

1. Open DjeLab workspace
2. Check browser console for any errors
3. Programmatically load a demo script and call `StartDemo()`
4. Verify:
   - Annotation bubble appears
   - Pointer highlights target element
   - Play/pause/next controls work
   - Step counter is accurate
