import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'

const BASE = '/djehuti'

interface Announcement {
  id: string
  title: string
  body: string
  priority: number
  publishedAt: string | null
  expiresAt: string | null
  createdAt: string
}

async function apiFetch(url: string, opts?: RequestInit) {
  const res = await fetch(url, { credentials: 'include', ...opts })
  if (!res.ok) throw new Error(res.statusText)
  return res.json()
}

export default function AnnouncementsPage() {
  const { user } = useAuth()
  const [announcements, setAnnouncements] = useState<Announcement[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [subscribed, setSubscribed] = useState<boolean | null>(null)
  const [email, setEmail] = useState('')
  const [subStatus, setSubStatus] = useState<'idle' | 'pending' | 'done' | 'error'>('idle')

  useEffect(() => {
    apiFetch(`${BASE}/api/announcements?limit=50`)
      .then(setAnnouncements)
      .catch(() => setError('Failed to load announcements.'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    if (!user) return
    apiFetch(`${BASE}/api/announcements/subscribed`)
      .then(d => setSubscribed(d.subscribed ?? false))
      .catch(() => {})
  }, [user])

  const subscribe = async (e: React.FormEvent) => {
    e.preventDefault()
    const addr = user?.email || email.trim()
    if (!addr) return
    setSubStatus('pending')
    try {
      await apiFetch(`${BASE}/api/announcements/subscribe`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: addr }),
      })
      setSubStatus('done')
      if (user) setSubscribed(true)
    } catch {
      setSubStatus('error')
    }
  }

  const unsubscribe = async () => {
    if (!confirm('Unsubscribe from announcements?')) return
    try {
      await apiFetch(`${BASE}/api/announcements/subscribe`, { method: 'DELETE' })
      setSubscribed(false)
    } catch { setError('Failed to unsubscribe.') }
  }

  return (
    <div className="community-page">
      <div className="ann-page-header">
        <h2 className="community-page-title">Announcements</h2>

        {/* Subscription widget */}
        <div className="ann-sub-widget">
          {user ? (
            subscribed === true ? (
              <div className="ann-sub-status">
                <span className="ann-sub-active">✓ Subscribed to email alerts</span>
                <button className="ann-unsub-btn" onClick={unsubscribe}>Unsubscribe</button>
              </div>
            ) : subscribed === false ? (
              subStatus === 'done' ? (
                <p className="ann-sub-confirm-msg">Check your email to confirm your subscription.</p>
              ) : (
                <button className="tiptap-action-btn primary ann-sub-btn" onClick={subscribe} disabled={subStatus === 'pending'}>
                  {subStatus === 'pending' ? 'Subscribing…' : 'Subscribe for email alerts'}
                </button>
              )
            ) : null
          ) : (
            subStatus === 'done' ? (
              <p className="ann-sub-confirm-msg">Check your email to confirm your subscription.</p>
            ) : (
              <form className="ann-guest-sub-form" onSubmit={subscribe}>
                <input
                  type="email"
                  className="papers-new-input"
                  placeholder="your@email.com"
                  value={email}
                  onChange={e => setEmail(e.target.value)}
                  required
                />
                <button type="submit" className="tiptap-action-btn primary" disabled={subStatus === 'pending'}>
                  {subStatus === 'pending' ? 'Subscribing…' : 'Get email alerts'}
                </button>
              </form>
            )
          )}
          {subStatus === 'error' && <p className="auth-error" style={{ marginTop: 4 }}>Failed to subscribe. Try again.</p>}
        </div>
      </div>

      {error && <p className="auth-error">{error}</p>}
      {loading && <div className="forum-loading">Loading…</div>}

      {!loading && announcements.length === 0 && (
        <p className="forum-empty">No announcements at this time.</p>
      )}

      <div className="ann-list">
        {announcements.map(a => (
          <div key={a.id} className={`ann-card priority-${a.priority > 0 ? 'high' : 'normal'}`}>
            {a.priority > 0 && <span className="ann-priority-badge">Important</span>}
            <h3 className="ann-card-title">{a.title}</h3>
            <div className="ann-card-body" dangerouslySetInnerHTML={{ __html: a.body }} />
            <div className="ann-card-meta">
              {a.publishedAt && <span>{new Date(a.publishedAt).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })}</span>}
              {a.expiresAt && <span className="ann-expires">Expires {new Date(a.expiresAt).toLocaleDateString()}</span>}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
