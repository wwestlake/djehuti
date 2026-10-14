import type { DocManual } from '../electronicsDocs'

export const doc020: DocManual = {
  "slug": "installation-and-getting-started",
  "title": "Djehuti Electronics Lab Installation and Getting Started",
  "documentId": "DJEHUTI-DOC-020",
  "revision": "0.1",
  "status": "Initial draft pending installer verification",
  "date": "2026-10-13",
  "sections": [
    {
      "slug": "overview",
      "title": "Overview",
      "summary": "Initial installation orientation and first simulation exercise",
      "content": "![Documentation figure](/docs/electronics-work-bench/doc-030/djehuti-electronics-lab-splash.png)\n\nInitial installation orientation and first simulation exercise\n\n| Field | Value |\n| --- | --- |\n| Publisher | LagDaemon Software |\n| Application | Djehuti Electronics Lab |\n| Document identifier | DJEHUTI-DOC-020 |\n| Revision | 0.1 |\n| Date | 2026-10-13 |\n| Status | Initial draft pending installer verification |\n| Original baseline | 76c6049 |\n| Current source consulted | 76c6049 |\n\nSource basis: Original documentation baseline was Git HEAD 76c6049. Current focused review on 2026-10-13 consulted the same committed HEAD, with uncommitted Machine Builder and Control Panel changes present. No application source files were changed during this documentation pass."
    },
    {
      "slug": "1-purpose-and-scope",
      "title": "1 Purpose and Scope",
      "summary": "This guide introduces installation status, prerequisites, first launch orientation, project opening, and a first simulation exercise for Djehuti Electronics Lab. The rele...",
      "content": "This guide introduces installation status, prerequisites, first launch orientation, project opening, and a first simulation exercise for Djehuti Electronics Lab. The release installer is still under development, so installer dependent steps are marked pending verification."
    },
    {
      "slug": "2-verification-state",
      "title": "2 Verification State",
      "summary": "Documentation section from the published manual.",
      "content": "| Topic | Status |\n| --- | --- |\n| Release installer | Pending verification. Do not treat this draft as final installer documentation. |\n| Developer build path | Source README shows a CMake build path using JUCE. This is developer information, not final end user installation guidance. |\n| Xyce external simulator | Implemented as an external backend. Setup path and supported versions require final installer confirmation. |\n| First simulation exercise | Uses the existing Audio Mixer project artifact and observed ERC/Xyce output files. |"
    },
    {
      "slug": "3-prerequisites",
      "title": "3 Prerequisites",
      "summary": "Documentation section from the published manual.",
      "content": "- A Windows desktop environment capable of running the application.\n\n- Access to the Djehuti Electronics Lab project folder and examples.\n\n- Xyce installed and configured if Xyce-backed analyses are required.\n\n- Google Drive for Desktop only if the user wants synchronized documentation or project files.\n\n- Developer build prerequisites, including JUCE and CMake, only for source builds."
    },
    {
      "slug": "4-first-launch-orientation",
      "title": "4 First Launch Orientation",
      "summary": "The application opens as a workbench with schematic and related engineering panels. Common toolbar controls observed in the implementation include New, Open, ERC, Analyti...",
      "content": "The application opens as a workbench with schematic and related engineering panels. Common toolbar controls observed in the implementation include New, Open, ERC, Analytics, Compile Preview, Stamp Mode, Snap, and zoom controls. The exact installer shortcut, initial project location, and welcome behavior are pending final verification."
    },
    {
      "slug": "5-opening-a-project",
      "title": "5 Opening a Project",
      "summary": "Documentation section from the published manual.",
      "content": "1. Start the application.\n\n1. Use Open to select an existing project folder or create a new project using the project workflow.\n\n1. Open the desired diagram from the project diagrams folder.\n\n1. Confirm that the schematic tab displays the expected circuit and that the status or console area reports the diagram as opened."
    },
    {
      "slug": "6-first-simulation-exercise-audio-mixer",
      "title": "6 First Simulation Exercise Audio Mixer",
      "summary": "The current project contains an Audio Mixer diagram that is suitable for a first orientation exercise because it includes sources, passives, an op amp, named nets, power ...",
      "content": "The current project contains an Audio Mixer diagram that is suitable for a first orientation exercise because it includes sources, passives, an op amp, named nets, power rails, ERC output, analytics setup, and Xyce output artifacts.\n\n1. Open the Audio Mixer diagram from the project diagrams folder.\n\n1. Run ERC. The existing report for this draft records 42 components, 41 wires, zero errors, and zero warnings.\n\n1. Open Analytics and review the configured transient or operating point settings.\n\n1. Run an operating point or transient analysis with the Xyce backend if Xyce is configured.\n\n1. Inspect MIX_OUT, SUM, P15V, and N15V results. The operating point result previously observed for this test held P15V at 15 V, N15V at -15 V, and the AC-coupled audio nodes near 0 V.\n\n1. Open the generated Xyce netlist when model fidelity matters. The existing output artifact for this diagram includes the UA741 subcircuit and an op amp instance using UA741."
    },
    {
      "slug": "7-troubleshooting",
      "title": "7 Troubleshooting",
      "summary": "Documentation section from the published manual.",
      "content": "| Symptom | Likely cause | Action |\n| --- | --- | --- |\n| Xyce analysis cannot start | Xyce executable is missing or not configured. | Run or review the Xyce setup workflow after installer guidance is finalized. |\n| Named model is missing | Model library does not contain the required .MODEL or .SUBCKT. | Use the part fidelity label and generated netlist error to identify the missing model. |\n| ERC passes but behavior is wrong | ERC checks connectivity, not circuit intent. | Run simulation, inspect waveforms, and compare results against expected engineering behavior. |\n| Google Drive file is not visible online | Drive sync has not completed. | Wait for sync or check Google Drive for Desktop status. |"
    },
    {
      "slug": "8-pending-verification",
      "title": "8 Pending Verification",
      "summary": "Documentation section from the published manual.",
      "content": "- Final installer name, version, and install path.\n\n- First launch behavior and default project location.\n\n- Bundled or user-installed Xyce policy.\n\n- Exact screenshots for the first launch and first simulation exercise."
    },
    {
      "slug": "revision-history",
      "title": "Revision History",
      "summary": "Documentation section from the published manual.",
      "content": "| Revision | Date | Author | Summary |\n| --- | --- | --- | --- |\n| 0.1 | 2026-10-13 | Codex documentation pass | Initial draft with installer dependent steps marked pending verification. |"
    }
  ]
}
