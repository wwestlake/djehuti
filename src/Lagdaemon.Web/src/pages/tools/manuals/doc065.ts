import type { DocManual } from '../electronicsDocs'

export const doc065: DocManual = {
  "slug": "frust-language-reference",
  "title": "Djehuti Electronics Lab FRust Language Reference",
  "documentId": "DJEHUTI-DOC-065",
  "revision": "0.1",
  "status": "Initial technical reference ready for review",
  "date": "2026-10-13",
  "sections": [
    {
      "slug": "overview",
      "title": "Overview",
      "summary": "Source-verified reference for FRust syntax, semantics, compiler behavior, and Djehuti host integration",
      "content": "![Documentation figure](/docs/electronics-work-bench/doc-030/djehuti-electronics-lab-splash.png)\n\nSource-verified reference for FRust syntax, semantics, compiler behavior, and Djehuti host integration\n\n| Field | Value |\n| --- | --- |\n| Publisher | LagDaemon Software |\n| Application | Djehuti Electronics Lab |\n| Document identifier | DJEHUTI-DOC-065 |\n| Revision | 0.1 |\n| Date | 2026-10-13 |\n| Status | Initial technical reference ready for review |\n| Original documentation baseline | 76c6049 |\n| Current Djehuti source consulted | 7c99dac |\n| Local FRust source consulted | ccc4c5f |\n| Standalone GitHub FrustLang | d14a274 |\n\nSource basis: Source review used Djehuti Git HEAD 7c99dac on 2026-10-13, with local uncommitted Control Panel and scratch/test artifacts present. The workbench CMake configuration points to local FRust source checkout; that local FRust checkout was at ccc4c5f on branch development with only untracked sim/status artifacts observed. The standalone GitHub FrustLang master reference was inspected at d14a274. This documentation pass did not modify application or FRust source."
    },
    {
      "slug": "1-purpose-and-source-authority",
      "title": "1 Purpose and Source Authority",
      "summary": "This reference describes the FRust language as implemented or source-visible in the inspected lexer, parser, AST, compiler API, runtime, tests, and Djehuti host integrati...",
      "content": "This reference describes the FRust language as implemented or source-visible in the inspected lexer, parser, AST, compiler API, runtime, tests, and Djehuti host integration. It is not a Rust manual. Rust-like syntax should not be assumed to imply Rust semantics.\n\n![Documentation figure](/docs/electronics-work-bench/doc-065/image-01.png)\n\nFigure 1. Language, compiler/runtime, and Djehuti host layers used by this reference.\n\n| Source | Revision or basis | Use in this reference |\n| --- | --- | --- |\n| Djehuti application | 7c99dac | Embedded engine, node compiler, programmable component integration, debugger executor, smoke-test results. |\n| Local FRust source checkout | ccc4c5f | Compiler/runtime source used by Djehuti CMake configuration. |\n| GitHub FrustLang master clone | d14a274 | Standalone language, grammar, API, README, and gap notes. |\n| FRUST_LANG_SPEC and LANGUAGE_GAPS | Source-inspected | Used cautiously because some specification text is aspirational or records planned/future states. |"
    },
    {
      "slug": "2-lexical-structure",
      "title": "2 Lexical Structure",
      "summary": "The lexer recognizes line comments beginning with // and block comments delimited by /* and */. It recognizes integer, floating-point, string, and Boolean literals. Strin...",
      "content": "The lexer recognizes line comments beginning with // and block comments delimited by /* and */. It recognizes integer, floating-point, string, and Boolean literals. String escapes visible in the lexer include newline, tab, backslash, and double quote.\n\n| Category | Source-visible tokens |\n| --- | --- |\n| Declaration keywords | fn, extern, entry, use, import, impl, interface, manifest, pub, unsafe, struct, type, enum |\n| Control flow | if, else, while, loop, for, in, break, continue, return, match, _ |\n| State and effects | effect, perform, handle, resume, with |\n| Components | component, node, pure, callable, in, out |\n| Metaprogramming | build_time, quote, unquote |\n| Pointer/reference forms | own, shared, weak, raw |\n| Literals | true, false, integer, floating-point, string |\n| Operators and punctuation | ::, ::<, .., ->, =>, ==, !=, <=, >=, <<, >>, arithmetic, bitwise, comparison, assignment, braces, parens, brackets, comma, semicolon, colon, dot |"
    },
    {
      "slug": "3-program-structure-and-declarations",
      "title": "3 Program Structure and Declarations",
      "summary": "A FRust source unit is a list of declarations and may include a trailing statement. Source-visible declarations include functions, structs, enums, type aliases, effects, ...",
      "content": "A FRust source unit is a list of declarations and may include a trailing statement. Source-visible declarations include functions, structs, enums, type aliases, effects, components, nodes, use/import declarations, impl blocks, interfaces, manifests, and top-level statements.\n\n| Declaration | Current source-visible shape |\n| --- | --- |\n| Function | pub, unsafe, extern, and entry modifiers are grammar-visible. Functions may have generic parameters, typed parameters, optional return type, block body, expression body, or prototype. |\n| Struct | Struct declarations are source-visible. Struct literals and member access are present in AST/grammar. |\n| Enum | Enum declarations and match expressions are source-visible. Known match limitations are recorded later. |\n| Type alias | type declarations are grammar-visible. |\n| Impl and interface | Impl/interface syntax is source-visible; practical Djehuti examples are not yet broadly validated. |\n| Manifest | Manifest declarations support package metadata used by compiler/runtime integration. |\n| Use/import | Qualified use forms and direct pod import syntax are source-visible. |"
    },
    {
      "slug": "4-types",
      "title": "4 Types",
      "summary": "The grammar and AST represent named types, generic type arguments, numeric type arguments, refinements, smart-pointer prefixes, raw pointers, references, arrays, structs,...",
      "content": "The grammar and AST represent named types, generic type arguments, numeric type arguments, refinements, smart-pointer prefixes, raw pointers, references, arrays, structs, enums, and function-oriented forms. The Djehuti smoke tests verify f64, String, Array<f64,N>, and host function signatures directly.\n\n| Type area | Documentation confidence |\n| --- | --- |\n| f64 | Verified by smoke tests using gain, one_pole, mutable while loop, and host exp/log. |\n| String | Verified by runScript returning String and print_line host capture. |\n| Array<f64,N> | Verified by smoke test writing through an array parameter and observing host buffer persistence. |\n| bool and integers | Source-visible and used in parser/AST; some boolean and integer behavior appears in debugger tests. |\n| Structs and enums | Source-visible in grammar/AST. Enum/match support is present, but some pattern forms remain partial. |\n| own/shared/weak/raw | Source-visible. Standalone documentation and tests discuss smart-pointer forms; Djehuti host examples in this pass did not validate the complete memory model. |\n| Refinements and effects | Source-visible in grammar/spec areas, but not treated as fully operational in Djehuti examples without further validation. |"
    },
    {
      "slug": "5-bindings-scope-and-mutability",
      "title": "5 Bindings, Scope, and Mutability",
      "summary": "Local bindings use let with optional mut and optional type annotation. Function parameters are typed. Blocks create expression scopes and can return a trailing value. Cur...",
      "content": "Local bindings use let with optional mut and optional type annotation. Function parameters are typed. Blocks create expression scopes and can return a trailing value. Current gap notes indicate destructuring in let declarations, function parameters, and for-loop bindings is not implemented.\n\npub fn reduce(x: f64) -> f64 = {\n\nlet mut y: f64 = x;\n\nwhile y > 0.1 {\n\ny = y * 0.5;\n\n};\n\ny\n\n}"
    },
    {
      "slug": "6-operators-and-expressions",
      "title": "6 Operators and Expressions",
      "summary": "Primary expressions include literals, identifiers, paths, parenthesized expressions, blocks, if expressions, match expressions, while, loop, range for loops, break, conti...",
      "content": "| Precedence group | Operators or forms |\n| --- | --- |\n| Assignment | = |\n| Bitwise or/xor/and | \\|, ^, & |\n| Equality | ==, != |\n| Comparison | <, >, <=, >= |\n| Shift | <<, >> |\n| Additive | +, - |\n| Multiplicative | *, /, % |\n| Cast | as |\n| Unary | -, !, pointer dereference * |\n| Postfix | function call, member access, index access, turbofish-style generic call |\n\nPrimary expressions include literals, identifiers, paths, parenthesized expressions, blocks, if expressions, match expressions, while, loop, range for loops, break, continue, array literals, self, struct literals, smart pointer construction, closures, build_time, quote, unquote, perform, resume, and handle."
    },
    {
      "slug": "7-control-flow",
      "title": "7 Control Flow",
      "summary": "Documentation section from the published manual.",
      "content": "| Form | Implemented or source-visible behavior |\n| --- | --- |\n| if / else | Expression form is grammar-visible and used by generated node programs. |\n| while | Verified by frust_engine_smoke.exe with a mutable f64 loop. |\n| loop | Grammar-visible loop expression with break/continue support. |\n| for | Range form only: for identifier in start .. end block. No general iterator protocol is documented as verified. |\n| match | Grammar-visible and linked to enum support; match syntax uses match (expr) { pattern => expr }. Some pattern forms remain partial. |\n| break/continue | Grammar-visible in loops. |"
    },
    {
      "slug": "8-functions-generics-and-calls",
      "title": "8 Functions, Generics, and Calls",
      "summary": "Functions use typed parameters and may return a typed value. Generic parameters and generic calls are source-visible. The smoke tests directly validate native lookup and ...",
      "content": "Functions use typed parameters and may return a typed value. Generic parameters and generic calls are source-visible. The smoke tests directly validate native lookup and execution of f64 functions. Broader generic behavior should be validated feature by feature before examples rely on it.\n\npub fn gain(x: f64, g: f64) -> f64 = {\n\nx * g\n\n}"
    },
    {
      "slug": "9-data-structures-and-patterns",
      "title": "9 Data Structures and Patterns",
      "summary": "Documentation section from the published manual.",
      "content": "| Feature | Status |\n| --- | --- |\n| Array literals and indexing | Source-visible; Array<f64,N> parameter writes are smoke-tested. |\n| Struct literals and member access | Source-visible; practical Djehuti examples still need focused validation. |\n| Enums and variant patterns | Source-visible; gap notes indicate v1 match limitations. |\n| Wildcard patterns | Source-visible through underscore. |\n| Struct patterns | Parse support is noted, but code generation is listed as incomplete in gap notes. |\n| String literal patterns | Gap notes warn that this area is rejected or unsafe because string equality is not value-safe in the current path. |\n| Match guards | Not implemented in the reviewed gap notes. |\n| Nested exhaustiveness | Top-level exhaustiveness is the focus; nested sub-pattern exhaustiveness is not separately documented as complete. |"
    },
    {
      "slug": "10-modules-manifests-and-host-boundaries",
      "title": "10 Modules, Manifests, and Host Boundaries",
      "summary": "The compiler API accepts source strings and source providers. The source-visible HostEnvironment routes file access, logging, and host I/O through virtual paths. Djehuti ...",
      "content": "The compiler API accepts source strings and source providers. The source-visible HostEnvironment routes file access, logging, and host I/O through virtual paths. Djehuti uses manifest text when wrapping scripts and plugin units.\n\n| Boundary | Practical documentation rule |\n| --- | --- |\n| Standalone FRust | Language, compiler, standard libraries, plugin host, IDE, and standalone tools belong to the FrustLang project. |\n| Djehuti script host | Adds runScript wrapper behavior and print_line logging. |\n| Djehuti programmable component host | Adds pc_* host functions and solver state semantics. |\n| Djehuti node compiler | Converts graph JSON to FRust source and validates generated source. |\n| Xyce backend | Does not execute FRust code in the reviewed implementation. |"
    },
    {
      "slug": "11-compiler-jit-and-diagnostics",
      "title": "11 Compiler, JIT, and Diagnostics",
      "summary": "The embeddable CompilerApi accepts source files, pod providers, and host environment services. It can produce diagnostics and compiled object data without requiring direc...",
      "content": "The embeddable CompilerApi accepts source files, pod providers, and host environment services. It can produce diagnostics and compiled object data without requiring direct filesystem writes. Djehuti then loads generated source through frust_plugin_host and looks up exported functions by name.\n\n| Capability | Verified evidence |\n| --- | --- |\n| Compile and run script | frust_engine_smoke.exe: script returns hello from Frust. |\n| Line diagnostics | frust_engine_smoke.exe: syntax error reported as script.frust line 2. |\n| Load/reload native functions | frust_engine_smoke.exe: same key reload changed behavior to 2.5. |\n| Host math externs | frust_engine_smoke.exe: exp/log extern host functions passed. |\n| Debugger ticks | frust_debugger_tests.exe: compiled code called runtime frust_dbg_tick export. |\n| Generated plugin path | generated_gain_smoke.exe: embedded FRust model loaded and processed audio gain on both channels. |"
    },
    {
      "slug": "12-djehuti-host-integration-reference",
      "title": "12 Djehuti Host Integration Reference",
      "summary": "Djehuti host integration is intentionally listed apart from language syntax. These functions and behaviors are available because the workbench registers them with the run...",
      "content": "Djehuti host integration is intentionally listed apart from language syntax. These functions and behaviors are available because the workbench registers them with the runtime; they should not be documented as portable FRust language features.\n\n| Integration point | Behavior |\n| --- | --- |\n| FrustEngine.runScript | Compiles and runs a script that defines pub fn run() -> String; captures print_line output and the return string. |\n| FrustExecution executor | Runs/debugs one program at a time on a worker thread and exposes snapshot state for UI panels. |\n| NodeCompiler | Converts node graphs to real FRust source and validates generated source. |\n| Programmable components | Evaluate compiled compute functions during internal solver iterations with host calls for pins, time, params, and state. |\n| Generated DSP plugin smoke | Shows a separate generated plugin path that loads an embedded FRust model and applies a gain parameter. |"
    },
    {
      "slug": "13-unsupported-partial-or-unvalidated-features",
      "title": "13 Unsupported, Partial, or Unvalidated Features",
      "summary": "Documentation section from the published manual.",
      "content": "| Feature area | Reference status |\n| --- | --- |\n| Rust borrow checker/lifetimes | Do not assume Rust semantics. No Rust borrow-checker model is documented from this pass. |\n| Destructuring | Not implemented for let declarations, function parameters, or for-loop bindings in reviewed gap notes. |\n| Match guards | Not implemented in reviewed gap notes. |\n| Struct-pattern code generation | Struct patterns parse but are not documented as codegen-complete. |\n| String patterns | Not documented as safe value matching; gap notes identify string comparison concerns. |\n| General iterator for loops | Only range form is documented from grammar review. |\n| Complete standard library catalog | Out of scope for Rev 0.1; requires separate inventory of pods and library modules. |\n| GUI workflow validation | Compiler/runtime smoke tests passed. Live GUI screenshots and acceptance walkthroughs remain open. |"
    },
    {
      "slug": "14-minimal-verified-examples",
      "title": "14 Minimal Verified Examples",
      "summary": "pub fn run() -> String = {",
      "content": "pub fn run() -> String = {\n\nprint_line(\"first\");\n\n\"done\"\n\n}\n\npub fn gain(x: f64, g: f64) -> f64 = {\n\nx * g\n\n}\n\nThese examples are intentionally small because they are backed by executable smoke evidence. Larger examples should cite their own run evidence or be clearly marked as source-visible but not practically verified."
    },
    {
      "slug": "revision-history",
      "title": "Revision History",
      "summary": "Documentation section from the published manual.",
      "content": "| Revision | Date | Author | Summary |\n| --- | --- | --- | --- |\n| 0.1 | 2026-10-13 | Codex documentation pass | Initial FRust language reference based on lexer/parser/API/source review, smoke tests, and known-gap notes. |"
    }
  ]
}
