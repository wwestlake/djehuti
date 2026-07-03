import { useEffect, useMemo, useState } from 'react'
import './App.css'
import landingBg from './assets/landing-bg.jpg'
import MudPage from './pages/MudPage'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import { ThemeProvider, THEMES, useTheme } from './contexts/ThemeContext'

const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_OAUTH_CLIENT_ID || ''
const GITHUB_CLIENT_ID = import.meta.env.VITE_GITHUB_OAUTH_CLIENT_ID || ''
const HCAPTCHA_SITE_KEY = import.meta.env.VITE_HCAPTCHA_SITE_KEY || ''

type AuthMode = 'signin' | 'signup' | 'forgot'

function ThemePicker() {
  const { theme, setTheme } = useTheme()

  return (
    <div className="mud-auth-theme-row">
      <span>Style</span>
      <div className="mud-auth-theme-pills">
        {THEMES.map(option => (
          <button
            key={option.id}
            type="button"
            className={`mud-auth-theme-pill${theme === option.id ? ' active' : ''}`}
            onClick={() => setTheme(option.id)}
          >
            {option.label}
          </button>
        ))}
      </div>
    </div>
  )
}

function OAuthButtons() {
  const redirect = encodeURIComponent('/mud/')

  return (
    <div className="oauth-buttons">
      <a
        href={`https://accounts.google.com/o/oauth2/v2/auth?client_id=${GOOGLE_CLIENT_ID}&redirect_uri=https://lagdaemon.com/djehuti/api/auth/oauth/google/callback&response_type=code&scope=openid%20email%20profile&state=${redirect}`}
        className="oauth-button google"
        title="Sign in with Google"
      >
        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
          <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" />
          <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" />
          <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" />
          <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" />
        </svg>
        <span>Google</span>
      </a>
      <a
        href={`https://github.com/login/oauth/authorize?client_id=${GITHUB_CLIENT_ID}&redirect_uri=https://lagdaemon.com/djehuti/api/auth/oauth/github/callback&scope=user:email&state=${redirect}`}
        className="oauth-button github"
        title="Sign in with GitHub"
      >
        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
          <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z" />
        </svg>
        <span>GitHub</span>
      </a>
    </div>
  )
}

function MudLanding({ onEnter }: { onEnter: () => void }) {
  const { user } = useAuth()

  return (
    <section
      className="mud-page mud-page-app mud-landing-page"
      style={{
        backgroundImage: `linear-gradient(180deg, rgba(4, 7, 16, 0.68) 0%, rgba(4, 7, 16, 0.88) 55%, rgba(4, 7, 16, 0.97) 100%), url(${landingBg})`,
      }}
    >
      <div className="mud-shell mud-landing-shell">
        <div className="mud-landing-hero">
          <div className="mud-kicker">LagDaemon MUD</div>
          <h1>Two worlds. One door.</h1>
          <p className="mud-landing-tagline">
            A text-first multiplayer world you can play from any browser, on any phone.
            Walk a medieval keep and its haunted forest, or dock at a drifting star station
            and board the derelict clamped to its ring. Gather, craft, read everything, and
            leave your mark.
          </p>
          <div className="mud-landing-actions">
            <button className="mud-command-btn" onClick={onEnter}>
              {user ? 'Enter the world' : 'Create account / Sign in'}
            </button>
            <a className="mud-back-btn" href="/">Back to LagDaemon.com</a>
          </div>
          {user
            ? <p className="mud-landing-note">Signed in as {user.displayName || 'Anonymous'}. Your characters are waiting.</p>
            : <p className="mud-landing-note">Playing requires a free account. Looking around here does not.</p>}
        </div>

        <div className="mud-landing-grid">
          <div className="mud-card mud-card-flat">
            <h2>Medieval Realm</h2>
            <p className="mud-landing-copy">
              Enter at the keep gate. Vaults below, a greenwood beyond the walls, a beacon
              above the clouds, and a barrow door that asks you to knock on your way out.
            </p>
          </div>
          <div className="mud-card mud-card-flat">
            <h2>Sci-Fi Realm</h2>
            <p className="mud-landing-copy">
              Dock at Star Reach. Ride the freight lift to the drift ring, board the Vagrant
              Star, dive into the Signal Sea, or walk the hull out to the Scar.
            </p>
          </div>
          <div className="mud-card mud-card-flat">
            <h2>Gather and Craft</h2>
            <p className="mud-landing-copy">
              75 rooms across 13 zones, stocked with materials and 38 recipes — torches,
              wards, medkits, a lens that shows how the damage happened.
            </p>
          </div>
          <div className="mud-card mud-card-flat">
            <h2>AI Companions</h2>
            <p className="mud-landing-copy">
              Eligible characters can enable a personal AI companion that inhabits the world
              alongside you. Bring your own key or use a supported tier.
            </p>
          </div>
        </div>

        <div className="mud-card mud-card-flat mud-landing-how">
          <h2>How to start</h2>
          <ol>
            <li>Create a free account, or sign in with Google or GitHub.</li>
            <li>Create a character — free players get one starter character in each realm.</li>
            <li>Step through a portal and type <code>look</code>. The world takes it from there.</li>
          </ol>
        </div>
      </div>
    </section>
  )
}

function MudAuthScreen({ onBack }: { onBack?: () => void }) {
  const { login, signup, error, clearError, isLoading } = useAuth()
  const [mode, setMode] = useState<AuthMode>('signin')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [feedback, setFeedback] = useState<string | null>(null)
  const [working, setWorking] = useState(false)
  const signupUsesOAuthOnly = Boolean(HCAPTCHA_SITE_KEY)

  const title = useMemo(() => {
    switch (mode) {
      case 'signup': return 'Enter the MUD'
      case 'forgot': return 'Recover access'
      default: return 'Enter LagDaemon MUD'
    }
  }, [mode])

  const subtitle = useMemo(() => {
    switch (mode) {
      case 'signup': return 'Create your shared Djehuti account here. You do not need to leave the game app.'
      case 'forgot': return 'We will send a reset link to your email.'
      default: return 'Sign in here, then step straight into the game.'
    }
  }, [mode])

  const switchMode = (next: AuthMode) => {
    clearError()
    setFeedback(null)
    setMode(next)
  }

  const submitSignIn = async (e: React.FormEvent) => {
    e.preventDefault()
    clearError()
    setFeedback(null)
    setWorking(true)
    try {
      await login(email, password)
    } finally {
      setWorking(false)
    }
  }

  const submitSignUp = async (e: React.FormEvent) => {
    e.preventDefault()
    clearError()
    setFeedback(null)

    if (password !== confirmPassword) {
      setFeedback('Passwords do not match.')
      return
    }

    if (password.length < 8) {
      setFeedback('Password must be at least 8 characters.')
      return
    }

    setWorking(true)
    try {
      await signup(email, password, '')
      setFeedback(`Account created for ${email}. Check your email, then sign in here.`)
      setMode('signin')
      setPassword('')
      setConfirmPassword('')
    } finally {
      setWorking(false)
    }
  }

  const submitForgot = async (e: React.FormEvent) => {
    e.preventDefault()
    clearError()
    setFeedback(null)
    setWorking(true)
    try {
      const response = await fetch('/djehuti/api/auth/password-reset-request', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email }),
      })

      if (!response.ok) {
        const text = await response.text()
        throw new Error(text || `Request failed with ${response.status}`)
      }

      setFeedback(`A reset link was sent to ${email}.`)
      setMode('signin')
    } catch (requestError) {
      setFeedback(requestError instanceof Error ? requestError.message : 'Reset request failed.')
    } finally {
      setWorking(false)
    }
  }

  return (
    <section className="mud-page mud-page-app">
      <div className="mud-shell mud-auth-shell">
        <div className="mud-auth-card">
          <div className="mud-kicker">LagDaemon MUD</div>
          <h1>{title}</h1>
          <p className="mud-auth-copy">{subtitle}</p>
          <ThemePicker />

          {mode === 'signin' && (
            <form className="auth-form" onSubmit={submitSignIn}>
              <label>
                <span>Email</span>
                <input type="email" value={email} onChange={e => setEmail(e.target.value)} autoComplete="email" required disabled={working || isLoading} />
              </label>
              <label>
                <span>Password</span>
                <input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="current-password" required disabled={working || isLoading} />
              </label>
              {(error || feedback) && <div className="auth-error">{error || feedback}</div>}
              <button type="submit" className="primary-action auth-button" disabled={working || isLoading || !email || !password}>
                {working || isLoading ? 'Signing in...' : 'Sign in'}
              </button>
            </form>
          )}

          {mode === 'signup' && (
            signupUsesOAuthOnly ? (
              <div className="mud-auth-copy-block">
                <p className="mud-auth-copy">
                  New account creation in the MUD app currently uses Google or GitHub sign-in so the security check stays intact.
                </p>
                {feedback && <div className="auth-error">{feedback}</div>}
              </div>
            ) : (
              <form className="auth-form" onSubmit={submitSignUp}>
                <label>
                  <span>Email</span>
                  <input type="email" value={email} onChange={e => setEmail(e.target.value)} autoComplete="email" required disabled={working || isLoading} />
                </label>
                <label>
                  <span>Password</span>
                  <input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="new-password" required disabled={working || isLoading} />
                </label>
                <label>
                  <span>Confirm password</span>
                  <input type="password" value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} autoComplete="new-password" required disabled={working || isLoading} />
                </label>
                {(error || feedback) && <div className="auth-error">{error || feedback}</div>}
                <button type="submit" className="primary-action auth-button" disabled={working || isLoading || !email || !password || !confirmPassword}>
                  {working || isLoading ? 'Creating account...' : 'Create account'}
                </button>
              </form>
            )
          )}

          {mode === 'forgot' && (
            <form className="auth-form" onSubmit={submitForgot}>
              <label>
                <span>Email</span>
                <input type="email" value={email} onChange={e => setEmail(e.target.value)} autoComplete="email" required disabled={working} />
              </label>
              {feedback && <div className="auth-error">{feedback}</div>}
              <button type="submit" className="primary-action auth-button" disabled={working || !email}>
                {working ? 'Sending...' : 'Send reset link'}
              </button>
            </form>
          )}

          <div className="auth-modal-divider">or continue with</div>
          <OAuthButtons />

          <div className="mud-auth-links">
            <button type="button" className="auth-link" onClick={() => switchMode('signin')}>Sign in</button>
            <button type="button" className="auth-link" onClick={() => switchMode('signup')}>Create account</button>
            <button type="button" className="auth-link" onClick={() => switchMode('forgot')}>Forgot password</button>
          </div>

          <div className="mud-auth-actions">
            {onBack && <button className="mud-back-btn" onClick={onBack}>← Game landing</button>}
            <a className="mud-back-btn" href="/">Back to LagDaemon</a>
          </div>
        </div>
      </div>
    </section>
  )
}

type MudScreen = 'landing' | 'auth' | 'game'

function MudRoot() {
  const { user, isLoading } = useAuth()
  const [screen, setScreen] = useState<MudScreen>('landing')

  useEffect(() => {
    if (screen === 'auth' && user) setScreen('game')
  }, [screen, user])

  if (isLoading) {
    return (
      <section className="mud-page mud-page-app">
        <div className="mud-shell mud-auth-shell">
          <div className="mud-auth-card">
            <div className="forum-loading">Loading MUD access…</div>
          </div>
        </div>
      </section>
    )
  }

  if (screen === 'game' && user) return <MudPage onExit={() => setScreen('landing')} />
  if (screen === 'auth' && !user) return <MudAuthScreen onBack={() => setScreen('landing')} />
  return <MudLanding onEnter={() => setScreen(user ? 'game' : 'auth')} />
}

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <MudRoot />
      </AuthProvider>
    </ThemeProvider>
  )
}
