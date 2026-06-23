import { useState } from 'react'
import './App.css'

function Nav() {
  return (
    <header className="nav">
      <a className="nav-logo" href="#top">
        <img src="/logo.png" alt="Lag Daemon" />
      </a>
      <nav>
        <a href="#research">Research</a>
        <a href="#instrument">Instrument</a>
        <a href="#papers">Papers</a>
        <a href="#about">About</a>
        <a className="nav-cta" href="/djehuti/">Open Djehuti ↗</a>
      </nav>
    </header>
  )
}

function Hero() {
  return (
    <section className="hero" id="top">
      <div className="hero-copy">
        <div className="hero-eyebrow">AI Research · Information Space Dynamics</div>
        <div className="hero-logo">
          <img src="/logo.png" alt="Djehuti Cyberscope AI+" />
        </div>
        <h1>What does an LLM conversation look like from the outside?</h1>
        <a className="hero-cta" href="/djehuti/">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <polygon points="5 3 19 12 5 21 5 3" />
          </svg>
          Open Djehuti Cyberscope
        </a>
      </div>
      <div className="hero-visual">
        <img src="/dashboard-deformation-phase-space.png" alt="Djehuti 3D phase-space view" />
      </div>
    </section>
  )
}

function Pitch() {
  return (
    <section className="pitch" id="research">
      <div className="pitch-inner">
        <div className="section-label">The Research</div>
        <p>
          <strong>Every large language model conversation traces a path through information space.</strong>{' '}
          Velocity accumulates, curvature bends the trajectory, and somewhere ahead — invisible to the
          model itself — attractors exert pull. The question is whether those dynamics can be measured
          from the outside, without touching model weights, attention patterns, or provider internals.
          That constraint turns out to be surprisingly productive.
        </p>
        <p>
          <strong>Information Space Dynamics</strong> is a framework for making that measurement rigorous.
          It treats the prompt-response sequence as a formal trajectory: each turn produces an observable
          vector of lexical, structural, and semantic quantities, and the sequence of those vectors
          yields velocity, curvature, torsional accumulation, and stability margin — all computed
          from text alone. The framework draws a hard line between <em>direct observations</em>,
          {' '}<em>calibrated estimates</em>, and <em>hypothesis-dependent quantities</em>, so the instrument
          never silently fabricates a value it cannot actually see.
        </p>
        <p>
          <strong>Djehuti Cyberscope AI+</strong> is the empirical workbench that puts ISD into practice.
          Load a conversation transcript, run the analysis pipeline, and the dashboard renders the full
          trajectory: a 3D deformation phase-space, per-turn metric timelines, a feature finder that
          flags high-velocity transitions and structural shifts, and attractor-approach diagnostics
          that fire when torsional accumulation and stability margin cross calibrated thresholds.
          An embedded AI analyst — grounded entirely in ISD theory — narrates what the data actually
          shows, citing turn indices and metric values, not vague impressions.
        </p>
        <p>
          <strong>The Live Lab</strong> extends the instrument to real-time experiments. Enter any
          provider API key directly in the browser — it never leaves client state — start a vanilla
          conversation, and watch Djehuti collect each completed turn for analysis as the exchange
          unfolds. The Multi-LLM Moderated Conversation Engine layer supports structured
          multi-participant sessions with moderator intervention thresholds and pairwise interferometry
          across model trajectories.
        </p>
        <p>
          This is early-stage research infrastructure, not a product. The codebase is open, the
          measurement protocol is documented, and the framework is designed to be extended.
          If you are studying LLM behavior and want instrumentation that respects the difference
          between what can be observed and what must remain an estimate, Djehuti is built for that work.
        </p>
      </div>
    </section>
  )
}

function Screenshots() {
  return (
    <section className="screenshots" id="instrument">
      <div className="section-label">The Instrument</div>
      <div className="screenshot-grid">
        <div className="screenshot-card wide">
          <img src="/dashboard-deformation-phase-space.png" alt="3D deformation phase-space" />
          <div className="screenshot-caption">
            <h3>Deformation Phase-Space</h3>
            <p>
              Three-dimensional trajectory plot of velocity, curvature, and torsional accumulation
              across the full conversation. Attractor-approach events appear as annotated markers at
              the boundary perimeter.
            </p>
          </div>
        </div>
        <div className="screenshot-card">
          <img src="/dashboard-metric-timelines.png" alt="Metric timelines" />
          <div className="screenshot-caption">
            <h3>Metric Timelines</h3>
            <p>
              Per-turn plots of prompt-response alignment, semantic velocity, lexical similarity,
              word-count delta, and response length over integer logical time.
            </p>
          </div>
        </div>
        <div className="screenshot-card">
          <img src="/dashboard-feature-finder.png" alt="Feature finder" />
          <div className="screenshot-caption">
            <h3>Feature Finder</h3>
            <p>
              Indexed markers for high-velocity transitions, low alignment, structural changes,
              repeated prompts, and attractor-approach events — each with torsional-resistance basis
              and severity.
            </p>
          </div>
        </div>
      </div>
    </section>
  )
}

function Papers() {
  return (
    <section className="papers" id="papers">
      <div className="papers-inner">
        <div className="section-label">Publications</div>
        <div className="paper-cards">
          <div className="paper-card">
            <div className="paper-tag">Framework · 2026</div>
            <h3>Information Space Dynamics</h3>
            <p>
              The theoretical foundation — velocity, curvature, torsional resistance, and
              attractor-approach diagnostics for LLM trajectory analysis under strict
              pure-observability constraints.
            </p>
            <div className="paper-doi">
              <a href="https://doi.org/10.5281/zenodo.20690590" target="_blank" rel="noopener">
                DOI: 10.5281/zenodo.20690590 ↗
              </a>
            </div>
          </div>
          <div className="paper-card">
            <div className="paper-tag">Instrument · 2026</div>
            <h3>Djehuti Cyberscope AI+</h3>
            <p>
              Protocol specification for the empirical measurement workbench — ingestion model,
              observable vector construction, measurement primitives, and analyst boundary.
            </p>
            <div className="paper-doi">
              <a href="https://doi.org/10.5281/zenodo.20739448" target="_blank" rel="noopener">
                DOI: 10.5281/zenodo.20739448 ↗
              </a>
            </div>
          </div>
          <div className="paper-card">
            <div className="paper-tag">Coming soon</div>
            <h3>Articles &amp; Notes</h3>
            <p>
              Shorter-form writing on measurement findings, anomalies, and theoretical extensions —
              forthcoming.
            </p>
          </div>
        </div>
      </div>
    </section>
  )
}

function About() {
  return (
    <section className="about" id="about">
      <div className="section-label">About</div>
      <div className="about-grid">
        <div>
          <p>
            W. Westlake is an independent AI researcher focused on empirical observability of large
            language model behavior. The work centers on building rigorous measurement instruments
            that respect the boundary between what can be directly observed and what must remain a
            calibrated estimate.
          </p>
          <p>
            The ISD framework treats conversation as a trajectory through information space —
            studying velocity, curvature, torsional accumulation, and attractor-approach signatures
            without requiring access to model internals.
          </p>
        </div>
        <div>
          <p>
            Current work: expanding Djehuti's measurement capabilities, formalizing the zeta-4
            observables, and developing multi-LLM interferometry protocols for comparative
            trajectory analysis across models.
          </p>
          <div style={{ marginTop: '1.2rem' }}>
            <div className="contact-item">
              ✉ <a href="mailto:wwestlake@lagdaemon.com">wwestlake@lagdaemon.com</a>
            </div>
            <div className="contact-item">
              ⌥ <a href="https://github.com/wwestlake" target="_blank" rel="noopener">github.com/wwestlake</a>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}

type ModalProps = {
  open: boolean
  onClose: () => void
  title: string
  effective: string
  items: { strong: string; body: string }[]
}

function Modal({ open, onClose, title, effective, items }: ModalProps) {
  if (!open) return null
  return (
    <div
      className="modal-overlay open"
      onClick={(e) => { if (e.target === e.currentTarget) onClose() }}
    >
      <div className="modal" role="dialog" aria-modal="true" aria-label={title}>
        <button className="modal-close" onClick={onClose} aria-label="Close">&times;</button>
        <h2>{title}</h2>
        <div className="modal-date">{effective}</div>
        <ul>
          {items.map((item, i) => (
            <li key={i}>
              <strong>{item.strong}</strong>
              {item.body ? ' — ' : ''}
              {item.body}
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

const PRIVACY_ITEMS = [
  { strong: 'We collect no personal information', body: 'no names, emails, accounts, or registration.' },
  { strong: 'Conversation data is stored anonymously', body: 'runs stored for research, not attributed to any individual. By using Djehuti you acknowledge submitted run data may be retained and used for research.' },
  { strong: 'Your API keys stay on your device', body: 'stored in browser local storage only, never transmitted to our servers.' },
  { strong: 'All content on this site is public', body: 'do not submit private or confidential content.' },
  { strong: 'No tracking or analytics', body: 'no cookies, ad trackers, or third-party analytics.' },
]

const AUP_ITEMS = [
  { strong: 'Research and educational use only', body: 'this instrument is provided for legitimate AI research, study, and experimentation. Commercial resale or redistribution of results without attribution is not permitted.' },
  { strong: 'Do not submit harmful content', body: 'do not input content that is illegal, abusive, threatening, or designed to produce harmful outputs. You are responsible for the prompts you submit.' },
  { strong: 'API keys are your responsibility', body: 'you supply your own provider API keys. You are solely responsible for any charges, usage, or policy violations incurred with your key.' },
  { strong: 'No automated abuse', body: 'do not use scripts, bots, or automated tooling to hammer the service in ways that degrade availability for others.' },
  { strong: 'Submitted data may be used for research', body: 'conversation runs stored by the system are anonymous and may be retained for ISD research purposes. Do not submit confidential, proprietary, or personally identifying content.' },
  { strong: 'Violations may result in access termination', body: 'we reserve the right to block access for any use that violates these terms or applicable law.' },
]

function Footer({ onPrivacy, onAup }: { onPrivacy: () => void; onAup: () => void }) {
  return (
    <footer>
      <img src="/logo.png" alt="Lag Daemon" />
      <p>&copy; 2026 W. Westlake &mdash; <a href="mailto:wwestlake@lagdaemon.com">wwestlake@lagdaemon.com</a></p>
      <p>
        <a href="#" onClick={(e) => { e.preventDefault(); onPrivacy() }}>Privacy Policy</a>
        &nbsp;&middot;&nbsp;
        <a href="#" onClick={(e) => { e.preventDefault(); onAup() }}>Acceptable Use</a>
      </p>
    </footer>
  )
}

function App() {
  const [privacyOpen, setPrivacyOpen] = useState(false)
  const [aupOpen, setAupOpen] = useState(false)

  return (
    <>
      <Nav />
      <Hero />
      <Pitch />
      <Screenshots />
      <Papers />
      <About />
      <Footer onPrivacy={() => setPrivacyOpen(true)} onAup={() => setAupOpen(true)} />
      <Modal
        open={privacyOpen}
        onClose={() => setPrivacyOpen(false)}
        title="Privacy Policy — Djehuti Cyberscope AI+"
        effective="Effective Date: June 23, 2026"
        items={PRIVACY_ITEMS}
      />
      <Modal
        open={aupOpen}
        onClose={() => setAupOpen(false)}
        title="Acceptable Use Policy — Djehuti Cyberscope AI+"
        effective="Effective Date: June 23, 2026"
        items={AUP_ITEMS}
      />
    </>
  )
}

export default App
