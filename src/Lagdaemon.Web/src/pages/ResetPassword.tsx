import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { CheckCircle, AlertCircle } from 'lucide-react'

export function ResetPassword() {
  const [searchParams] = useSearchParams()
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [status, setStatus] = useState<'form' | 'success' | 'error'>('form')
  const [message, setMessage] = useState('')
  const [error, setError] = useState<string | null>(null)

  const token = searchParams.get('token')

  useEffect(() => {
    if (!token) {
      setStatus('error')
      setMessage('No reset token provided')
    }
  }, [token])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)

    if (password !== confirmPassword) {
      setError('Passwords do not match')
      return
    }

    if (password.length < 8) {
      setError('Password must be at least 8 characters')
      return
    }

    setIsLoading(true)

    try {
      const response = await fetch('/djehuti/api/auth/password-reset-confirm', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token, password }),
      })

      if (response.ok) {
        setStatus('success')
        setMessage('Your password has been reset! You can now sign in with your new password.')
      } else {
        const text = await response.text()
        setStatus('error')
        setMessage(text || 'Password reset failed. Token may be expired.')
      }
    } catch (err) {
      setStatus('error')
      setMessage(err instanceof Error ? err.message : 'Password reset failed')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="reset-password-container">
      <div className="reset-password-card">
        {status === 'form' && (
          <>
            <h2>Set a new password</h2>
            <form onSubmit={handleSubmit} className="reset-form">
              <label>
                <span>New password (min 8 characters)</span>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  required
                  disabled={isLoading}
                  autoComplete="new-password"
                />
              </label>

              <label>
                <span>Confirm password</span>
                <input
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="••••••••"
                  required
                  disabled={isLoading}
                  autoComplete="new-password"
                />
              </label>

              {error && <div className="reset-error">{error}</div>}

              <button
                type="submit"
                className="reset-button"
                disabled={isLoading || !password || !confirmPassword}
              >
                {isLoading ? 'Resetting...' : 'Reset password'}
              </button>
            </form>
          </>
        )}

        {status === 'success' && (
          <>
            <CheckCircle size={48} className="reset-icon success" />
            <h2>Password reset!</h2>
            <p>{message}</p>
            <a href="/" className="reset-button">
              Return to home
            </a>
          </>
        )}

        {status === 'error' && (
          <>
            <AlertCircle size={48} className="reset-icon error" />
            <h2>Reset failed</h2>
            <p>{message}</p>
            <a href="/" className="reset-button">
              Return to home
            </a>
          </>
        )}
      </div>
    </div>
  )
}
