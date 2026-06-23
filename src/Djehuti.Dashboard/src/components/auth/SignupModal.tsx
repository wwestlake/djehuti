import { useState } from 'react'
import { X, UserPlus } from 'lucide-react'
import { useAuth } from '../../contexts/AuthContext'

declare global {
  interface Window {
    hcaptcha?: {
      render: (element: string, options: { sitekey: string; callback: (token: string) => void }) => void
      reset: () => void
    }
  }
}

type SignupModalProps = {
  open: boolean
  onClose: () => void
  onSwitchToLogin: () => void
}

export function SignupModal({ open, onClose, onSwitchToLogin }: SignupModalProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [hcaptchaToken, setHcaptchaToken] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [successMessage, setSuccessMessage] = useState('')
  const { signup, error, clearError } = useAuth()

  const hcaptchaSiteKey = import.meta.env.VITE_HCAPTCHA_SITE_KEY || ''

  if (!open) return null

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    clearError()
    setSuccessMessage('')

    if (password !== confirmPassword) {
      alert('Passwords do not match')
      return
    }

    if (password.length < 8) {
      alert('Password must be at least 8 characters')
      return
    }

    if (!hcaptchaToken) {
      alert('Please complete the CAPTCHA')
      return
    }

    setIsLoading(true)

    try {
      await signup(email, password, hcaptchaToken)
      setSuccessMessage(
        'Account created! Check your email to verify your address. ' +
        'Then you can sign in.'
      )
      setTimeout(() => {
        setEmail('')
        setPassword('')
        setConfirmPassword('')
        setHcaptchaToken('')
        setSuccessMessage('')
        onSwitchToLogin()
      }, 3000)
    } catch {
      // Error is managed by context
      if (window.hcaptcha) {
        window.hcaptcha.reset()
      }
      setHcaptchaToken('')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="modal-overlay open" onClick={(e) => {
      if (e.target === e.currentTarget) onClose()
    }}>
      <div className="modal auth-modal" role="dialog" aria-modal="true" aria-label="Sign up">
        <button className="modal-close" onClick={onClose} aria-label="Close">
          <X size={20} />
        </button>

        <div className="auth-modal-header">
          <UserPlus size={24} />
          <h2>Create Djehuti account</h2>
          <p className="auth-modal-subtitle">Save and manage your datasets</p>
        </div>

        {successMessage && (
          <div className="auth-success">{successMessage}</div>
        )}

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
            <span>Password (min 8 characters)</span>
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

          {hcaptchaSiteKey && (
            <div className="hcaptcha-container">
              <div
                id="hcaptcha"
                data-sitekey={hcaptchaSiteKey}
                data-callback="hcaptchaCallback"
              />
            </div>
          )}

          {error && <div className="auth-error">{error}</div>}

          <button
            type="submit"
            className="primary-action auth-button"
            disabled={isLoading || !email || !password || !confirmPassword || !hcaptchaToken}
          >
            {isLoading ? 'Creating account...' : 'Create account'}
          </button>
        </form>

        <div className="auth-modal-footer">
          <span>Already have an account?</span>
          <button
            type="button"
            className="auth-link"
            onClick={() => {
              clearError()
              onSwitchToLogin()
            }}
          >
            Sign in
          </button>
        </div>

        <div className="auth-modal-terms">
          <small>
            By creating an account, you agree to our terms.
            Your data is stored securely and never shared.
          </small>
        </div>
      </div>
    </div>
  )
}
