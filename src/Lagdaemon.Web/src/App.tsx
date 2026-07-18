import { useState, useEffect, useRef } from 'react'
import type { ReactNode } from 'react'
import { Routes, Route, useNavigate, useLocation } from 'react-router-dom'
import './App.css'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import { UserPrefsProvider } from './contexts/UserPrefsContext'
import { ThemeProvider } from './contexts/ThemeContext'
import { UserMenu } from './components/auth/UserMenu'
import { ProtectedRoute } from './components/auth/ProtectedRoute'
import { ScrollToTop } from './components/ScrollToTop'
import NotificationDropdown from './components/NotificationDropdown'
import SettingsPanel from './components/settings/SettingsPanel'
import type { SettingsSection } from './components/settings/SettingsPanel'
import { LoginModal } from './components/auth/LoginModal'
import { SignupModal } from './components/auth/SignupModal'
import { ForgotPasswordModal } from './components/auth/ForgotPasswordModal'
import ForumPage from './pages/community/ForumPage'
import ForumForumPage from './pages/community/ForumForumPage'
import ForumThreadPage from './pages/community/ForumThreadPage'
import ForumSearchPage from './pages/community/ForumSearchPage'
import BlogPage from './pages/community/BlogPage'
import BlogArticlePage from './pages/community/BlogArticlePage'
import BlogEditorPage from './pages/community/BlogEditorPage'
import PapersListPage from './pages/community/PapersListPage'
import PaperWorkspacePage from './pages/community/PaperWorkspacePage'
import PublicPapersPage, { PublicPaperReadPage } from './pages/community/PublicPapersPage'
import ProfilePage from './pages/community/ProfilePage'
import PublicProfilePage from './pages/community/PublicProfilePage'
import AdminPage from './pages/community/AdminPage'
import AnnouncementsPage from './pages/community/AnnouncementsPage'
import AnnouncementBanner from './pages/community/AnnouncementBanner'
import AchievementsPage from './pages/profile/AchievementsPage'
import SupportersPage from './pages/community/SupportersPage'
import DownloadsPage from './pages/community/DownloadsPage'
import DownloadProductPage from './pages/community/DownloadProductPage'
import SponsorsPage from './pages/community/SponsorsPage'
import DesktopAuthPage from './pages/auth/DesktopAuthPage'

import { blogApi } from './api/blogApi'
import type { BlogArticle } from './api/blogApi'

// ── Nav ───────────────────────────────────────────────────────────────────────

type NavProps = {
  onOpenLogin: () => void
  onOpenSettings: (section?: SettingsSection) => void
  onOpenAchievements: () => void
}

// Click-to-open dropdown on desktop, plain expandable section in the mobile
// drawer (same markup works for both -- the drawer's own vertical flow
// makes an absolutely-positioned panel unnecessary there; only the CSS
// under .nav-desktop switches it to a floating panel).
function NavGroup({ label, children, onNavigate }: { label: string; children: ReactNode; onNavigate: () => void }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onDocClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDocClick)
    return () => document.removeEventListener('mousedown', onDocClick)
  }, [open])

  return (
    <div className={`nav-group${open ? ' open' : ''}`} ref={ref}>
      <button
        type="button"
        className="nav-community-link nav-group-toggle"
        aria-expanded={open}
        onClick={() => setOpen(o => !o)}
      >
        {label} <span className="nav-group-caret">▾</span>
      </button>
      <div className="nav-group-panel" onClick={() => { setOpen(false); onNavigate() }}>
        {children}
      </div>
    </div>
  )
}

function Nav({ onOpenLogin, onOpenSettings, onOpenAchievements }: NavProps) {
  const { user } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [drawerOpen, setDrawerOpen] = useState(false)

  const go = (path: string) => { setDrawerOpen(false); navigate(path) }
  const isHome = location.pathname === '/'
  const active = (path: string) => location.pathname.startsWith(path)

  const links = (
    <>
      {isHome ? (
        <>
          <a href="#research" onClick={() => setDrawerOpen(false)}>Research</a>
          <a href="#instrument" onClick={() => setDrawerOpen(false)}>Instrument</a>
          <a href="#about" onClick={() => setDrawerOpen(false)}>About</a>
        </>
      ) : (
        <button className="nav-section-back breadcrumb-link" onClick={() => go('/')}>← Home</button>
      )}
      <button className={`nav-community-link${active('/announcements') ? ' active' : ''}`} onClick={() => go('/announcements')}>Announcements</button>
      <NavGroup label="Tools" onNavigate={() => setDrawerOpen(false)}>
        <a className="nav-community-link" href="/learn/">Learn</a>
        <button className={`nav-community-link${active('/papers') ? ' active' : ''}`} onClick={() => go('/papers')}>Papers</button>
        <button className={`nav-community-link${active('/downloads') ? ' active' : ''}`} onClick={() => go('/downloads')}>Downloads</button>
        {user?.roles?.includes('system:engineer') && (
          <a className="nav-community-link" href="/math/">DjeLab</a>
        )}
      </NavGroup>
      <NavGroup label="Social" onNavigate={() => setDrawerOpen(false)}>
        <button className={`nav-community-link${active('/forum') ? ' active' : ''}`} onClick={() => go('/forum')}>Forum</button>
        <button className={`nav-community-link${active('/blog') ? ' active' : ''}`} onClick={() => go('/blog')}>Blog</button>
        <a className="nav-community-link" href="/mud/">MUD</a>
      </NavGroup>
      <button className={`nav-community-link${active('/sponsors') ? ' active' : ''}`} onClick={() => go('/sponsors')}>Sponsors</button>
      {user && (
        <button className={`nav-community-link${active('/profile') ? ' active' : ''}`} onClick={() => go('/profile')}>Profile</button>
      )}
      {user?.role === 'admin' && (
        <button className={`nav-community-link${active('/admin') ? ' active' : ''}`} onClick={() => go('/admin')}>Admin</button>
      )}
      <a className="nav-cta" href="/djehuti/" onClick={() => setDrawerOpen(false)}>Open Djehuti ↗</a>
    </>
  )

  return (
    <>
      <header className="nav">
        <a className="nav-logo" href="/" onClick={e => { e.preventDefault(); go('/') }}>
          <img src="/logo.png" alt="Lag Daemon" />
        </a>
        <nav className="nav-desktop">
          {links}
          {user && <NotificationDropdown />}
          <UserMenu onOpenLogin={onOpenLogin} onOpenSettings={onOpenSettings} onOpenAchievements={onOpenAchievements} />
        </nav>
        <div className="nav-mobile-bar">
          {user && <NotificationDropdown />}
          <UserMenu onOpenLogin={onOpenLogin} onOpenSettings={onOpenSettings} onOpenAchievements={onOpenAchievements} />
          <button className="nav-hamburger" onClick={() => setDrawerOpen(o => !o)} aria-label="Menu">
            <span /><span /><span />
          </button>
        </div>
      </header>

      {drawerOpen && <div className="nav-drawer-overlay" onClick={() => setDrawerOpen(false)} />}
      <nav className={`nav-drawer${drawerOpen ? ' open' : ''}`}>
        <button className="nav-drawer-close" onClick={() => setDrawerOpen(false)} aria-label="Close">✕</button>
        {links}
      </nav>
    </>
  )
}

// ── Marketing sections (home) ─────────────────────────────────────────────────

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
        <img src="/cyberscope-analyze-hero.png" alt="Djehuti Cyberscope 3D phase space analysis" />
      </div>
    </section>
  )
}

function FeaturedPost() {
  const [article, setArticle] = useState<BlogArticle | null>(null)
  const navigate = useNavigate()

  useEffect(() => {
    blogApi.getArticles({ page: 1, pageSize: 1 }).then(items => setArticle(items[0] ?? null))
  }, [])

  if (!article) return null

  return (
    <section className="featured-post">
      <div className="featured-post-inner">
        <div className="featured-post-label">From the Blog</div>
        <div className="featured-post-card" onClick={() => navigate(`/blog/${article.slug}`)} role="button" tabIndex={0} onKeyDown={e => e.key === 'Enter' && navigate(`/blog/${article.slug}`)}>
          {article.coverUrl && <img className="featured-post-cover" src={article.coverUrl} alt="" />}
          <div className="featured-post-body">
            <h2 className="featured-post-title">{article.title}</h2>
            {article.subtitle && <p className="featured-post-subtitle">{article.subtitle}</p>}
            {article.excerpt && <p className="featured-post-excerpt">{article.excerpt}</p>}
            <span className="featured-post-cta">Read article →</span>
          </div>
        </div>
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
          <img src="/djelab-workspace-announcement.png" alt="DjeLab workspace" />
          <div className="screenshot-caption">
            <h3>DjeLab Workspace</h3>
            <p>
              A browser-based lab for opening data files, previewing their structure, streaming them
              into code, plotting the result, and keeping the graph, editor, and log together in one
              place.
            </p>
          </div>
        </div>
      </div>
    </section>
  )
}

function PapersSection() {
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
                Protocol DOI: 10.5281/zenodo.20739448 ↗
              </a>
            </div>
            <div className="paper-doi">
              <a href="https://doi.org/10.5281/zenodo.20816558" target="_blank" rel="noopener">
                Software DOI (1.0.4): 10.5281/zenodo.20816558 ↗
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
  open: boolean; onClose: () => void; title: string; effective: string
  items: { strong: string; body: string }[]
}

function SiteStats() {
  const [stats, setStats] = useState<{ members: number; posts: number; threads: number; badges: number } | null>(null)

  useEffect(() => {
    fetch('/djehuti/api/stats').then(r => r.ok ? r.json() : null).then(d => {
      if (d) setStats({ members: d.members, posts: d.posts, threads: d.threads, badges: d.badges })
    }).catch(() => {})
  }, [])

  if (!stats) return null

  const fmt = (n: number) => n >= 1000 ? `${(n / 1000).toFixed(1)}k` : String(n)

  return (
    <section className="site-stats">
      <div className="site-stats-inner">
        {([
          ['Members',  stats.members,  '👤'],
          ['Posts',    stats.posts,    '✍️'],
          ['Threads',  stats.threads,  '💬'],
          ['Badges',   stats.badges,   '🏅'],
        ] as [string, number, string][]).map(([label, value, icon]) => (
          <div key={label} className="site-stat">
            <span className="site-stat-icon">{icon}</span>
            <span className="site-stat-value">{fmt(value)}</span>
            <span className="site-stat-label">{label}</span>
          </div>
        ))}
      </div>
    </section>
  )
}

function PatreonBadge() {
  return (
    <section className="patreon-badge-section">
      <div className="patreon-badge-inner">
        <div className="patreon-badge-text">
          <strong>Support this research</strong>
          <span>Djehuti is independent, open research. If it's useful to you, consider backing it on Patreon.</span>
        </div>
        <div className="patreon-badge-actions">
          <a
            className="patreon-badge-btn"
            href="https://www.patreon.com/lagdaemon"
            target="_blank"
            rel="noopener noreferrer"
          >
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
              <path d="M14.82 2.41C11.57 2.41 8.93 5.05 8.93 8.3c0 3.24 2.64 5.88 5.89 5.88 3.24 0 5.88-2.64 5.88-5.88 0-3.25-2.64-5.89-5.88-5.89zM3.1 21.59h3.16V2.41H3.1v19.18z"/>
            </svg>
            Become a Patron
          </a>
          <a className="patreon-badge-wall-link" href="/supporters">View Supporters →</a>
        </div>
      </div>
    </section>
  )
}

function SpreadTheWord() {
  const [copied, setCopied] = useState(false)
  const url = 'https://lagdaemon.com'
  const text = encodeURIComponent('Lagdaemon — AI research, creative experimentation, and the Djehuti analysis instrument. Worth a look.')
  const copy = () => {
    navigator.clipboard.writeText(url)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }
  return (
    <section className="spread-section">
      <div className="spread-inner">
        <div className="spread-text">
          <strong>Know someone who should be here?</strong>
          <span>Share Lagdaemon with researchers, builders, and creative practitioners working at the edge of AI.</span>
        </div>
        <div className="spread-actions">
          <button className="spread-btn" onClick={copy}>
            {copied ? '✓ Copied' : '🔗 Copy link'}
          </button>
          <a className="spread-btn" href={`https://twitter.com/intent/tweet?url=${encodeURIComponent(url)}&text=${text}`} target="_blank" rel="noopener noreferrer">
            Share on 𝕏
          </a>
          <a className="spread-btn" href={`https://www.linkedin.com/sharing/share-offsite/?url=${encodeURIComponent(url)}`} target="_blank" rel="noopener noreferrer">
            Share on LinkedIn
          </a>
        </div>
      </div>
    </section>
  )
}

function Modal({ open, onClose, title, effective, items }: ModalProps) {
  if (!open) return null
  return (
    <div className="modal-overlay open" onClick={(e) => { if (e.target === e.currentTarget) onClose() }}>
      <div className="modal" role="dialog" aria-modal="true" aria-label={title}>
        <button className="modal-close" onClick={onClose} aria-label="Close">&times;</button>
        <h2>{title}</h2>
        <div className="modal-date">{effective}</div>
        <ul>
          {items.map((item, i) => (
            <li key={i}><strong>{item.strong}</strong>{item.body ? ' — ' : ''}{item.body}</li>
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
        &nbsp;&middot;&nbsp;
        <a href="/sponsors">Sponsors</a>
        &nbsp;&middot;&nbsp;
        <a href="/supporters">Supporters</a>
      </p>
    </footer>
  )
}

// ── Root app ──────────────────────────────────────────────────────────────────

function AppInner() {
  const location = useLocation()
  const navigate = useNavigate()
  const isHome = location.pathname === '/'
  const isDesktopAuth = location.pathname === '/auth/desktop'

  // Anonymous page-view beacon — fires on every route change for non-logged-in visitors
  const { user } = useAuth()
  useEffect(() => {
    if (user) return  // only track anonymous
    const params = new URLSearchParams({ path: location.pathname, ref: document.referrer })
    fetch(`/djehuti/api/track/pageview?${params}`, { method: 'POST' }).catch(() => {})
  }, [location.pathname, user])

  const [privacyOpen, setPrivacyOpen] = useState(false)
  const [aupOpen, setAupOpen] = useState(false)
  const [showLogin, setShowLogin] = useState(false)
  const [showSignup, setShowSignup] = useState(false)
  const [showForgotPassword, setShowForgotPassword] = useState(false)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [settingsSection, setSettingsSection] = useState<SettingsSection>('general')

  const openSettings = (sec: SettingsSection = 'general') => {
    setSettingsSection(sec)
    setSettingsOpen(true)
  }

  if (isDesktopAuth) {
    return (
      <>
        <ScrollToTop />
        <DesktopAuthPage />
      </>
    )
  }

  return (
    <>
      <ScrollToTop />
      <Nav onOpenLogin={() => setShowLogin(true)} onOpenSettings={openSettings} onOpenAchievements={() => navigate('/achievements')} />

      {isHome ? (
        <>
          <Hero />
          <SiteStats />
          <PatreonBadge />
          <FeaturedPost />
          <Pitch />
          <Screenshots />
          <PapersSection />
          <About />
          <PatreonBadge />
          <SpreadTheWord />
          <Footer onPrivacy={() => setPrivacyOpen(true)} onAup={() => setAupOpen(true)} />
        </>
      ) : (
        <main className="community-main">
          {location.pathname !== '/announcements' && <AnnouncementBanner />}
          <Routes>
            <Route path="/announcements" element={<AnnouncementsPage />} />
            <Route path="/forum" element={<ForumPage />} />
            <Route path="/forum/search" element={<ForumSearchPage />} />
            <Route path="/forum/:forumId" element={<ForumForumPage />} />
            <Route path="/forum/thread/:threadId" element={<ForumThreadPage />} />
            <Route path="/blog" element={<BlogPage />} />
            <Route path="/blog/editor" element={<ProtectedRoute><BlogEditorPage /></ProtectedRoute>} />
            <Route path="/blog/editor/:articleId" element={<ProtectedRoute><BlogEditorPage /></ProtectedRoute>} />
            <Route path="/blog/:slug" element={<BlogArticlePage />} />
            <Route path="/papers" element={<PublicPapersPage />} />
            <Route path="/papers/read/:paperId" element={<PublicPaperReadPage />} />
            <Route path="/papers/workspace" element={<ProtectedRoute><PapersListPage /></ProtectedRoute>} />
            <Route path="/papers/:paperId" element={<ProtectedRoute><PaperWorkspacePage /></ProtectedRoute>} />
            <Route path="/profile" element={<ProtectedRoute><ProfilePage /></ProtectedRoute>} />
            <Route path="/profile/:userId" element={<PublicProfilePage />} />
            <Route path="/achievements" element={<ProtectedRoute><AchievementsPage /></ProtectedRoute>} />
            <Route path="/supporters" element={<SupportersPage />} />
            <Route path="/downloads" element={<DownloadsPage />} />
            <Route path="/downloads/:slug" element={<DownloadProductPage />} />
            <Route path="/sponsors" element={<SponsorsPage />} />
            <Route path="/admin" element={<ProtectedRoute requiredRole="admin"><AdminPage /></ProtectedRoute>} />
          </Routes>
        </main>
      )}

      <Modal open={privacyOpen} onClose={() => setPrivacyOpen(false)}
        title="Privacy Policy — Djehuti Cyberscope AI+" effective="Effective Date: June 23, 2026" items={PRIVACY_ITEMS} />
      <Modal open={aupOpen} onClose={() => setAupOpen(false)}
        title="Acceptable Use Policy — Djehuti Cyberscope AI+" effective="Effective Date: June 23, 2026" items={AUP_ITEMS} />

      <SettingsPanel open={settingsOpen} initialSection={settingsSection} onClose={() => setSettingsOpen(false)} />

      <LoginModal open={showLogin} onClose={() => setShowLogin(false)}
        onSwitchToSignup={() => { setShowLogin(false); setShowSignup(true) }}
        onSwitchToForgotPassword={() => { setShowLogin(false); setShowForgotPassword(true) }} />
      <SignupModal open={showSignup} onClose={() => setShowSignup(false)}
        onSwitchToLogin={() => { setShowSignup(false); setShowLogin(true) }} />
      <ForgotPasswordModal open={showForgotPassword} onClose={() => setShowForgotPassword(false)}
        onSwitchToLogin={() => { setShowForgotPassword(false); setShowLogin(true) }} />
    </>
  )
}

function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <UserPrefsProvider>
          <AppInner />
        </UserPrefsProvider>
      </AuthProvider>
    </ThemeProvider>
  )
}

export default App
