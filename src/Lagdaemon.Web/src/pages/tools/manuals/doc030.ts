import type { DocManual } from '../electronicsDocs'

export const doc030: DocManual = {
  "slug": "user-guide",
  "title": "Djehuti Electronics Lab User Guide",
  "documentId": "DJEHUTI-DOC-030",
  "revision": "0.1",
  "status": "Initial draft pending GUI validation screenshots",
  "date": "2026-10-13",
  "sections": [
    {
      "slug": "overview",
      "title": "Overview",
      "summary": "Initial operating guide for the workbench interface",
      "content": "![Documentation figure](/docs/electronics-work-bench/doc-030/djehuti-electronics-lab-splash.png)\n\nInitial operating guide for the workbench interface\n\n| Field | Value |\n| --- | --- |\n| Publisher | LagDaemon Software |\n| Application | Djehuti Electronics Lab |\n| Document identifier | DJEHUTI-DOC-030 |\n| Revision | 0.1 |\n| Date | 2026-10-13 |\n| Status | Initial draft pending GUI validation screenshots |\n| Original baseline | 76c6049 |\n| Current source consulted | 76c6049 |\n\nSource basis: Original documentation baseline was Git HEAD 76c6049. Current focused review on 2026-10-13 consulted the same committed HEAD, with uncommitted Machine Builder and Control Panel changes present. No application source files were changed during this documentation pass."
    },
    {
      "slug": "1-purpose-and-scope",
      "title": "1 Purpose and Scope",
      "summary": "This User Guide describes the main operating concepts of Djehuti Electronics Lab as currently visible in source and project artifacts. It is an initial draft and should b...",
      "content": "This User Guide describes the main operating concepts of Djehuti Electronics Lab as currently visible in source and project artifacts. It is an initial draft and should be reviewed against the running application before final publication."
    },
    {
      "slug": "2-workbench-orientation",
      "title": "2 Workbench Orientation",
      "summary": "The workbench is organized around project artifacts and editor panels. The primary experience includes a schematic editor, PCB view, analytics tools, control and machine ...",
      "content": "The workbench is organized around project artifacts and editor panels. The primary experience includes a schematic editor, PCB view, analytics tools, control and machine panels, console output, and an assistant tool surface. The toolbar exposes common actions such as creating or opening work, running ERC, opening Analytics, compiling previews, toggling Stamp Mode, snapping to the grid, and changing zoom."
    },
    {
      "slug": "3-projects-and-diagrams",
      "title": "3 Projects and Diagrams",
      "summary": "Documentation section from the published manual.",
      "content": "- Projects hold diagrams, outputs, model references, generated reports, and related artifacts.\n\n- Diagrams are stored as editable JSON files and can be opened by name from the project.\n\n- Outputs such as ERC reports and generated Xyce files are written under project output folders.\n\n- Because project artifacts are text or ordinary files, they are suitable for source control once a project becomes important."
    },
    {
      "slug": "4-schematic-editing",
      "title": "4 Schematic Editing",
      "summary": "The schematic editor provides component placement, wiring, net labels, power rails, ground symbols, parameter editing, grouping, and hierarchical sub diagrams. Wires can ...",
      "content": "The schematic editor provides component placement, wiring, net labels, power rails, ground symbols, parameter editing, grouping, and hierarchical sub diagrams. Wires can be routed and adjusted without changing connectivity when the tool refuses illegal moves.\n\n| Task | User action | Documentation status |\n| --- | --- | --- |\n| Place a part | Choose a component from the catalog or assistant tool surface and place it on the schematic. | Implemented in source; screenshots pending. |\n| Connect pins | Draw wires between pins, labels, supplies, and ground. | Implemented in source; workflow examples pending. |\n| Edit values | Use Properties or parameter tools to change value, model, waveform, or instrument settings. | Implemented in source; final property names should be verified in UI. |\n| Check the circuit | Run ERC and read errors, warnings, and info findings. | Implemented; examples should distinguish ERC from simulation validation. |"
    },
    {
      "slug": "5-simulation-fidelity",
      "title": "5 Simulation Fidelity",
      "summary": "Parts expose a simulation fidelity label. The label helps users distinguish primitive parts, generic models, vendor models, ideal behavior, unsupported symbols, and progr...",
      "content": "Parts expose a simulation fidelity label. The label helps users distinguish primitive parts, generic models, vendor models, ideal behavior, unsupported symbols, and programmable components. The label is a truthfulness aid, not a guarantee that every analysis and engine combination has been practically validated."
    },
    {
      "slug": "6-analytics-and-simulation",
      "title": "6 Analytics and Simulation",
      "summary": "Analytics runs analyses on the open diagram. The internal solver and the Xyce backend do not support identical feature sets. Xyce-backed runs generate netlists and extern...",
      "content": "Analytics runs analyses on the open diagram. The internal solver and the Xyce backend do not support identical feature sets. Xyce-backed runs generate netlists and external result files. Internal solver paths support callback based features that Xyce cannot call directly.\n\n- Use operating point for static node voltages and currents.\n\n- Use transient analysis for time domain behavior.\n\n- Use AC or Bode style analysis for small signal frequency response where supported.\n\n- Use generated netlists and result files when model binding or simulator behavior must be reviewed."
    },
    {
      "slug": "7-instruments-and-results",
      "title": "7 Instruments and Results",
      "summary": "Instrument style views include oscilloscope, multimeter, frequency analyzer, plotter, and machine plot panel concepts. Plot panels can present strip chart and XY style vi...",
      "content": "Instrument style views include oscilloscope, multimeter, frequency analyzer, plotter, and machine plot panel concepts. Plot panels can present strip chart and XY style views for slower multichannel system signals. The final instruments manual should specify sampling, display timebase, and export formats in detail."
    },
    {
      "slug": "8-pcb-workflow",
      "title": "8 PCB Workflow",
      "summary": "The PCB tab manages board shape, standard board options, holes, cutouts, stackup, footprint selection, schematic synchronization, placement, routing, DRC, netlist verific...",
      "content": "The PCB tab manages board shape, standard board options, holes, cutouts, stackup, footprint selection, schematic synchronization, placement, routing, DRC, netlist verification, text, graphics, fabrication rules, and fabrication export. This draft does not yet verify export file contents."
    },
    {
      "slug": "9-machine-builder-and-control-panel",
      "title": "9 Machine Builder and Control Panel",
      "summary": "Machine Builder assembles schematic references, FRust controllers, DC motors, plot panels, machine inputs and outputs, mechanical bodies, joints, geometry, and control pa...",
      "content": "Machine Builder assembles schematic references, FRust controllers, DC motors, plot panels, machine inputs and outputs, mechanical bodies, joints, geometry, and control panels. Source review shows commands for validating, compiling, starting, stopping, resetting, and opening referenced models."
    },
    {
      "slug": "10-frust-and-programmable-components",
      "title": "10 FRust and Programmable Components",
      "summary": "FRust programmable components can define pins, parameters, state, and behavior programs. They are designed for internal solver interaction. Xyce cannot call FRust code du...",
      "content": "FRust programmable components can define pins, parameters, state, and behavior programs. They are designed for internal solver interaction. Xyce cannot call FRust code during its own numerical iterations, so Xyce workflows require explicit compatible models or should avoid those programmable components."
    },
    {
      "slug": "11-assistant-operation",
      "title": "11 Assistant Operation",
      "summary": "The integrated assistant has access to application tools for schematic editing, analytics, instruments, PCB, machine operations, FRust programs, programmable components, ...",
      "content": "The integrated assistant has access to application tools for schematic editing, analytics, instruments, PCB, machine operations, FRust programs, programmable components, and generated reports. Users should treat assistant actions as real edits to project artifacts and verify diagrams, ERC results, simulations, and generated outputs before relying on them."
    },
    {
      "slug": "12-known-draft-gaps",
      "title": "12 Known Draft Gaps",
      "summary": "Documentation section from the published manual.",
      "content": "- Screenshots and exact menu paths require live GUI validation.\n\n- Installer flow is pending verification.\n\n- Simulation analysis coverage needs a dedicated circuit simulation manual.\n\n- PCB fabrication export details need practical verification.\n\n- FRust language and API documentation require authoritative reference confirmation."
    },
    {
      "slug": "revision-history",
      "title": "Revision History",
      "summary": "Documentation section from the published manual.",
      "content": "| Revision | Date | Author | Summary |\n| --- | --- | --- | --- |\n| 0.1 | 2026-10-13 | Codex documentation pass | Initial source based User Guide draft pending live UI validation. |"
    }
  ]
}
