import type { DocManual } from '../electronicsDocs'

export const doc010: DocManual = {
  "slug": "product-overview",
  "title": "Djehuti Electronics Lab Product Overview",
  "documentId": "DJEHUTI-DOC-010",
  "revision": "0.2",
  "status": "Corrected draft ready for next independent review",
  "date": "2026-10-13",
  "sections": [
    {
      "slug": "overview",
      "title": "Overview",
      "summary": "Professional overview for users developers and reviewers",
      "content": "![Documentation figure](/docs/electronics-work-bench/doc-030/djehuti-electronics-lab-splash.png)\n\nProfessional overview for users developers and reviewers\n\n| Field | Value |\n| --- | --- |\n| Publisher | LagDaemon Software |\n| Application | Djehuti Electronics Lab |\n| Document identifier | DJEHUTI-DOC-010 |\n| Revision | 0.2 |\n| Date | 2026-10-13 |\n| Status | Corrected draft ready for next independent review |\n| Original baseline | 76c6049 |\n| Current source consulted | 76c6049 |\n\nSource basis: Original documentation baseline was Git HEAD 76c6049. Current focused review on 2026-10-13 consulted the same committed HEAD, with uncommitted Machine Builder and Control Panel changes present. No application source files were changed during this documentation pass."
    },
    {
      "slug": "1-introduction",
      "title": "1 Introduction",
      "summary": "Djehuti Electronics Lab is a desktop engineering workbench for building, simulating, instrumenting, and documenting electronic and electromechanical systems. It combines ...",
      "content": "Djehuti Electronics Lab is a desktop engineering workbench for building, simulating, instrumenting, and documenting electronic and electromechanical systems. It combines schematic capture, circuit analysis, programmable components, machine simulation, instrument style data viewing, PCB preparation, and an integrated assistant tool surface.\n\nThis overview explains what the application is intended to do, how its major systems fit together, and what kinds of engineering workflows it supports in the current implementation. It is written for engineers, students, technicians, technically capable hobbyists, developers, and reviewers who need a realistic picture of the product.\n\nThe current product state is a working prototype with active development still occurring. Features that exist in source but still need hands on workflow validation are identified as requiring practical validation rather than presented as final release behavior."
    },
    {
      "slug": "2-product-purpose",
      "title": "2 Product Purpose",
      "summary": "The purpose of Djehuti Electronics Lab is to make electronics design and experimentation feel like one continuous environment. A user can sketch a circuit, run analyses, ...",
      "content": "The purpose of Djehuti Electronics Lab is to make electronics design and experimentation feel like one continuous environment. A user can sketch a circuit, run analyses, inspect results, create programmable behavior, assemble larger machine systems, and prepare related PCB or documentation artifacts without moving between unrelated tools for every step.\n\nThe application is especially oriented toward engineering experiments, teaching, testing, and rapid design exploration. This draft does not present the prototype as a certified replacement for production signoff tools. It presents it as a laboratory environment where circuit behavior, component models, control logic, and system level behavior can be explored and recorded."
    },
    {
      "slug": "3-intended-users",
      "title": "3 Intended Users",
      "summary": "Documentation section from the published manual.",
      "content": "| User group | Primary needs |\n| --- | --- |\n| Electronics engineers | Build circuits, choose models, run analyses, inspect netlists and results, and move toward PCB implementation. |\n| Students and instructors | Create reproducible exercises that explain circuit behavior, measurements, waveforms, and system response. |\n| Technicians and testers | Use instrument style views, operating point checks, plots, and recorded signals to understand test circuits. |\n| Embedded and controls developers | Use FRust programmable components and machine simulation to prototype signal processing and control behavior. |\n| Application developers | Understand architecture, file formats, model binding, assistant tools, and areas that require continued implementation. |"
    },
    {
      "slug": "4-product-architecture",
      "title": "4 Product Architecture",
      "summary": "Djehuti Electronics Lab is organized around editable project artifacts. The schematic editor creates diagrams and netlists. The analytics system runs analyses and present...",
      "content": "Djehuti Electronics Lab is organized around editable project artifacts. The schematic editor creates diagrams and netlists. The analytics system runs analyses and presents tables or traces. The internal solver handles local numerical simulation, while the Xyce backend writes netlists and runs the external Xyce simulator where supported. Machine Builder combines schematic references, programmable components, motors, ports, mechanical bodies, and control panels into higher level systems.\n\nThe integrated assistant sits beside these systems as a tool user. It can inspect registered parts, place components, route wires, run analyses, query instruments, operate Machine Builder, edit FRust programs, and help create repeatable examples. The assistant tool surface is part of the application architecture and should be documented as an engineering interface, not merely a chat feature.\n\n## 4 1 Main Subsystems\n\n| Subsystem | Role in the application |\n| --- | --- |\n| Schematic editor | Creates circuits from symbols, pins, wires, nets, supplies, and parameterized components. |\n| Analytics panel | Runs supported analyses and stores result tables and traces. |\n| Internal circuit solver | Provides modified nodal analysis and callback based features integrated directly into the application. |\n| Xyce backend | Generates Xyce netlists and runs the external Xyce executable for supported analyses and model paths. |\n| Part catalog and model libraries | Define selectable parts, parameters, simulation fidelity labels, and SPICE model bindings. |\n| Machine Builder | Assembles subsystems, motors, programmable controllers, plot panels, and ports into higher level systems. |\n| FRust and Node Designer | Provide programmable behavior for standalone programs and reusable schematic components. |\n| PCB panel | Manages board outline, footprints, placement, routing, DRC, and fabrication export paths. |\n| Control Panel and instruments | Provide user operated controls, status displays, plots, strip charts, XY plots, and measurement views. |"
    },
    {
      "slug": "5-electronics-workflow",
      "title": "5 Electronics Workflow",
      "summary": "A typical electronics workflow begins with a schematic. The user places parts from the catalog, names important nets, wires the circuit, sets values and model choices, th...",
      "content": "A typical electronics workflow begins with a schematic. The user places parts from the catalog, names important nets, wires the circuit, sets values and model choices, then runs ERC and analysis. The application can produce operating point values, time domain traces, frequency response data, and measurement tables. Where the Xyce backend is selected, the application generates a SPICE style netlist and calls the configured Xyce executable.\n\nThe source currently shows support for primitive components, independent sources, controlled sources, behavioral sources, switches, semiconductor families, op amps, comparators, regulators, mutual inductance, and model libraries. The documentation rule is truthfulness: a named real device, a generic model, an ideal component, and an unsupported part must not be described as if they had the same simulation meaning.\n\n## 5 1 Model Fidelity in the Current Implementation\n\nSimulation fidelity labels are implemented as user visible metadata. Source review found the labels exposed through the part catalog, the workbench capability catalog, schematic parameter reporting, and the Properties panel. Current labels include primitive, generic_model, vendor_model, ideal, unsupported, and a programmable label for FRust programmable components.\n\nThose labels do not by themselves prove that every part in that category has complete practical validation. They are an implemented truthfulness mechanism: they tell the user which kind of simulation representation the application believes is being used. Engine support, model availability, pin mapping, and numerical behavior still need to be verified for specific parts and workflows before final release claims are made.\n\n## 5 2 UA741 Vendor Model Evidence\n\nThe opamp_741 symbol has specific source evidence for Xyce vendor model binding. The Xyce model binding registry maps opamp_741 to the UA741 SUBCKT and defines an explicit app pin to model pin mapping: IN+ to pin 1, IN- to pin 2, V+ to pin 3, V- to pin 4, and OUT to pin 5. The bundled model library data/models/ua741_ti.lib contains a Texas Instruments sourced UA741 macromodel and a .SUBCKT UA741 declaration.\n\nThe backend also reports a clear error if the UA741 model is missing or if the expected model kind cannot be resolved. Existing Audio Mixer output artifacts show a generated Xyce netlist containing .SUBCKT UA741 and an X instance using UA741, and Xyce stdout ending the simulation. This supports the implementation claim. It does not replace final GUI acceptance testing, so the workflow remains marked as implemented with practical validation required."
    },
    {
      "slug": "6-practical-engineering-example",
      "title": "6 Practical Engineering Example",
      "summary": "A concrete implemented workflow is the four channel Audio Mixer diagram in the project diagrams folder. The diagram uses four sine input sources, coupling capacitors, lev...",
      "content": "A concrete implemented workflow is the four channel Audio Mixer diagram in the project diagrams folder. The diagram uses four sine input sources, coupling capacitors, level controls, input resistors, a summing node, an opamp_741 stage, feedback compensation, output coupling, a load resistor, and positive and negative supply rails.\n\nThe value of this example is that it exercises several application layers together: schematic construction, named nets, source waveforms, op amp model binding, ERC, Xyce netlist generation, transient analysis setup, and result file creation. The local ERC report for this diagram lists 42 components, 41 wires, zero errors, and zero warnings. The generated Xyce netlist records the UA741 subcircuit and the op amp instance line, then requests transient output for rails, summing node, and mixer output signals.\n\nThis is an engineering demonstration of workflow integration, not a final audio product specification. Final tutorial text should still verify expected gain, frequency response, clipping margin, and noise behavior before presenting the mixer as a completed audio design."
    },
    {
      "slug": "7-machine-and-systems-workflow",
      "title": "7 Machine and Systems Workflow",
      "summary": "Machine Builder extends the application beyond isolated circuits. It provides an assembly canvas for system level designs that may combine schematic subsystems, FRust con...",
      "content": "Machine Builder extends the application beyond isolated circuits. It provides an assembly canvas for system level designs that may combine schematic subsystems, FRust controllers, DC motors, input and output ports, mechanical bindings, control panels, and plot panels. Toolbar commands in source include creating, loading, saving, validating, compiling, starting, stopping, resetting, and opening referenced subsystem models.\n\nThe current implementation includes built in electromechanical concepts such as DC motors, shafts, tachometer style signals, position sensing, speed, torque, current, and load. Control panels can expose controls and status indicators for runtime operation, including emergency stop and plot panel widgets. These features should be documented with practical examples after hands on validation."
    },
    {
      "slug": "8-frust-programmable-components",
      "title": "8 FRust Programmable Components",
      "summary": "FRust integration allows behavior to be expressed as programs and node based definitions. Programmable schematic components can define pins, parameters, state variables, ...",
      "content": "FRust integration allows behavior to be expressed as programs and node based definitions. Programmable schematic components can define pins, parameters, state variables, and node programs. Each placed instance keeps its own state during simulation. The solver can rerun these programs during internal numerical iterations so program output can depend on circuit voltage and current conditions.\n\nThe Xyce backend cannot call application code during its own Newton iterations. For that reason, FRust programmable components and other callback based models must either run through the internal solver or be represented by an explicit Xyce compatible model. The manuals should treat this as an important engine selection rule."
    },
    {
      "slug": "9-instruments-and-data",
      "title": "9 Instruments and Data",
      "summary": "The application includes instrument style data views and analysis outputs. Source evidence shows oscilloscope style channel readings, multimeter functions, frequency anal...",
      "content": "The application includes instrument style data views and analysis outputs. Source evidence shows oscilloscope style channel readings, multimeter functions, frequency analyzer behavior, 2D and 3D plotter modes, analytics result history, and machine plot panels. The machine plot panel includes strip chart and XY plot modes and is intended for slow moving or multichannel system signals.\n\nThe documentation should distinguish three related concepts: acquisition sampling, simulation time stepping, and display timebase. A signal recorder can display a long time window even when the underlying simulator uses smaller adaptive or configured steps. This distinction will be important for the Instruments and Data Acquisition manual."
    },
    {
      "slug": "10-pcb-and-fabrication-workflow",
      "title": "10 PCB and Fabrication Workflow",
      "summary": "The PCB system is visible in the tool surface and source. It supports board queries, standard board sizes, outline and cutout editing, mounting holes, stackup selection, ...",
      "content": "The PCB system is visible in the tool surface and source. It supports board queries, standard board sizes, outline and cutout editing, mounting holes, stackup selection, schematic synchronization, footprint assignment, placement, routing, route rule settings, DRC, netlist verification, board text and graphics, fabrication rules, and fabrication export.\n\nThe first documentation pass should present PCB features as implemented but requiring practical validation until export formats, fabrication assumptions, and DRC rule coverage are confirmed in running workflows."
    },
    {
      "slug": "11-integrated-assistant",
      "title": "11 Integrated Assistant",
      "summary": "Djehuti Electronics Lab includes an assistant facing API that can work with the application rather than merely describe it. The source exposes tools for schematic editing...",
      "content": "Djehuti Electronics Lab includes an assistant facing API that can work with the application rather than merely describe it. The source exposes tools for schematic editing, analytics, instruments, PCB work, FRust programs, programmable components, machines, control panels, mechanical views, and demonstrations. This creates a strong foundation for an assistant that can build examples, inspect parameters, and help the user operate the workbench.\n\nBecause assistant tools can modify diagrams and project artifacts, documentation should describe the assistant as an operator with tool access. It should also explain review practices: users should inspect generated diagrams, verify simulations, and keep project artifacts under source control when work becomes important."
    },
    {
      "slug": "12-data-and-publication-model",
      "title": "12 Data and Publication Model",
      "summary": "The application works with ordinary local files. Diagrams, machine assemblies, programmable component definitions, model libraries, ERC reports, result files, and documen...",
      "content": "The application works with ordinary local files. Diagrams, machine assemblies, programmable component definitions, model libraries, ERC reports, result files, and documentation can be stored in project folders. This is useful for source control, review, and repeatability. It also allows the documentation library itself to be managed as synchronized editable files under Google Drive.\n\nThe documentation set should eventually include file format references for diagram JSON, machine JSON, programmable component JSON, output reports, model libraries, and export products. Those references should be based on source and examples rather than guessed schemas."
    },
    {
      "slug": "13-current-maturity",
      "title": "13 Current Maturity",
      "summary": "Documentation section from the published manual.",
      "content": "| Area | Current maturity |\n| --- | --- |\n| Circuit editing and simulation | Broad implementation exists. Core workflows need final user facing screenshots and targeted examples. |\n| Model fidelity labels | Implemented as visible labels in source. They are not the same as complete validation for every part model. |\n| Xyce model binding | Specific binding evidence exists for UA741 and other registry entries. Practical validation remains part specific. |\n| Machine Builder and control panels | Substantial implementation exists, with ongoing local changes in the working tree. |\n| FRust programmable components | Substantial source implementation exists. Language reference and API details need authoritative confirmation. |\n| PCB workflows | Feature surface is visible. Final documentation should verify export products and rule behavior. |\n| Installer and onboarding | Installer is still being developed. Installation instructions must be marked pending until verified. |"
    },
    {
      "slug": "14-example-user-journey",
      "title": "14 Example User Journey",
      "summary": "Documentation section from the published manual.",
      "content": "1. Create or open a project and open the schematic editor.\n\n1. Place components, connect pins, name important nets, and set component values.\n\n1. Run ERC and correct any reported connection problems.\n\n1. Choose the appropriate simulation engine for the model types in the design.\n\n1. Run operating point, transient, AC, or another supported analysis.\n\n1. Inspect result tables and traces, then adjust component values or model choices.\n\n1. Move to Machine Builder when the circuit is part of a controlled electromechanical system.\n\n1. Use plot panels, analytics, and saved project files to preserve the experiment for review."
    },
    {
      "slug": "revision-history",
      "title": "Revision History",
      "summary": "Documentation section from the published manual.",
      "content": "| Revision | Date | Author | Summary |\n| --- | --- | --- | --- |\n| 0.1 | 2026-10-13 | Codex documentation pass | Initial source based draft for review. |\n| 0.2 | 2026-10-13 | Codex documentation pass | Addressed independent review findings: clarified model fidelity labels, added UA741 evidence, added Audio Mixer example, removed internal roadmap, and refreshed document structure. |"
    }
  ]
}
