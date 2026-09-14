import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { betaApi } from '../../api/betaApi'
import type { BetaProduct } from '../../api/betaApi'

export default function BetaTestPage() {
  const { user } = useAuth()
  const [products, setProducts] = useState<BetaProduct[]>([])
  const [loading, setLoading] = useState(true)
  const [email, setEmail] = useState('')
  const [selectedSlug, setSelectedSlug] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [result, setResult] = useState<'ok' | 'error' | null>(null)

  useEffect(() => {
    betaApi.getOpenProducts()
      .then(ps => {
        setProducts(ps)
        if (ps.length > 0) setSelectedSlug(ps[0].slug)
      })
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    if (user?.email) setEmail(user.email)
  }, [user])

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!email.trim() || !selectedSlug) return
    setSubmitting(true)
    setResult(null)
    try {
      await betaApi.signup(email.trim(), selectedSlug)
      setResult('ok')
    } catch {
      setResult('error')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="supporters-page">
      <div className="supporters-hero">
        <h1>Beta Test Program</h1>
        <p>
          Help shape the Djehuti Suite before it ships. Sign up to test a specific app that's currently
          open for beta, and you'll get an email with what to install and how to get started.
        </p>
        <p>
          Beta testers get Curious Mind-tier access to lagdaemon.com for as long as they're active --
          no payment, no obligation beyond submitting feedback at least once every 30 days. Miss that
          window and you're simply dropped from the program; there's no penalty, and you're welcome to
          sign up again any time.
        </p>
      </div>

      <div style={{ maxWidth: 480, margin: '0 auto', padding: '0 16px' }}>
        {loading ? (
          <p style={{ textAlign: 'center', color: 'var(--text-muted)' }}>Loading…</p>
        ) : products.length === 0 ? (
          <p style={{ textAlign: 'center', color: 'var(--text-muted)' }}>
            Nothing is open for public beta signup right now -- check back soon.
          </p>
        ) : result === 'ok' ? (
          <div style={{ textAlign: 'center' }}>
            <h3>You're signed up.</h3>
            <p style={{ color: 'var(--text-muted)' }}>
              Check your email ({email}) for next steps.
            </p>
          </div>
        ) : (
          <form onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <label style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
              Which app?
              <select
                className="admin-role-select"
                style={{ width: '100%', marginTop: 4 }}
                value={selectedSlug}
                onChange={e => setSelectedSlug(e.target.value)}
              >
                {products.map(p => (
                  <option key={p.slug} value={p.slug}>{p.name}</option>
                ))}
              </select>
            </label>
            {products.find(p => p.slug === selectedSlug)?.description && (
              <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem', margin: 0 }}>
                {products.find(p => p.slug === selectedSlug)?.description}
              </p>
            )}
            <label style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
              Email
              <input
                className="admin-search-input"
                style={{ width: '100%', marginTop: 4 }}
                type="email"
                required
                value={email}
                onChange={e => setEmail(e.target.value)}
                placeholder="you@example.com"
              />
            </label>
            <button type="submit" className="supporters-patreon-btn" disabled={submitting || !email.trim()}>
              {submitting ? 'Signing up…' : 'Sign up to beta test'}
            </button>
            {result === 'error' && (
              <p style={{ color: 'var(--danger, #e5484d)', fontSize: '0.9rem' }}>
                Something went wrong. Please try again in a moment.
              </p>
            )}
          </form>
        )}
      </div>
    </div>
  )
}
