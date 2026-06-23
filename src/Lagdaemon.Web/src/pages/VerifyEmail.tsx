import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { CheckCircle, AlertCircle } from 'lucide-react'

export function VerifyEmail() {
  const [searchParams] = useSearchParams()
  const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading')
  const [message, setMessage] = useState('')

  useEffect(() => {
    const verifyEmail = async () => {
      const token = searchParams.get('token')
      if (!token) {
        setStatus('error')
        setMessage('No verification token provided')
        return
      }

      try {
        const response = await fetch('http://localhost:5087/api/auth/verify-email', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ token }),
        })

        if (response.ok) {
          setStatus('success')
          setMessage('Your email has been verified! You can now sign in.')
        } else {
          const text = await response.text()
          setStatus('error')
          setMessage(text || 'Verification failed. Token may be expired.')
        }
      } catch (err) {
        setStatus('error')
        setMessage(err instanceof Error ? err.message : 'Verification failed')
      }
    }

    verifyEmail()
  }, [searchParams])

  return (
    <div className="verify-email-container">
      <div className="verify-email-card">
        {status === 'loading' && (
          <>
            <div className="verify-spinner" />
            <h2>Verifying your email...</h2>
          </>
        )}

        {status === 'success' && (
          <>
            <CheckCircle size={48} className="verify-icon success" />
            <h2>Email verified!</h2>
            <p>{message}</p>
            <a href="/" className="verify-button">
              Return to home
            </a>
          </>
        )}

        {status === 'error' && (
          <>
            <AlertCircle size={48} className="verify-icon error" />
            <h2>Verification failed</h2>
            <p>{message}</p>
            <a href="/" className="verify-button">
              Return to home
            </a>
          </>
        )}
      </div>
    </div>
  )
}
