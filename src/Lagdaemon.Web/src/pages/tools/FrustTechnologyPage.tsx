import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import './FrustTechnologyPage.css'
import ShareButton from '../../components/share/ShareButton'
import { usePageSeo } from '../../components/share/usePageSeo'

const LANGUAGE_REFERENCE = '/electronics-work-bench/docs/frust-language-reference/overview'
const PROGRAMMING_GUIDE = '/electronics-work-bench/docs/frust-programming-guide/overview'

const examples = {
  gain: {
    label: 'Audio gain',
    file: 'gain.frust',
    source: 'pub fn gain(sample: f64, amount: f64) -> f64 = {\n    sample * amount\n}',
    description: 'A small, typed function scales an audio sample. With sample = 0.5 and amount = 0.5, the result is 0.25.',
  },
  filter: {
    label: 'One-pole filter',
    file: 'one_pole.frust',
    source: 'pub fn one_pole(y: f64, x: f64, a: f64) -> f64 = {\n    y + a * (x - y)\n}',
    description: 'One step of a smoothing filter: move the previous value y toward input x by coefficient a. The host retains the value between calls.',
  },
  image: {
    label: 'Image channel',
    file: 'mix_channel.frust',
    source: 'pub fn mix_channel(base: f64, layer: f64, opacity: f64) -> f64 = {\n    base + opacity * (layer - base)\n}',
    description: 'The math of a channel blend, expressed as a pure function. A video host must supply frame channels and a bounded opacity parameter; this example illustrates the calculation.',
  },
} as const

const screenshots = [
  {
    src: '/djehuti-suite/station-main.jpg',
    app: 'Djehuti Station',
    title: 'Sound design, inside the workstation',
    alt: 'Station Signal Lab authoring canvas with a Float variable, Compile button, Tracker, and Virtual Engineer panel',
    caption: 'Signal Lab brings graph authoring, parameters, and compilation into the audio workspace. This capture shows the canvas and a Float variable ready for a patch.',
  },
  {
    src: '/djehuti-suite/engine-logic.jpg',
    app: 'Djehuti Engine',
    title: 'Visual logic with source in view',
    alt: 'Engine Logic workspace with OnStart and SetPosition nodes, an inspector, and a Generated Code panel',
    caption: 'The Logic workspace places event and scene-operation nodes alongside an inspector and generated source. The two nodes in this capture have not yet been connected.',
  },
  {
    src: '/djehuti-suite/engine-inspector.jpg',
    app: 'Embedded FRust',
    title: 'Code where the scene lives',
    alt: 'Engine entity inspector with Transform properties, a Live FRust Script editor, and material controls',
    caption: 'Live FRust scripts sit beside entity properties, keeping behavior and the object it controls in the same editing context.',
  },
] as const

function Code({ source }: { source: string }) {
  // Highlight this page's fixed examples without adding a highlighter to the site bundle.
  const tokens = source.split(/(\b(?:pub|fn|let|mut|while|f64|String)\b|\/\/[^\n]*|"[^"\n]*"|\b\d+(?:\.\d+)?\b)/g)
  return <code>{tokens.map((token, index) => {
    const kind = /^(pub|fn|let|mut|while)$/.test(token) ? 'keyword'
      : /^(f64|String)$/.test(token) ? 'type'
      : /^("|\/\/)/.test(token) ? 'literal'
      : /^\d/.test(token) ? 'number' : ''
    return kind ? <span className={`frust-code-${kind}`} key={index}>{token}</span> : token
  })}</code>
}

function GainGraph() {
  return (
    <>
    <svg className="frust-node-graph frust-node-graph-wide" viewBox="0 0 560 250" role="img" aria-labelledby="frust-graph-title frust-graph-desc">
      <title id="frust-graph-title">A gain function as a node graph</title>
      <desc id="frust-graph-desc">Two f64 inputs, sample and amount, connect to Multiply. Its output connects to the f64 result. This illustrates the same sample times amount expression shown in the source example.</desc>
      <defs>
        <pattern id="frust-grid" width="20" height="20" patternUnits="userSpaceOnUse">
          <circle cx="1" cy="1" r="1" fill="currentColor" />
        </pattern>
      </defs>
      <rect className="frust-graph-grid" width="560" height="250" fill="url(#frust-grid)" />
      <g className="frust-graph-wires" fill="none" strokeWidth="2.5">
        <path d="M155 71 C205 71 185 111 235 111" />
        <path d="M155 181 C205 181 185 141 235 141" />
        <path d="M365 126 H415" />
      </g>
      <g className="frust-graph-node">
        <rect x="20" y="35" width="135" height="72" rx="8" />
        <rect x="20" y="145" width="135" height="72" rx="8" />
        <rect x="235" y="75" width="130" height="102" rx="8" />
        <rect x="415" y="90" width="125" height="72" rx="8" />
      </g>
      <g className="frust-graph-node-text">
        <text x="35" y="64">sample</text><text className="frust-graph-type" x="35" y="88">f64 · input</text>
        <text x="35" y="174">amount</text><text className="frust-graph-type" x="35" y="198">f64 · input</text>
        <text x="258" y="109">Multiply</text><text className="frust-graph-symbol" x="288" y="151">×</text>
        <text x="434" y="119">result</text><text className="frust-graph-type" x="434" y="143">f64 · output</text>
      </g>
      <g className="frust-graph-ports">
        <circle cx="155" cy="71" r="4" /><circle cx="155" cy="181" r="4" />
        <circle cx="235" cy="111" r="4" /><circle cx="235" cy="141" r="4" />
        <circle cx="365" cy="126" r="4" /><circle cx="415" cy="126" r="4" />
      </g>
    </svg>
    <svg className="frust-node-graph frust-node-graph-mobile" viewBox="0 0 280 310" role="img" aria-labelledby="frust-mobile-graph-title frust-mobile-graph-desc">
      <title id="frust-mobile-graph-title">A gain function as a node graph</title>
      <desc id="frust-mobile-graph-desc">The sample and amount f64 inputs connect downward into Multiply, then to the f64 result.</desc>
      <g className="frust-graph-wires" fill="none" strokeWidth="2.5">
        <path d="M65 80 C65 105 105 95 105 120" /><path d="M215 80 C215 105 175 95 175 120" /><path d="M140 180 V225" />
      </g>
      <g className="frust-graph-node">
        <rect x="12" y="20" width="106" height="60" rx="8" /><rect x="162" y="20" width="106" height="60" rx="8" />
        <rect x="85" y="120" width="110" height="60" rx="8" /><rect x="85" y="225" width="110" height="65" rx="8" />
      </g>
      <g className="frust-graph-node-text">
        <text x="25" y="43">sample</text><text className="frust-graph-type" x="25" y="66">f64 · input</text>
        <text x="175" y="43">amount</text><text className="frust-graph-type" x="175" y="66">f64 · input</text>
        <text x="99" y="145">Multiply</text><text className="frust-graph-symbol" x="131" y="173">×</text>
        <text x="108" y="250">result</text><text className="frust-graph-type" x="95" y="275">f64 · output</text>
      </g>
      <g className="frust-graph-ports">
        <circle cx="65" cy="80" r="4" /><circle cx="215" cy="80" r="4" /><circle cx="105" cy="120" r="4" />
        <circle cx="175" cy="120" r="4" /><circle cx="140" cy="180" r="4" /><circle cx="140" cy="225" r="4" />
      </g>
    </svg>
    </>
  )
}

export default function FrustTechnologyPage() {
  const [example, setExample] = useState<keyof typeof examples>('gain')
  const [enlarged, setEnlarged] = useState<(typeof screenshots)[number] | null>(null)
  const dialog = useRef<HTMLDialogElement>(null)
  const selected = examples[example]

  usePageSeo({
    title: 'FRust Language & Technology — Lag Daemon',
    description: 'A typed programming language, LLVM-based compiler, and embeddable runtime for programmable desktop applications.',
  })

  useEffect(() => {
    if (enlarged && !dialog.current?.open) dialog.current?.showModal()
  }, [enlarged])

  return (
    <article className="community-page frust-page">
      <header className="frust-hero">
        <p className="frust-eyebrow">The programmable foundation of Djehuti</p>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 8, flexWrap: 'wrap' }}>
          <h1>FRust Language <span>&amp; Technology</span></h1>
          <ShareButton title="FRust Language & Technology — Lag Daemon" text="A typed programming language, LLVM-based compiler, and embeddable runtime for programmable desktop applications." />
        </div>
        <p className="frust-lead">From an idea to native execution.</p>
        <p className="frust-intro">
          Write a function. Connect a graph. Build behavior into the tools you use.
          FRust combines a typed programming language, an LLVM-based compiler,
          and an embeddable runtime to make desktop applications programmable from within.
        </p>
        <div className="frust-actions">
          <a className="btn-primary" href="#frust-in-action">Explore the technology <span aria-hidden="true">↓</span></a>
          <Link className="frust-text-link" to="/djehuti-suite">See the desktop tools <span aria-hidden="true">↗</span></Link>
        </div>
        <ul className="frust-facts" aria-label="Platform foundations">
          <li>Statically typed</li><li>LLVM native code</li><li>Visual + textual authoring</li><li>Embedded execution</li>
        </ul>
      </header>

      <section className="frust-section" id="frust-in-action" aria-labelledby="frust-authoring-heading">
        <div className="frust-section-heading">
          <p className="frust-eyebrow">01 / Two ways to express an idea</p>
          <h2 id="frust-authoring-heading">Connect the behavior. Read the code.</h2>
          <p>A visual graph makes relationships visible. FRust source makes the calculation explicit.
            Graph-generated programs enter the same compiler pipeline as handwritten code.</p>
        </div>
        <div className="frust-authoring">
          <figure className="frust-graph-panel">
            <div className="frust-panel-bar"><span className="frust-status-dot" />Gain / illustrative node graph</div>
            <GainGraph />
            <figcaption>Typed inputs → multiply → output. The graph expresses <code>sample * amount</code>.</figcaption>
          </figure>
          <div className="frust-code-panel">
            <div className="frust-example-picker" role="group" aria-label="Choose a FRust code example">
              {(Object.keys(examples) as (keyof typeof examples)[]).map(key => (
                <button type="button" key={key} aria-pressed={example === key} onClick={() => setExample(key)}>{examples[key].label}</button>
              ))}
            </div>
            <div className="frust-code-filename">{selected.file}<span>FRust</span></div>
            <pre tabIndex={0} aria-label={`${selected.label} FRust source`}><Code source={selected.source} /></pre>
            <p className="frust-example-description" aria-live="polite">{selected.description}</p>
          </div>
        </div>
        <div className="frust-three-up">
          <div><h3>Expressive by design</h3><p>Typed functions, structs, enums, generics, closures, and pattern matching support more than short scripts.</p></div>
          <div><h3>Visual where it helps</h3><p>Nodes expose data flow and parameters. Graphs generate real source that the compiler can validate.</p></div>
          <div><h3>Close to the work</h3><p>Host functions connect compiled behavior to application data, controls, and processing callbacks.</p></div>
        </div>
      </section>

      <section className="frust-section" aria-labelledby="frust-embedded-heading">
        <div className="frust-section-heading">
          <p className="frust-eyebrow">02 / Programming in context</p>
          <h2 id="frust-embedded-heading">Extend the application you are already using.</h2>
          <p>FRust gives Djehuti applications a common language foundation while each application
            supplies the operations that make sense for its workspace.</p>
        </div>
        <div className="frust-screenshots">
          {screenshots.map(shot => (
            <figure className="frust-screenshot" key={shot.src}>
              <button type="button" onClick={() => setEnlarged(shot)} aria-label={`Enlarge screenshot: ${shot.title}`} aria-haspopup="dialog">
                <img src={shot.src} alt={shot.alt} loading="lazy" width="1456" height="819" />
                <span className="frust-enlarge" aria-hidden="true">View full size ↗</span>
              </button>
              <figcaption><span className="frust-eyebrow">{shot.app}</span><h3>{shot.title}</h3><p>{shot.caption}</p></figcaption>
            </figure>
          ))}
        </div>
        <p className="frust-caption">Actual desktop application captures. Select a screenshot to inspect the workspace.</p>
      </section>

      <section className="frust-section" aria-labelledby="frust-media-heading">
        <div className="frust-section-heading">
          <p className="frust-eyebrow">03 / Signals, images, and behavior</p>
          <h2 id="frust-media-heading">Small functions. Useful creative tools.</h2>
          <p>A processing function becomes useful when the host connects it to samples, parameters,
            scene objects, or simulation state. FRust is the language at that boundary.</p>
        </div>
        <div className="frust-use-cases">
          <div className="frust-use-case"><span className="frust-use-case-number">AUDIO / DSP</span><h3>Shape a signal</h3>
            <p>Build numerical processing from gain, filters, oscillators, and envelopes. Station’s Signal Lab combines graph authoring with its audio workflow.</p>
            <Link to="/djehuti-suite#station">Explore Station →</Link></div>
          <div className="frust-use-case"><span className="frust-use-case-number">VIDEO / IMAGE MATH</span><h3>Describe the transformation</h3>
            <p>Typed numerical functions can express channel scaling, mixing, and other image calculations. Video effects also need a host to supply frames, timing, and parameters.</p>
            <p className="frust-status-note">Movie’s FRust-specific video effect interfaces are in development; availability differs from Station’s audio integration.</p>
            <Link to="/djehuti-suite#movie">Explore Movie →</Link></div>
          <div className="frust-use-case"><span className="frust-use-case-number">PLUGINS / EXTENSIONS</span><h3>Add behavior without rebuilding the host</h3>
            <p>The plugin host loads compiled FRust functions through a C-compatible interface. Hosts can call plugins, and plugins can call registered host functions.</p>
            <p>Independent plugin handles support targeted reload and unload.</p></div>
          <div className="frust-use-case"><span className="frust-use-case-number">SCENES / ENGINEERING</span><h3>Work with the application model</h3>
            <p>Engine embeds per-entity scripts and visual logic. Electronics Lab connects programmable components to simulation pins, parameters, and instance state.</p>
            <Link to={PROGRAMMING_GUIDE}>Read the programming guide →</Link></div>
        </div>
      </section>

      <section className="frust-section frust-compiler-section" aria-labelledby="frust-compiler-heading">
        <div className="frust-section-heading">
          <p className="frust-eyebrow">04 / Under the surface</p>
          <h2 id="frust-compiler-heading">A direct path to optimized native code.</h2>
          <p>FRust lowers programs to LLVM intermediate representation, applies compiler optimizations,
            and produces machine code. The compiler supports just-in-time execution and ahead-of-time output.</p>
        </div>
        <figure className="frust-architecture">
          <div className="frust-architecture-inputs"><span>Handwritten FRust</span><span>Node graph → generated FRust</span></div>
          <span className="frust-flow-arrow" aria-hidden="true">↓</span>
          <ol className="frust-compiler-flow" aria-label="Compilation stages">
            <li><span>01</span><strong>Front end</strong><small>Parse, types &amp; diagnostics</small></li>
            <li><span>02</span><strong>LLVM IR</strong><small>Program representation</small></li>
            <li><span>03</span><strong>Optimize</strong><small>LLVM pass pipeline</small></li>
            <li><span>04</span><strong>Native code</strong><small>JIT or object output</small></li>
          </ol>
          <span className="frust-flow-arrow" aria-hidden="true">↓</span>
          <div className="frust-host-boundary"><strong>Djehuti application host</strong><span>Registered functions · parameters · application state · processing callbacks</span></div>
          <figcaption>One language pipeline; application-specific host interfaces. Simplified architecture illustration.</figcaption>
        </figure>
        <div className="frust-three-up">
          <div><h3>Concrete generic code</h3><p>Generic functions and types are specialized for their concrete types, keeping type information available to native code generation.</p></div>
          <div><h3>LLVM optimization</h3><p>The embeddable compiler uses LLVM’s O2 optimization pipeline before code generation. Performance depends on the program and its host workload.</p></div>
          <div><h3>A deliberate host boundary</h3><p>Each application exposes its own functions to FRust. Native extensions execute with the host’s capabilities; they are trusted code.</p></div>
        </div>
      </section>

      <section className="frust-section" aria-labelledby="frust-runtime-heading">
        <div className="frust-runtime-grid">
          <div className="frust-section-heading">
            <p className="frust-eyebrow">05 / A platform, not just syntax</p>
            <h2 id="frust-runtime-heading">Author. Compile. Load. Call.</h2>
            <p>The runtime gives the host a practical extension lifecycle: load a program,
              look up an exported function, execute it, and reload or unload that plugin when needed.</p>
            <p>Applications retain control of their documents, timing, and processing contracts.
              For real-time work, code must respect the host’s limits on allocation, blocking, and execution time.</p>
          </div>
          <div className="frust-runtime-details">
            <details><summary>What belongs to the language?</summary><p>Types, expressions, functions, control flow, generics, and other language constructs. An audio buffer or scene object belongs to the host interface that exposes it.</p></details>
            <details><summary>Does a node graph use a separate interpreter?</summary><p>The graph compiler produces FRust source. That source passes through the FRust compiler, so visual authoring connects to the same native execution foundation.</p></details>
            <details><summary>Is every capability available in every app?</summary><p>Integration varies by application. Station’s audio graphs, Engine’s scene scripting, and Electronics Lab’s programmable components use different host contracts. Movie’s FRust video integration is still developing.</p></details>
          </div>
        </div>
      </section>

      <footer className="frust-next">
        <div><p className="frust-eyebrow">Go further</p><h2>Explore the language. See it in context.</h2><p>Start with the desktop tools, or follow the technical guides into the language and its host integrations.</p></div>
        <div className="frust-resource-links">
          <Link to="/djehuti-suite">Djehuti Desktop Tools <span aria-hidden="true">↗</span></Link>
          <Link to={LANGUAGE_REFERENCE}>FRust language reference <span aria-hidden="true">→</span></Link>
          <Link to={PROGRAMMING_GUIDE}>Embedded programming guide <span aria-hidden="true">→</span></Link>
          <a href="https://github.com/wwestlake/FrustLang">Compiler &amp; runtime source <span aria-hidden="true">↗</span></a>
        </div>
      </footer>

      <dialog className="frust-lightbox" ref={dialog} aria-labelledby="frust-lightbox-title" onClose={() => setEnlarged(null)} onClick={event => { if (event.target === event.currentTarget) dialog.current?.close() }}>
        {enlarged && <><button type="button" className="frust-lightbox-close" autoFocus onClick={() => dialog.current?.close()} aria-label="Close screenshot">Close ✕</button>
          <figure><img src={enlarged.src} alt={enlarged.alt} /><figcaption id="frust-lightbox-title">{enlarged.title}</figcaption></figure></>}
      </dialog>
    </article>
  )
}
