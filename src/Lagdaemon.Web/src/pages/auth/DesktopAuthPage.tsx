import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'

// The desktop loopback + PKCE confirm screen. Deliberately NOT wrapped in
// ProtectedRoute -- that component redirects unauthenticated visitors to
// "/" and drops the current URL entirely, which would lose redirect_uri/
// state/code_challenge. Instead this page handles "not signed in" itself
// (inline login form) so the query params survive across that step, then
// shows the actual confirm screen once useAuth().user is set.
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
      <div className="settings-section" style={{ maxWidth: 480, margin: '4rem auto' }}>
        <h2>Sign-in link is incomplete</h2>
        <p className="settings-hint">This page needs to be opened by a desktop app, not visited directly.</p>
      </div>
    )
  }

  if (!isLoopback) {
    return (
      <div className="settings-section" style={{ maxWidth: 480, margin: '4rem auto' }}>
        <h2>Sign-in request rejected</h2>
        <p className="settings-hint">The redirect address for this request isn't a local app address. If you didn't expect this, close this tab.</p>
      </div>
    )
  }

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
    return <div className="settings-section" style={{ maxWidth: 480, margin: '4rem auto' }}><p className="settings-hint">Loading…</p></div>
  }

  return (
    <div className="settings-section" style={{ maxWidth: 480, margin: '4rem auto' }}>
      {!user ? (
        <>
          <h2>Sign in to continue to {appName}</h2>
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
            {error && <div className="settings-error">{error}</div>}
            <button type="submit" className="settings-button" disabled={loggingIn}>
              {loggingIn ? 'Signing in…' : 'Sign In'}
            </button>
          </form>
        </>
      ) : (
        <>
          <h2>{appName} wants to sign in</h2>
          <p className="settings-hint">Signed in as <strong>{user.email}</strong>. Continue to let {appName} access your account?</p>
          {error && <div className="settings-error">{error}</div>}
          <div className="settings-input-group">
            <button className="settings-button" onClick={handleContinue} disabled={continuing}>
              {continuing ? 'Continuing…' : 'Continue'}
            </button>
          </div>
        </>
      )}
    </div>
  )
}
