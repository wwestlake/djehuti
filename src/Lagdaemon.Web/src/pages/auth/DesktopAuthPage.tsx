import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'

const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_OAUTH_CLIENT_ID || ''
const GITHUB_CLIENT_ID = import.meta.env.VITE_GITHUB_OAUTH_CLIENT_ID || ''

const pageStyle: React.CSSProperties = {
  minHeight: '100vh',
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  padding: '1rem',
}

const cardStyle: React.CSSProperties = {
  position: 'static',
  width: '100%',
}

// The desktop loopback + PKCE confirm screen. Deliberately NOT wrapped in
// ProtectedRoute -- that component redirects unauthenticated visitors to
// "/" and drops the current URL entirely, which would lose redirect_uri/
// state/code_challenge. Instead this page handles "not signed in" itself
// (inline login form) so the query params survive across that step, then
// shows the actual confirm screen once useAuth().user is set. Also rendered
// standalone in App.tsx (no Nav/notifications) -- this is a focused
// consent screen, not a normal site page.
export default function DesktopAuthPage() {
  const [params] = useSearchParams()
  const { user, login, isLoading: authLoading } = useAuth()

  const redirectUri = params.get('redirect_uri') || ''
  const state = params.get('state') || ''
  const appName = params.get('app') || 'A desktop app'
  const codeChallenge = params.get('code_challenge') || ''

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loggingIn, setLoggingIn] = useState(false)
  const [continuing, setContinuing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const isLoopback = /^http:\/\/(127\.0\.0\.1|localhost)(:\d+)?\//.test(redirectUri)

  if (!redirectUri || !state || !codeChallenge) {
    return (
      <div style={pageStyle}>
        <div className="modal auth-modal" style={cardStyle}>
          <div className="auth-modal-header">
            <h2>Sign-in link is incomplete</h2>
            <p className="auth-modal-subtitle">This page needs to be opened by a desktop app, not visited directly.</p>
          </div>
        </div>
      </div>
    )
  }

  if (!isLoopback) {
    return (
      <div style={pageStyle}>
        <div className="modal auth-modal" style={cardStyle}>
          <div className="auth-modal-header">
            <h2>Sign-in request rejected</h2>
            <p className="auth-modal-subtitle">The redirect address for this request isn't a local app address. If you didn't expect this, close this tab.</p>
          </div>
        </div>
      </div>
    )
  }

  // OAuth callbacks redirect to whatever path is passed as their own
  // `state` param (see safeOAuthRedirectTarget in Program.fs) -- pointing
  // that back at this exact URL (with redirect_uri/state/code_challenge
  // still attached) is what lets Google/GitHub sign-in land back on the
  // confirm screen instead of the homepage. safeOAuthRedirectTarget
  // unescapes once on top of ASP.NET's automatic query-string decode, so
  // this needs to be encoded twice to survive the round trip intact.
  const returnTo = encodeURIComponent(encodeURIComponent(window.location.pathname + window.location.search))

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoggingIn(true)
    try {
      await login(email, password)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sign in failed')
    } finally {
      setLoggingIn(false)
    }
  }

  const handleContinue = async () => {
    setError(null)
    setContinuing(true)
    try {
      const response = await fetch('/djehuti/api/auth/desktop/authorize', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ redirectUri, codeChallenge }),
      })
      if (!response.ok) throw new Error(await response.text() || 'Could not start sign-in')
      const data = await response.json()
      window.location.href = `${redirectUri}${redirectUri.includes('?') ? '&' : '?'}code=${encodeURIComponent(data.code)}&state=${encodeURIComponent(state)}`
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not start sign-in')
      setContinuing(false)
    }
  }

  if (authLoading) {
    return (
      <div style={pageStyle}>
        <div className="modal auth-modal" style={cardStyle}>
          <p className="auth-modal-subtitle">Loading…</p>
        </div>
      </div>
    )
  }

  return (
    <div style={pageStyle}>
      <div className="modal auth-modal" style={cardStyle}>
        {!user ? (
          <>
            <div className="auth-modal-header">
              <h2>Sign in to continue to {appName}</h2>
            </div>
            <form onSubmit={handleLogin} className="auth-form">
              <label>
                <span>Email</span>
                <input type="email" value={email} onChange={e => setEmail(e.target.value)}
                  placeholder="your@email.com" required disabled={loggingIn} autoComplete="email" />
              </label>
              <label>
                <span>Password</span>
                <input type="password" value={password} onChange={e => setPassword(e.target.value)}
                  placeholder="••••••••" required disabled={loggingIn} autoComplete="current-password" />
              </label>
              {error && <div className="auth-error">{error}</div>}
              <button type="submit" className="primary-action auth-button" disabled={loggingIn || !email || !password}>
                {loggingIn ? 'Signing in…' : 'Sign In'}
              </button>
            </form>

            <div className="auth-modal-divider">or continue with</div>

            <div className="oauth-buttons">
              <a href={`https://accounts.google.com/o/oauth2/v2/auth?client_id=${GOOGLE_CLIENT_ID}&redirect_uri=https://lagdaemon.com/djehuti/api/auth/oauth/google/callback&response_type=code&scope=openid%20email%20profile&state=${returnTo}`}
                className="oauth-button google" title="Sign in with Google">
                <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                  <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" />
                  <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" />
                  <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" />
                  <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" />
                </svg>
                <span>Google</span>
              </a>
              <a href={`https://github.com/login/oauth/authorize?client_id=${GITHUB_CLIENT_ID}&redirect_uri=https://lagdaemon.com/djehuti/api/auth/oauth/github/callback&scope=user:email&state=${returnTo}`}
                className="oauth-button github" title="Sign in with GitHub">
                <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                  <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z" />
                </svg>
                <span>GitHub</span>
              </a>
            </div>
          </>
        ) : (
          <>
            <div className="auth-modal-header">
              <h2>{appName} wants to sign in</h2>
              <p className="auth-modal-subtitle">Signed in as <strong>{user.email}</strong>. Continue to let {appName} access your account?</p>
            </div>
            {error && <div className="auth-error">{error}</div>}
            <button className="primary-action auth-button" onClick={handleContinue} disabled={continuing}>
              {continuing ? 'Continuing…' : 'Continue'}
            </button>
          </>
        )}
      </div>
    </div>
  )
}
