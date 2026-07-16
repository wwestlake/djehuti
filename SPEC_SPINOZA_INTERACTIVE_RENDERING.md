# Spinoza Interactive Rendering System

## Overview

Extend Spinoza from a pure computational DSL to a **declarative interactive rendering language**. Programs emit structured render descriptors that the host uses to build interactive canvases with:
- Live data visualization (graphs, plots, 3D)
- Interactive controls (sliders, buttons, text inputs)
- Rich content rendering (music notation, tablature, math notation, piano keyboard, fretboard)
- Real-time interaction (control changes trigger re-evaluation)

## Design Principles

1. **Language stays pure**: No mutation, no I/O, no state. `emit()` is the only side channel.
2. **Descriptive not imperative**: Programs declare *what* to render, host handles *how*.
3. **Extensible**: New render types added without language changes.
4. **Bidirectional data flow**: 
   - Program → Host: emit render descriptors
   - Host → Program: re-run with updated parameter values
5. **Same system everywhere**: DjeLab Graph pane, Learn app canvas, classroom teaching tools

## Render Descriptor Format

All render objects follow a consistent pattern:

```json
{
  "type": "graph|button|slider|input|notation|piano|fretboard|music|text|container",
  "id": "unique_identifier",
  "props": {
    "key": "value",
    ...
  }
}
```

## Render Types & Schemas

### Graph / Chart
```json
{
  "type": "graph",
  "id": "g1",
  "props": {
    "title": "Population Growth",
    "x": [1, 2, 3, 4, 5],
    "y": [10, 25, 50, 120, 300],
    "xLabel": "Time (years)",
    "yLabel": "Population",
    "mode": "lines|scatter|bars|area",
    "color": "#FF6B6B"
  }
}
```

### Button
```json
{
  "type": "button",
  "id": "btn_play",
  "props": {
    "label": "Play Simulation",
    "disabled": false,
    "onClick": "trigger_simulation"
  }
}
```
When clicked, host runs callback that re-runs program with context.

### Slider / Input Control
```json
{
  "type": "slider",
  "id": "speed_slider",
  "props": {
    "label": "Speed",
    "min": 0,
    "max": 100,
    "value": 50,
    "step": 1,
    "unit": "m/s"
  }
}
```
On change, host updates program's `speed` variable and re-runs.

### Text Input
```json
{
  "type": "input",
  "id": "text_eq",
  "props": {
    "label": "Equation",
    "value": "sin(x) * cos(y)",
    "placeholder": "Enter expression...",
    "variableName": "equation"
  }
}
```

### Music Notation
```json
{
  "type": "notation",
  "id": "staff1",
  "props": {
    "scoreJson": "{...MusicScore JSON...}",
    "highlighted": ["note_id_1", "note_id_2"],
    "title": "C Major Scale"
  }
}
```

### Piano Keyboard
```json
{
  "type": "piano",
  "id": "piano1",
  "props": {
    "highlighted": [60, 64, 67],
    "startOctave": 3,
    "endOctave": 5,
    "title": "C Major Chord"
  }
}
```

### Fretboard
```json
{
  "type": "fretboard",
  "id": "guitar1",
  "props": {
    "highlighted": [{fret: 0, string: 0}, {fret: 2, string: 1}],
    "numStrings": 6,
    "numFrets": 12,
    "title": "G Major"
  }
}
```

### Music Staff (ABC Notation)
```json
{
  "type": "music",
  "id": "staff1",
  "props": {
    "abc": "M:4/4\nL:1/4\nC D E F",
    "title": "Simple Scale"
  }
}
```

### Math Expression (LaTeX)
```json
{
  "type": "math",
  "id": "eq1",
  "props": {
    "latex": "\\frac{\\pi r^2}{2}",
    "display": true
  }
}
```

### Text / Label
```json
{
  "type": "text",
  "id": "label1",
  "props": {
    "content": "Current temperature: 25°C",
    "size": "large|medium|small",
    "align": "left|center|right"
  }
}
```

### Container (Layout)
```json
{
  "type": "container",
  "id": "col1",
  "props": {
    "layout": "row|column|grid",
    "children": ["g1", "btn_play", "speed_slider"]
  }
}
```

## Spinoza Language Extensions

### New AST Node
```fsharp
type Expr =
    | ... (existing nodes)
    | Render of renderType: string * props: (string * Expr) list
```

### Usage Examples

```spinoza
// Simple graph
let x = range(0, 10, 0.1)
let y = [sin(xi) for xi in x]
emit(render("graph", [
  ("title", "Sine Wave"),
  ("x", x),
  ("y", y),
  ("mode", "lines")
]))

// Interactive simulation with slider
let speed = param("speed", 50)
let distance = speed * time
emit(render("slider", [
  ("label", "Speed"),
  ("min", 0),
  ("max", 100),
  ("value", speed),
  ("variableName", "speed")
]))
emit(render("text", [
  ("content", "Distance: " + string(distance) + " m")
]))

// Music notation
let highlighted_notes = [0, 2, 4]  // C, E, G (C major chord)
emit(render("notation", [
  ("scoreJson", score_json),
  ("highlighted", highlighted_notes),
  ("title", "C Major Chord")
]))
```

### New Builtin Functions

#### `param(name: string, default: number): number`
Get a parameter value. On first run uses default; on re-evaluation with changed controls, returns the new value.

#### `string(value): string`
Convert a value to string for display.

#### `json(object): string`
Serialize object to JSON string.

## Host Implementation (DjeLab & Learn App)

### Render Engine Workflow

1. **Initial Run**
   - User writes Spinoza program
   - Host evaluates program
   - Program emits render descriptors via `emit()`
   - Host collects all emitted descriptors

2. **Render Phase**
   - Host builds interactive canvas from descriptors
   - Places controls (sliders, buttons, inputs)
   - Renders visualization (graphs, notation, etc.)
   - Binds event handlers to controls

3. **Interaction**
   - User changes slider → host captures new value
   - User clicks button → host may run callback program
   - Host updates `param()` values in environment
   - **Re-runs entire program** with new params
   - Program emits new descriptors
   - Host **re-renders canvas** with new state

### Data Flow

```
Spinoza Program
    ↓ (evaluates with param values)
Emit Descriptors
    ↓ (host collects)
Render Descriptors → Build Canvas
    ↓ (user interacts)
Updated Param Values
    ↓ (sent back to program)
Spinoza Program (re-evaluate)
    ... (cycle repeats)
```

### Host Components Needed

1. **RenderDescriptor DTO** (C#)
   - Deserialized JSON from program
   - Validated against schema

2. **RenderEngine** (C#/Blazor)
   - Takes descriptors
   - Generates interactive components
   - Manages control bindings

3. **Canvas Renderer** (Blazor component)
   - Displays graphs, text, controls
   - Handles native rendering (notation, piano, fretboard, etc.)
   - Responsive layout

4. **Interaction Handler**
   - Slider changes → update param, re-run
   - Button clicks → run callback, re-run
   - Maintains re-run history/cache

## Implementation Phases

### Phase 1: Core Infrastructure
- [ ] Extend Spinoza AST with `Render` node
- [ ] Update parser to parse `render(...)` calls
- [ ] Update evaluator to construct descriptors
- [ ] Define RenderDescriptor DTO (C#)
- [ ] Build RenderEngine (accepts descriptors, produces interactive DOM)
- [ ] Update DjeLab to use RenderEngine instead of PlotlyChart

### Phase 2: Interactive Controls
- [ ] Implement `param()` builtin
- [ ] Add slider, input, button descriptor support
- [ ] Build control binding (change event → re-run)
- [ ] Text and label descriptors
- [ ] Container/layout descriptors

### Phase 3: Music & Notation Rendering
- [ ] Notation descriptor → MusicStaff component
- [ ] Piano descriptor → PianoKeys component
- [ ] Fretboard descriptor → FretboardDiagram component
- [ ] Music (ABC) descriptor → ABC renderer
- [ ] Math (LaTeX) descriptor → MathJax/KaTeX renderer

### Phase 4: Integration
- [ ] Apply RenderEngine to Learn app canvas
- [ ] Classroom teaching directives use render descriptors
- [ ] AI tutor can emit `render("notation", ...)` to teach
- [ ] Full end-to-end: Spinoza → Interactive Visualization → User Control → Spinoza

### Phase 5: Performance & Polish
- [ ] Cache re-run results for unchanged params
- [ ] Debounce rapid slider changes
- [ ] Progress indication for long-running programs
- [ ] Error handling and recovery
- [ ] Mobile-responsive layouts

## Examples

### Example 1: Interactive Sine Wave
```spinoza
let frequency = param("frequency", 2)
let amplitude = param("amplitude", 1)
let x = linspace(0, 2 * pi, 100)
let y = [amplitude * sin(frequency * xi) for xi in x]

emit(render("slider", [
  ("label", "Frequency"), ("min", 1), ("max", 10), 
  ("value", frequency), ("variableName", "frequency")
]))
emit(render("slider", [
  ("label", "Amplitude"), ("min", 0.1), ("max", 3), 
  ("value", amplitude), ("variableName", "amplitude")
]))
emit(render("graph", [
  ("title", "Sine Wave"), ("x", x), ("y", y), ("mode", "lines")
]))
```

### Example 2: Chord Player
```spinoza
let chord_type = param("chord", "C")
let notes = match(chord_type, [
  ("C", [60, 64, 67]),
  ("G", [67, 71, 74]),
  ("F", [65, 69, 72])
])

emit(render("button", [
  ("label", "Play Chord"), ("onClick", "play_audio")
]))
emit(render("piano", [
  ("highlighted", notes),
  ("title", chord_type + " Major")
]))
```

### Example 3: Music Theory Lesson (AI Tutor)
```spinoza
// Tutor generates this to teach scales
let scale_type = param("scale", "major")
let root_note = param("root", "C")

let notes = match(scale_type, [
  ("major", [0, 2, 4, 5, 7, 9, 11]),
  ("minor", [0, 2, 3, 5, 7, 8, 10])
])

let root_midi = note_to_midi(root_note)
let midi_notes = [root_midi + interval for interval in notes]

emit(render("notation", [
  ("scoreJson", build_scale_notation(root_note, notes)),
  ("title", root_note + " " + scale_type + " Scale")
]))
emit(render("piano", [
  ("highlighted", midi_notes)
]))
emit(render("text", [
  ("content", "The " + root_note + " " + scale_type + " scale has these notes")
]))
```

## Compatibility Notes

- **Backwards compatible**: Existing Spinoza programs that only emit numbers still work
- **Gradual adoption**: Mix emit numbers and render descriptors in same program
- **Same evaluation model**: Program is still pure, fully evaluated each time
- **No performance regression**: Emitting descriptors is as fast as emitting numbers

## Safety & Constraints

- Spinoza remains **sandboxed** (render descriptors can't access filesystem, network, etc.)
- No program can modify another program's state
- Canvas rendering is **deterministic** (same program state → same visual output)
- **No scripting in descriptors** (descriptors are data, not code)
