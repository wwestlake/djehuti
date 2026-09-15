import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { betaApi } from '../../api/betaApi'
import type { BetaProduct } from '../../api/betaApi'

export default function BetaTestPage() {
  const { user, isLoading: authLoading, openLogin, openSignup } = useAuth()
  const [products, setProducts] = useState<BetaProduct[]>([])
  const [loading, setLoading] = useState(true)
  const [joined, setJoined] = useState<Set<string>>(new Set())
  const [joiningSlug, setJoiningSlug] = useState<string | null>(null)
  const [joiningAll, setJoiningAll] = useState(false)

  useEffect(() => {
    betaApi.getOpenProducts()
      .then(setProducts)
      .finally(() => setLoading(false))
  }, [])

  const joinOne = async (slug: string) => {
    setJoiningSlug(slug)
    try {
      await betaApi.signup(slug)
      setJoined(prev => new Set(prev).add(slug))
    } catch {
      // swallow -- the button reverts, user can just retry
    } finally {
      setJoiningSlug(null)
    }
  }

  const joinAll = async () => {
    setJoiningAll(true)
    try {
      const remaining = products.filter(p => !joined.has(p.slug))
      for (const p of remaining) {
        try {
          await betaApi.signup(p.slug)
          setJoined(prev => new Set(prev).add(p.slug))
        } catch {
          // keep going with the rest even if one fails
        }
      }
    } finally {
      setJoiningAll(false)
    }
  }

  return (
    <div className="supporters-page">
      <div className="supporters-hero">
        <h1>Beta Test Program</h1>
        <p>
          Help shape the Djehuti Suite before it ships. Sign up to test a specific app that's currently
          open for beta, or join all of them at once -- one click each, no form to fill out.
        </p>
        <p>
          Beta testers get Curious Mind-tier access to lagdaemon.com for as long as they're active --
          no payment, no obligation beyond submitting feedback at least once every 30 days. Miss that
          window and you're simply dropped from the program; there's no penalty, and you're welcome to
          join again any time.
        </p>
      </div>

      <div style={{ maxWidth: 560, margin: '0 auto', padding: '0 16px' }}>
        {authLoading || loading ? (
          <p style={{ textAlign: 'center', color: 'var(--text-muted)' }}>Loading…</p>
        ) : !user ? (
          <div style={{ textAlign: 'center', display: 'flex', flexDirection: 'column', gap: 12 }}>
            <p style={{ color: 'var(--text-muted)' }}>
              You'll need a lagdaemon.com account to sign up for beta testing.
            </p>
            <div style={{ display: 'flex', gap: 12, justifyContent: 'center' }}>
              <button className="supporters-patreon-btn" onClick={openLogin}>Log In</button>
              <button className="supporters-patreon-btn" onClick={openSignup}>Create Account</button>
            </div>
          </div>
        ) : products.length === 0 ? (
          <p style={{ textAlign: 'center', color: 'var(--text-muted)' }}>
            Nothing is open for beta signup right now -- check back soon.
          </p>
        ) : (
          <>
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
              <button
                className="post-action"
                disabled={joiningAll || products.every(p => joined.has(p.slug))}
                onClick={joinAll}
              >
                {joiningAll ? 'Joining all…' : 'Join all'}
              </button>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              {products.map(p => {
                const isJoined = joined.has(p.slug)
                return (
                  <div key={p.slug} style={{
                    display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12,
                    padding: '1rem 1.25rem', borderRadius: 'var(--radius)',
                    background: 'var(--surface)', border: '1px solid var(--border)',
                  }}>
                    <div>
                      <div style={{ fontWeight: 600 }}>{p.name}</div>
                      {p.description && <div style={{ color: 'var(--text-muted)', fontSize: '0.85rem', marginTop: 2 }}>{p.description}</div>}
                    </div>
                    <button
                      className="supporters-patreon-btn"
                      style={{ flexShrink: 0 }}
                      disabled={isJoined || joiningSlug === p.slug || joiningAll}
                      onClick={() => joinOne(p.slug)}
                    >
                      {isJoined ? 'Joined ✓' : (joiningSlug === p.slug ? 'Joining…' : 'Join beta')}
                    </button>
                  </div>
                )
              })}
            </div>
            {joined.size > 0 && (
              <p style={{ textAlign: 'center', marginTop: 20 }}>
                <Link to="/downloads">Go to Downloads →</Link>
              </p>
            )}
          </>
        )}
      </div>
    </div>
  )
}
