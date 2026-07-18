# Djehuti Architect (DjeArch) — Vision & Scope

> **Status: draft, in-progress design discussion.** This document captures what's been decided so far and flags what's still open. Update it as decisions get made — do not treat silence on an open question as an answer.

## What it is

Djehuti Architect is a web-based technical architecture design tool. The core idea that distinguishes it from a plain diagramming tool: **an AI reads a codebase and produces the architecture views itself**, rather than a human hand-authoring a model and the tool just rendering it. BYOK (Bring Your Own Key) AI is built into the tool for this purpose, following the same pattern already proven in Djehuti Teacher.

The underlying principle carried over from the existing scaffold: diagrams and documentation are always **generated views of a single source-of-truth model**, never hand-maintained separately. What's new in this phase of the vision is *how that model gets populated* — from AI reading source code, not just manual entry.

## Current state (built, on `develop`, not yet merged to `main`)

Two commits so far:
1. **Scaffold** — new Blazor WebAssembly project (`src/Djehuti.Architect`), core model schema.
2. **Mermaid diagram rendering** — model → Mermaid C4-container text → rendered SVG, verified live in-browser.

### Data model (`Models/ArchitectureModel.cs`)

Deliberately coarse, container-level shape:
- `ArchitectureComponent` — a module/service/library/external system/database/UI node. Nests via `ParentId` (container view vs. component view is the same list at different depths).
- `ArchitectureConnection` — a directed relationship between two components (calls/reads/writes/publishes/subscribes/depends-on), with optional protocol.
- `DeploymentNode` — a place components run (server/container/region), also nests via `ParentId`.
- `ArchitectureModel` — the whole thing: name, description, schema version, the three lists above, `GeneratedAt`, and `SourceRepository` (a local path or GitHub URL — present in the schema already, but nothing populates it yet; every model so far is hand-authored in `Services/SampleModels.cs`).

### Diagram generation (`Services/MermaidGenerator.cs`)

Pure projection: model → Mermaid C4Container syntax. Sanitizes ids/labels since they're expected to eventually come from AI-generated or repo-derived data, not just hand-typed JSON. Scoped to the container view only — no actor/person element yet (the model has no concept of an end user, and reading a repo alone can't discover "the user" from source).

## Vision for what's next (in discussion — not yet fully decided)

The AI-driven direction adds capability the current build doesn't have at all:

1. **AI scans source code and produces the architecture model** — instead of `SampleModels.cs`-style hand authoring, point the tool at a codebase and it infers components, connections, and (eventually) class-level detail.
2. **Multiple view types beyond the C4 container diagram** — class diagrams were named explicitly as a target ("class diagrams et al"), implying sequence diagrams, deployment diagrams, and similar are likely wanted too as the tool matures.
3. **BYOK AI integration**, reusing the pattern already live in Djehuti Teacher (accept a user-supplied API key; officially support one provider, others best-effort).

## Open questions (unresolved as of this writing)

These need answers before implementation starts on the AI-scanning phase — do not guess at them:

- **How does the AI get the source code?** Candidates: paste a GitHub repo URL and clone/read server-side, upload a zip through the browser, or direct filesystem/git access on the server (as Cyberscope already has for its own analysis). Could be more than one of these over time — which comes first?
- **Class diagrams need a schema the current model doesn't have.** `ArchitectureComponent`/`ArchitectureConnection` are container-level only — no classes, fields, methods, inheritance, or interface implementation. Open question: one unified model that captures both the high-level container view *and* class-level detail, or a separate deeper-zoom model that only populates when a specific component is drilled into?
- **What "a language built in to help it build architecture" means, precisely.** Two live interpretations, not yet disambiguated with the user:
  - (a) the AI is given a set of tools to explore a codebase interactively (list files, read a file, search for a symbol) so it can navigate a large repo instead of needing the whole thing dumped into one prompt — this is the most common shape for "AI reads your codebase" features, and would reuse the BYOK plumbing from Teacher; or
  - (b) an actual architecture description language (in the spirit of Structurizr's DSL) that the AI writes to as its output format, which then drives diagram generation, distinct from the tool-access question entirely.
- **BYOK scope** — same posture as Teacher (officially support OpenAI only, other provider keys accepted as best-effort/placeholder — see [[DjeLab-MultiProvider-APIKeys]]), or does Architect need genuine multi-provider support from day one, e.g. because code-scanning may need larger context windows or specific model capabilities not all providers offer?

## Prior roadmap note (from the original scaffold commit, superseded/expanded by the above)

The original commit message ordered work as: diagrams and UI first (done) → storage format refinement next → industry-format export later. "Storage format refinement" was never elaborated beyond that phrase — there is currently **no persistence at all** (no save/load, no API endpoint, no database table); every model is an in-memory C# object built by `SampleModels.cs`. That gap still needs resolving regardless of how the AI-scanning questions above land.
