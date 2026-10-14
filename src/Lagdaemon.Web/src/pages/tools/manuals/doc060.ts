import type { DocManual } from '../electronicsDocs'

export const doc060: DocManual = {
  "slug": "frust-programming-guide",
  "title": "Djehuti Electronics Lab FRust Programming Guide",
  "documentId": "DJEHUTI-DOC-060",
  "revision": "0.1",
  "status": "Initial technical manual ready for review",
  "date": "2026-10-13",
  "sections": [
    {
      "slug": "overview",
      "title": "Overview",
      "summary": "Practical guide for FRust scripts, node programs, programmable components, and simulation integration",
      "content": "![Documentation figure](/docs/electronics-work-bench/doc-030/djehuti-electronics-lab-splash.png)\n\nPractical guide for FRust scripts, node programs, programmable components, and simulation integration\n\n| Field | Value |\n| --- | --- |\n| Publisher | LagDaemon Software |\n| Application | Djehuti Electronics Lab |\n| Document identifier | DJEHUTI-DOC-060 |\n| Revision | 0.1 |\n| Date | 2026-10-13 |\n| Status | Initial technical manual ready for review |\n| Original documentation baseline | 76c6049 |\n| Current Djehuti source consulted | 7c99dac |\n| Local FRust source consulted | ccc4c5f |\n| Standalone GitHub FrustLang | d14a274 |\n\nSource basis: Source review used Djehuti Git HEAD 7c99dac on 2026-10-13, with local uncommitted Control Panel and scratch/test artifacts present. The workbench CMake configuration points to local FRust source checkout; that local FRust checkout was at ccc4c5f on branch development with only untracked sim/status artifacts observed. The standalone GitHub FrustLang master reference was inspected at d14a274. This documentation pass did not modify application or FRust source."
    },
    {
      "slug": "1-purpose-and-scope",
      "title": "1 Purpose and Scope",
      "summary": "This guide explains how to use FRust in Djehuti Electronics Lab for practical engineering work. It covers standalone scripts, generated node programs, programmable electr...",
      "content": "This guide explains how to use FRust in Djehuti Electronics Lab for practical engineering work. It covers standalone scripts, generated node programs, programmable electronic components, debugging, and interaction with electrical and machine simulation artifacts.\n\nThe guide separates three things that can otherwise become blurred: the FRust language, the compiler/runtime used to execute FRust, and the Djehuti host functions that connect FRust code to schematics and machines."
    },
    {
      "slug": "2-source-baseline-and-verification-method",
      "title": "2 Source Baseline and Verification Method",
      "summary": "Documentation section from the published manual.",
      "content": "| Item | Verification basis |\n| --- | --- |\n| Djehuti workbench | Read-only review of Electronics Design Tool prototype source tree at Git HEAD 7c99dac. |\n| Local FRust checkout | local FRust source checkout at ccc4c5f; untracked sim/status files were present but no tracked changes were reported. |\n| Standalone FrustLang reference | standalone FrustLang reference checkout at GitHub master d14a274. |\n| FRust script smoke | frust_engine_smoke.exe passed: scripts, line diagnostics, f64 functions, arrays, host exp/log, while loops, negative constants. |\n| FRust debugger smoke | frust_debugger_tests.exe passed 82 checks covering breakpoints, watches, step controls, pause, stop, and worker-thread execution. |\n| Generated plugin smoke | generated_gain_smoke.exe passed: embedded FRust model loaded, gain parameter applied to both channels, state saved/restored. |\n| Motor-controller artifact | examples/model_driven_motor/nominal/SavedMotorController.frcomp.json |\n| No source changes | This documentation pass did not edit application source or run a broad build. |"
    },
    {
      "slug": "3-frust-architecture-in-djehuti",
      "title": "3 FRust Architecture in Djehuti",
      "summary": "Figure 1. FRust execution paths in the current application source.",
      "content": "![Documentation figure](/docs/electronics-work-bench/doc-060/image-01.png)\n\nFigure 1. FRust execution paths in the current application source.\n\nDjehuti embeds FRust through the FrustEngine wrapper. Scripts and generated programs are compiled in memory through CompilerApi and loaded through the frust_plugin_host runtime. The application registers host functions so FRust code can log output, call math helpers, and interact with programmable component pin and state models.\n\n| Layer | What it owns | What not to assume |\n| --- | --- | --- |\n| FRust language | Syntax, declarations, expressions, type forms, functions, structs, enums, arrays, and control flow. | Do not assume Rust behavior unless the FRust grammar, tests, or implementation supports it. |\n| Compiler/runtime | Parsing, diagnostics, object generation, JIT loading, function lookup, execution, and unload. | Passing a compiler smoke test does not prove every language feature is production-ready. |\n| Djehuti host integration | Script runner, node-program compiler, programmable component host calls, debugger executor, and simulation callbacks. | Djehuti host functions are not part of standalone FRust. |"
    },
    {
      "slug": "4-development-workflows",
      "title": "4 Development Workflows",
      "summary": "The integrated script path expects a FRust source unit that defines a public run function returning String. The application wraps the source with a manifest and host logg...",
      "content": "## 4 1 Standalone Script Workflow\n\nThe integrated script path expects a FRust source unit that defines a public run function returning String. The application wraps the source with a manifest and host logging support, compiles it, runs it, captures printed output, and unloads the temporary unit.\n\npub fn run() -> String = {\n\nprint_line(\"first\");\n\n\"done\"\n\n}\n\nVerification: frust_engine_smoke.exe reported that print_line output was captured before the return value, producing first followed by done. The same smoke also confirmed that a syntax error inside the script is reported on the user's script line, not on the hidden wrapper line.\n\n## 4 2 Node Program Workflow\n\nNode programs are graph artifacts. The NodeCompiler converts graph JSON into real FRust source, validates that source with the embedded compiler, and can export source/debug information. The source-visible v1 node set includes literals, arithmetic, comparisons, if, extern calls, and print-style output.\n\nNode debugging is adapted from FrustIDE. The source-visible executor runs programs on a worker thread, tracks Idle, Compiling, Running, Paused, Completed, Failed, and Cancelled states, and exposes breakpoints, frames, variables, watches, and step commands.\n\n## 4 3 Programmable Component Workflow\n\nA programmable electronic component combines a .frcomp.json definition with a FRust or node-generated program. The definition supplies pins, parameters, state variables, output resistance, and the program file. At run time, each schematic instance has its own state.\n\n![Documentation figure](/docs/electronics-work-bench/doc-060/image-02.png)\n\nFigure 2. Source-derived view of the SavedMotorController programmable component.\n\n| Observed item | Source-derived value |\n| --- | --- |\n| Pins | None (input); None (input); None (input); None (input); None (input); None (input); None (input); None (voltage_output); None (voltage_output); None (voltage_output) |\n| Parameters | kp=None; v_max=None; v_min=None |\n| State | run=None |\n| Node count | 46 |\n| Program file | SavedMotorController.frnode.json |\n| Output resistance | 0.05 ohm in the component definition |"
    },
    {
      "slug": "5-basic-frust-programming",
      "title": "5 Basic FRust Programming",
      "summary": "The verified script and smoke examples use public functions, typed f64 parameters, mutable local variables, while loops, array parameters, extern host functions, string r...",
      "content": "The verified script and smoke examples use public functions, typed f64 parameters, mutable local variables, while loops, array parameters, extern host functions, string returns, and print_line. These are the safest starting points for engineering examples because they are exercised by current smoke tests.\n\npub fn gain(x: f64, g: f64) -> f64 = {\n\nx * g\n\n}\n\npub fn one_pole(y: f64, x: f64, a: f64) -> f64 = {\n\ny + a * (x - y)\n\n}\n\n| Example | Verified result |\n| --- | --- |\n| gain(0.5, 3) | 1.5 |\n| one_pole after three steps toward 1.0 with a=0.5 | 0.875 |\n| while loop with mutable f64 | 0.0625 final value in smoke test |\n| negative constants | -5.0 in smoke test |\n| Array<f64,N> parameter write | host buffer update persisted; ws0 reported 0.5 |"
    },
    {
      "slug": "6-host-functions-for-programmable-components",
      "title": "6 Host Functions for Programmable Components",
      "summary": "Programmable components are evaluated through host calls implemented by Djehuti. The current internal solver calls the compiled program during Newton iterations. Program ...",
      "content": "Programmable components are evaluated through host calls implemented by Djehuti. The current internal solver calls the compiled program during Newton iterations. Program writes are tentative during a solver trial and are committed only when the step is accepted.\n\n| Host function | Purpose |\n| --- | --- |\n| djehuti_pc_v | Read a pin voltage. |\n| djehuti_pc_i | Read a pin current. |\n| djehuti_pc_time | Read simulation time. |\n| djehuti_pc_dt | Read current time step. |\n| djehuti_pc_param | Read a component parameter. |\n| djehuti_pc_state | Read instance state. |\n| djehuti_pc_set_state | Write instance state for the accepted step. |\n| djehuti_pc_drive | Set a voltage-output pin model. |\n| djehuti_pc_drive_current | Set a current-output pin model. |\n| djehuti_pc_set_resistance | Set pin output resistance. |\n\nThe pin current model recorded in source is: current into the circuit at a driven pin equals I plus the voltage error divided by R, with current returned at the reference node. That means the programmed behavior is not just a display value; it stamps an electrical model into the internal solver."
    },
    {
      "slug": "7-debugging-and-diagnostics",
      "title": "7 Debugging and Diagnostics",
      "summary": "Documentation section from the published manual.",
      "content": "| Capability | Verified or source-visible behavior |\n| --- | --- |\n| Diagnostics | frust_engine_smoke.exe confirmed script line 2 syntax diagnostics are mapped to the visible script line. |\n| Breakpoints | frust_debugger_tests.exe confirmed breakpoint pause, disabled breakpoints, hit counts, and breakpoints following edited node lines. |\n| Variables and watches | Debugger smoke confirmed stack frame variables and watch values before and after execution of a node line. |\n| Step control | Step Into, Step Over, and Step Out were exercised in function graph and state-machine tests. |\n| Stop and pause | Stop cancels paused or running programs; pause can stop a running debug session in a loop. |\n| Concurrency limit | Starting another program while one is paused is refused. |\n| Limitation | Code that enters a native call that never returns cannot be interrupted until control returns to the FRust debug tick path. |"
    },
    {
      "slug": "8-worked-engineering-examples",
      "title": "8 Worked Engineering Examples",
      "summary": "Use the run-script pattern when the task is a small calculation, validation helper, or automation snippet that returns text. The verified example prints first and returns...",
      "content": "## 8 1 Basic Standalone Script\n\nUse the run-script pattern when the task is a small calculation, validation helper, or automation snippet that returns text. The verified example prints first and returns done. Expected behavior: compile succeeds, output contains the print text before the return string.\n\n## 8 2 Numerical Calculation\n\nThe gain and one-pole examples are verified compiled functions. They are appropriate for explaining FRust numerical expressions because the smoke checks actual native function lookup and execution rather than only parser acceptance.\n\n## 8 3 Programmable Component Connected to Simulation Signals\n\nThe SavedMotorController component reads power, command, setpoint, and feedback pins; reads kp, v_min, and v_max parameters; stores run state; clamps drive voltage; and drives lamp outputs. This behavior is source-verified from the .frcomp and .frnode artifacts. A complete live GUI step-by-step acceptance run remains open.\n\n## 8 4 Feedback Control Using the Motor Controller Artifact\n\nThe model-driven motor artifact demonstrates the intended engineering pattern: a FRust programmable controller feeds a motor-drive schematic, the motor feedback signal returns to the controller, and plot channels record speed, tach, drive_out, and fault_lamp. The artifact and prior machine manual evidence support the documentation example; this guide does not claim a new live GUI validation beyond the existing smoke artifacts."
    },
    {
      "slug": "9-engineering-patterns",
      "title": "9 Engineering Patterns",
      "summary": "Documentation section from the published manual.",
      "content": "- Keep numeric behavior in small pure functions when possible, then call those functions from host-facing programs.\n\n- Treat host functions as the boundary between FRust and the simulation. Do not mix standalone language examples with Djehuti-only host calls without labeling the boundary.\n\n- Clamp voltage and current outputs before driving pins, especially when a controller reads untrusted or transient simulation feedback.\n\n- Use explicit state variables for latches and sequencers. Remember that programmable component state commits only on accepted solver steps.\n\n- Keep real-time programs finite and non-blocking. A long loop may be stoppable through debug ticks, but a native call that never returns is not a safe control strategy."
    },
    {
      "slug": "10-limitations-and-open-items",
      "title": "10 Limitations and Open Items",
      "summary": "Documentation section from the published manual.",
      "content": "| Area | Documentation status |\n| --- | --- |\n| Xyce integration | FRust programmable components are documented for the internal solver path. Xyce does not execute FRust component code in the reviewed implementation. |\n| Full language coverage | The companion language reference records source-visible syntax and known gaps. Not every grammar production has a Djehuti GUI validation result. |\n| GUI screenshots | Source and smoke evidence are strong for compiler/runtime behavior. A future review should add live editor screenshots after the Control Panel Designer changes settle. |\n| Standalone vs embedded FRust | Local FRust source checkout development revision differs from the GitHub master reference. Both revisions are recorded for traceability. |"
    },
    {
      "slug": "revision-history",
      "title": "Revision History",
      "summary": "Documentation section from the published manual.",
      "content": "| Revision | Date | Author | Summary |\n| --- | --- | --- | --- |\n| 0.1 | 2026-10-13 | Codex documentation pass | Initial FRust programming guide based on source inspection, smoke tests, and model-driven motor artifacts. |"
    }
  ]
}
