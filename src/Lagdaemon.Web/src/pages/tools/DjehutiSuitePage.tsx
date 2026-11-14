import { useState } from 'react'

interface Shot {
  src: string
  alt: string
  caption: string
}

interface AppInfo {
  id: string
  name: string
  tagline: string
  intro: string
  capabilities: string[]
  main: Shot
  extra: Shot[]
  status?: string
}

const SUITE_APPS: AppInfo[] = [
  {
    id: 'station',
    name: 'Djehuti Station',
    tagline: 'Audio creative workstation',
    intro:
      'Djehuti Station is a JUCE-based audio workstation for sound design and programmable audio ' +
      'workflows. It pairs a multitrack timeline with a node-graph signal lab for building tones from ' +
      'oscillators, filters, and envelopes, and it hosts VST3 plugins for layered production.',
    capabilities: [
      'Tracker view for multitrack arranging, with project timing, snap, and lane management',
      'Signal Lab node graph for building signals from oscillators, filters, motion, and envelopes, with live compile',
      'MIDI control-surface support, including X-Touch / Mackie-style surfaces and generic USB MIDI controllers',
      'VST3 plugin hosting for building layered effects and textures',
      'A built-in Virtual Engineer assistant that can build, debug, and explain patches from inside the app',
    ],
    main: {
      src: '/djehuti-suite/station-main.jpg',
      alt: 'Djehuti Station workspace showing the Tracker, Signal Lab node graph, and Virtual Engineer panel',
      caption: 'The Tracker, Signal Lab, and Virtual Engineer assistant panel side by side in the main workspace.',
    },
    extra: [
      {
        src: '/djehuti-suite/station-audio.jpg',
        alt: 'Audio device settings showing MIDI control surfaces and audio I/O',
        caption: 'Audio device setup recognizing real MIDI control surfaces (X-Touch, BCR2000, USB MIDI).',
      },
      {
        src: '/djehuti-suite/station-ai.jpg',
        alt: 'A request typed into the Virtual Engineer assistant panel',
        caption: 'The Virtual Engineer assistant, ready to take a build or debugging request for the current graph.',
      },
    ],
  },
  {
    id: 'movie',
    name: 'Djehuti Movie',
    tagline: 'Video editing and production',
    intro:
      "Djehuti Movie is the suite's Windows video editor, built on JUCE for the desktop workspace and " +
      'FFmpeg for media decoding and encoding. It is an actively evolving editor, not yet a finished ' +
      'professional editing package, but its core timeline and preview workflow already runs end to end.',
    capabilities: [
      'Source and Program monitors for previewing clips and the assembled timeline separately',
      'Multi-track timeline with placement, trimming, splitting, and ripple deletion',
      'Title overlays with configurable text and duration',
      'Gain and opacity controls, undo/redo, and background export jobs',
      'Shared suite automation: the same commands the UI uses are reachable through an authenticated local API',
    ],
    main: {
      src: '/djehuti-suite/movie-main.jpg',
      alt: 'Djehuti Movie workspace with a media library of recorded camera and microphone clips',
      caption: 'The Movie workspace: Media Library, Source/Program monitors, and a 7-track timeline.',
    },
    extra: [
      {
        src: '/djehuti-suite/movie-timeline.jpg',
        alt: 'A recorded camera clip placed on the Movie timeline with the Source monitor previewing it',
        caption: 'A clip placed on the timeline, with the Track Inspector open on the right.',
      },
      {
        src: '/djehuti-suite/movie-title.jpg',
        alt: 'The Add Title Overlay dialog in Djehuti Movie',
        caption: 'Adding a text title overlay with a configurable duration.',
      },
    ],
  },
  {
    id: 'engine',
    name: 'Djehuti Engine',
    tagline: 'Game engine & node authoring',
    intro:
      'Djehuti Engine is a game engine, a node-based authoring tool, and a dedicated multiplayer server, ' +
      'built as one JUCE codebase. The editor and runtime are the same executable in different modes, and ' +
      'its scene, scripting, and server foundations are active today.',
    capabilities: [
      '3D scene editor with an OpenGL viewport, scene hierarchy, and per-entity Transform/Material inspector',
      'Live FRust scripting per entity, with on_start / on_tick hooks compiled from the editor',
      'A node-based visual logic graph (branches, comparisons, variables, spawn/destroy) that generates FRust code directly',
      'A dedicated headless CreationEngineServer target for multiplayer, built from the same codebase',
      'A bundled FRust plugin host for suite-wide scripting capabilities',
    ],
    main: {
      src: '/djehuti-suite/engine-main.jpg',
      alt: 'Creation Engine 3D scene editor with a single box entity in the viewport',
      caption: 'The scene editor: OpenGL viewport, entity hierarchy, and inspector.',
    },
    extra: [
      {
        src: '/djehuti-suite/engine-inspector.jpg',
        alt: 'The entity inspector showing Transform, a live FRust script, and material properties',
        caption: 'Per-entity Transform, a live FRust script (on_start / on_tick), and material albedo/metallic/roughness.',
      },
      {
        src: '/djehuti-suite/engine-logic.jpg',
        alt: 'A node-based logic graph with OnStart and SetPosition nodes connected',
        caption: 'Node-based visual logic: an OnStart node wired into a SetPosition node, generating FRust code live.',
      },
    ],
  },
  {
    id: 'cad',
    name: 'Djehuti CAD',
    tagline: 'Mechanical CAD & modeling',
    intro:
      'Djehuti CAD is an independent, FRust-programmable mechanical CAD application built with JUCE, ' +
      'Open CASCADE/OCAF, and modern OpenGL. It is the most developed of the suite’s engineering ' +
      'applications, with an authoritative document model and full undo/redo.',
    capabilities: [
      'Sketch-to-solid modeling: extrude, revolve, sweep, loft, Boolean union/subtract/intersect, fillet, and chamfer',
      'Mechanical joints and couplings with constraint solving, plus local inverse-kinematics targets',
      'Pattern tools (linear, circular, mirror) and parametric gear generation as real solids',
      'A manufacturing layout workspace for placing parts on a real printer’s build plate, with per-part position, rotation, and material color',
      'A BYOK agent and FRust/node integration for scripting the same commands the GUI uses',
    ],
    main: {
      src: '/djehuti-suite/cad-main.jpg',
      alt: 'DjehutiCAD workspace showing an extruded solid model built from a sketch',
      caption: 'A sketch-to-solid part, built with the Extrude feature, in the main CAD workspace.',
    },
    extra: [
      {
        src: '/djehuti-suite/cad-mechanism.jpg',
        alt: 'A gear-pair mechanism assembly with the mechanism solved',
        caption: 'A gear-pair mechanism assembly with revolute joints and gear meshing — the solver reports "Mechanism: solved".',
      },
      {
        src: '/djehuti-suite/cad-manufacturing.jpg',
        alt: 'The manufacturing layout tool with gear parts placed on a 3D printer build plate',
        caption: 'The manufacturing layout tool, placing parts on a real Bambu Lab printer’s build plate.',
      },
    ],
  },
]

const UPCOMING_APPS: { id: string; name: string; tagline: string; note: string }[] = [
  {
    id: 'texture',
    name: 'Djehuti Texture',
    tagline: 'Texture creation and editing',
    note:
      'Texture is currently a suite application scaffold: it has the shared Djehuti Suite shell, settings, ' +
      'and project registry, but texture-authoring tools have not been built yet. We’ll add a full ' +
      'showcase once there is a working workspace to show.',
  },
  {
    id: 'electronics',
    name: 'Djehuti Electronics Work Bench',
    tagline: 'Circuit design, simulation & analysis',
    note:
      'The Electronics Work Bench has a public page on this site already, but it is an explicit ' +
      '"coming soon" placeholder: the circuit workspace, simulation engine, virtual instruments, and ' +
      'oscilloscope described for it are still being built. See the ',
  },
]

function Lightbox({ shot, onClose }: { shot: Shot | null; onClose: () => void }) {
  if (!shot) return null
  return (
    <div className="djsuite-lightbox" role="dialog" aria-modal="true" onClick={onClose}>
      <button className="djsuite-lightbox-close" onClick={onClose} aria-label="Close">
        ✕
      </button>
      <figure onClick={e => e.stopPropagation()}>
        <img src={shot.src} alt={shot.alt} />
        <figcaption>{shot.caption}</figcaption>
      </figure>
    </div>
  )
}

function AppSection({ app, onEnlarge }: { app: AppInfo; onEnlarge: (shot: Shot) => void }) {
  return (
    <section className="djsuite-app" id={app.id} aria-label={app.name}>
      <div className="djsuite-app-header">
        <h2>{app.name}</h2>
        <p className="djsuite-app-tagline">{app.tagline}</p>
      </div>
      <p className="djsuite-app-intro">{app.intro}</p>

      <button
        type="button"
        className="djsuite-shot djsuite-shot-main"
        onClick={() => onEnlarge(app.main)}
      >
        <img src={app.main.src} alt={app.main.alt} loading="lazy" />
      </button>
      <p className="djsuite-shot-caption djsuite-shot-caption-main">{app.main.caption}</p>

      <div className="djsuite-shot-grid">
        {app.extra.map(shot => (
          <div key={shot.src}>
            <button type="button" className="djsuite-shot" onClick={() => onEnlarge(shot)}>
              <img src={shot.src} alt={shot.alt} loading="lazy" />
            </button>
            <p className="djsuite-shot-caption">{shot.caption}</p>
          </div>
        ))}
      </div>

      <div className="djsuite-capabilities">
        <h3>Capabilities</h3>
        <ul>
          {app.capabilities.map(c => (
            <li key={c}>{c}</li>
          ))}
        </ul>
      </div>
    </section>
  )
}

export default function DjehutiSuitePage() {
  const [enlarged, setEnlarged] = useState<Shot | null>(null)

  return (
    <div className="community-page djsuite-page">
      <section className="djsuite-hero">
        <p className="djsuite-kicker">Djehuti Desktop Tools</p>
        <h1 className="community-page-title">The Djehuti desktop application suite</h1>
        <p className="djsuite-intro">
          Djehuti is an evolving, integrated suite of desktop applications for audio production, video
          editing, real-time 3D, and mechanical design. The screenshots below are taken directly from each
          application running locally &mdash; not mockups.
        </p>
      </section>

      <section className="djsuite-integration" aria-label="Suite integration">
        <h2>How the suite fits together</h2>
        <p>
          Djehuti Station, Djehuti Movie, and Djehuti Engine share a common application shell: the same
          suite settings and EULA access, the same AI account and model routing, and the same project
          registry. Projects live in a shared virtual filesystem, so a project opened in one app is the
          same project the others see &mdash; Djehuti Movie, for example, read and wrote to a project named
          by Djehuti Station in our own testing. Station, Engine, and the CAD and Developer tools all host a
          FRust scripting runtime, so plugins, node graphs, and generated code follow the same language
          across apps. Djehuti CAD and the Electronics Work Bench are developed independently of that
          shared suite shell, as dedicated engineering applications.
        </p>
      </section>

      {SUITE_APPS.map(app => (
        <AppSection key={app.id} app={app} onEnlarge={setEnlarged} />
      ))}

      <section className="djsuite-upcoming" aria-label="In development">
        <h2>In development</h2>
        <div className="djsuite-upcoming-grid">
          {UPCOMING_APPS.map(item => (
            <div className="djsuite-upcoming-card" key={item.id}>
              <h3>{item.name}</h3>
              <p className="djsuite-app-tagline">{item.tagline}</p>
              <p>
                {item.note}
                {item.id === 'electronics' && (
                  <a href="/electronics-work-bench">Electronics Work Bench page</a>
                )}
                {item.id === 'electronics' && ' for its current status.'}
              </p>
            </div>
          ))}
        </div>
      </section>

      <Lightbox shot={enlarged} onClose={() => setEnlarged(null)} />
    </div>
  )
}
