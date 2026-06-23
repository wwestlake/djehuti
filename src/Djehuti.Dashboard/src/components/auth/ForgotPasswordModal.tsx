import { useState } from 'react'
import { X, Mail } from 'lucide-react'

type ForgotPasswordModalProps = {
  open: boolean
  onClose: () => void
  onSwitchToLogin: () => void
}

export function ForgotPasswordModal({ open, onClose, onSwitchToLogin }: ForgotPasswordModalProps) {
  const [email, setEmail] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [successEmail, setSuccessEmail] = useState('')
  const [error, setError] = useState<string | null>(null)

  const apiBase = import.meta.env.VITE_API_BASE || 'http://localhost:5087'

  if (!open) return null

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setIsLoading(true)

    try {
      const response = await fetch(`${apiBase}/api/auth/password-reset-request`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email }),
      })

      if (!response.ok) {
        const text = await response.text()
        throw new Error(text || `Request failed with ${response.status}`)
      }

      setSuccessEmail(email)
      setEmail('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Request failed')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="modal-overlay open" onClick={(e) => {
      if (e.target === e.currentTarget) onClose()
    }}>
      <div className="modal auth-modal" role="dialog" aria-modal="true" aria-label="Reset password">
        <button className="modal-close" onClick={onClose} aria-label="Close">
          <X size={20} />
        </button>

        <div className="auth-modal-header">
          <Mail size={24} />
          <h2>Reset your password</h2>
          <p className="auth-modal-subtitle">We'll send you a reset link</p>
        </div>

        {successEmail && (
          <div className="auth-verification-pending">
            <div className="verification-icon">✓</div>
            <h3>Check your email</h3>
            <p>We sent a password reset link to <strong>{successEmail}</strong></p>
            <p className="verification-note">Click the link in your email to set a new password.</p>
            <button
              type="button"
              className="primary-action"
              onClick={() => {
                setSuccessEmail('')
                onSwitchToLogin()
              }}
            >
              Back to sign in
            </button>
          </div>
        )}

        {!successEmail && (
        <form onSubmit={handleSubmit} className="auth-form">
          <label>
            <span>Email address</span>
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

          {error && <div className="auth-error">{error}</div>}

          <button
            type="submit"
            className="primary-action auth-button"
            disabled={isLoading || !email}
          >
            {isLoading ? 'Sending...' : 'Send reset link'}
          </button>
        </form>
        )}

        {!successEmail && (
        <div className="auth-modal-footer">
          <span>Remember your password?</span>
          <button
            type="button"
            className="auth-link"
            onClick={() => {
              setError(null)
              setEmail('')
              onSwitchToLogin()
            }}
          >
            Sign in
          </button>
        </div>
        )}
      </div>
    </div>
  )
}
