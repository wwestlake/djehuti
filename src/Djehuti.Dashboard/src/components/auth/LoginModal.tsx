import { useState } from 'react'
import { X, LogIn } from 'lucide-react'
import { useAuth } from '../../contexts/AuthContext'

type LoginModalProps = {
  open: boolean
  onClose: () => void
  onSwitchToSignup: () => void
}

export function LoginModal({ open, onClose, onSwitchToSignup }: LoginModalProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const { login, error, clearError } = useAuth()

  if (!open) return null

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    clearError()
    setIsLoading(true)

    try {
      await login(email, password)
      setEmail('')
      setPassword('')
      onClose()
    } catch {
      // Error is managed by context
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="modal-overlay open" onClick={(e) => {
      if (e.target === e.currentTarget) onClose()
    }}>
      <div className="modal auth-modal" role="dialog" aria-modal="true" aria-label="Login">
        <button className="modal-close" onClick={onClose} aria-label="Close">
          <X size={20} />
        </button>

        <div className="auth-modal-header">
          <LogIn size={24} />
          <h2>Sign in to Djehuti</h2>
          <p className="auth-modal-subtitle">Access your saved datasets and profile</p>
        </div>

        <form onSubmit={handleSubmit} className="auth-form">
          <label>
            <span>Email</span>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="your@email.com"
              required
              disabled={isLoading}
              autoComplete="email"
            />
          </label>

          <label>
            <span>Password</span>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              required
              disabled={isLoading}
              autoComplete="current-password"
            />
          </label>

          {error && <div className="auth-error">{error}</div>}

          <button
            type="submit"
            className="primary-action auth-button"
            disabled={isLoading || !email || !password}
          >
            {isLoading ? 'Signing in...' : 'Sign in'}
          </button>
        </form>

        <div className="auth-modal-footer">
          <span>Don't have an account?</span>
          <button
            type="button"
            className="auth-link"
            onClick={() => {
              clearError()
              onSwitchToSignup()
            }}
          >
            Create one
          </button>
        </div>

        <div className="auth-modal-divider">or</div>

        <div className="auth-modal-footer">
          <span>Forgot your password?</span>
          <button type="button" className="auth-link" disabled>
            Reset it
          </button>
        </div>
      </div>
    </div>
  )
}
