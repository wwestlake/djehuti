import { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'

const BASE = '/djehuti'

interface Product {
  id: string
  slug: string
  name: string
  description: string | null
  requiredTierId: string | null
}
interface ReleaseAsset {
  name: string
  url: string
  sizeBytes: number
}
interface Release {
  id: string
  tagName: string
  name: string | null
  body: string | null
  prerelease: boolean
  assets: ReleaseAsset[]
  publishedAt: string | null
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export default function DownloadProductPage() {
  const { slug } = useParams<{ slug: string }>()
  const { user } = useAuth()
  const [product, setProduct] = useState<Product | null>(null)
  const [releases, setReleases] = useState<Release[]>([])
  const [entitlements, setEntitlements] = useState<string[] | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [expanded, setExpanded] = useState<Set<string>>(new Set())

  useEffect(() => {
    setLoading(true)
    setError(null)
    fetch(`${BASE}/api/products/${slug}/releases`)
      .then(res => {
        if (!res.ok) throw new Error(res.status === 404 ? 'Product not found' : 'Could not load this product')
        return res.json()
      })
      .then(data => { setProduct(data.product); setReleases(data.releases) })
      .catch(err => setError(err.message))
      .finally(() => setLoading(false))
  }, [slug])

  useEffect(() => {
    if (!user) { setEntitlements(null); return }
    fetch(`${BASE}/api/users/entitlements`, { credentials: 'include' })
      .then(res => res.ok ? res.json() : { entitlements: [] })
      .then(data => setEntitlements(data.entitlements ?? []))
      .catch(() => setEntitlements([]))
  }, [user])

  const toggleExpanded = (id: string) => {
    setExpanded(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id); else next.add(id)
      return next
    })
  }

  if (loading) return <div style={{ maxWidth: 900, margin: '2rem auto', padding: '0 1rem' }}><p style={{ color: 'var(--text-muted)' }}>Loading…</p></div>
  if (error || !product) return (
    <div style={{ maxWidth: 900, margin: '2rem auto', padding: '0 1rem' }}>
      <p>{error ?? 'Product not found'}</p>
      <Link to="/downloads">← Back to Downloads</Link>
    </div>
  )

  const gated = !!product.requiredTierId
  const qualifies = !gated || (entitlements?.includes(product.slug) ?? false)
  const latest = releases[0]
  const older = releases.slice(1)

  return (
    <div style={{ maxWidth: 900, margin: '2rem auto', padding: '0 1rem' }}>
      <Link to="/downloads" style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>← All downloads</Link>
      <h1 style={{ marginBottom: 4 }}>{product.name}</h1>
      {product.description && <p style={{ color: 'var(--text-muted)' }}>{product.description}</p>}

      {gated && !qualifies && (
        <div style={{ background: 'var(--surface)', border: '1px solid var(--accent)', borderRadius: 'var(--radius)', padding: '1rem 1.25rem', margin: '1rem 0' }}>
          {user
            ? <p style={{ margin: 0 }}>Downloading {product.name} requires a higher Patreon tier. <a href="https://www.patreon.com/lagdaemon" target="_blank" rel="noopener noreferrer">Check tiers →</a></p>
            : <p style={{ margin: 0 }}>Sign in and support on Patreon to unlock downloads for {product.name}.</p>}
        </div>
      )}

      {releases.length === 0 && <p style={{ color: 'var(--text-muted)' }}>No releases published yet.</p>}

      {latest && (
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius)', padding: '1.5rem', marginTop: 16 }}>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 4 }}>
            <h2 style={{ margin: 0 }}>{latest.name || latest.tagName}</h2>
            <span style={{ fontSize: '0.8rem', color: 'var(--accent)', fontWeight: 600 }}>LATEST</span>
            {latest.prerelease && <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>(pre-release)</span>}
          </div>
          {latest.publishedAt && <p style={{ fontSize: '0.8rem', color: 'var(--text-muted)', margin: '0 0 12px' }}>{new Date(latest.publishedAt).toLocaleDateString()}</p>}
          {latest.body && <pre style={{ whiteSpace: 'pre-wrap', fontFamily: 'inherit', fontSize: '0.9rem', margin: '0 0 16px' }}>{latest.body}</pre>}
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
            {latest.assets.map(a => (
              qualifies
                ? <a key={a.name} href={a.url} className="primary-action auth-button" style={{ display: 'inline-block', textDecoration: 'none', width: 'auto', padding: '0.6rem 1rem' }}>
                    {a.name} <span style={{ opacity: 0.7 }}>({formatBytes(a.sizeBytes)})</span>
                  </a>
                : <span key={a.name} style={{ padding: '0.6rem 1rem', borderRadius: 6, background: 'var(--bg)', color: 'var(--text-muted)', fontSize: '0.9rem' }}>
                    {a.name} <span style={{ opacity: 0.7 }}>({formatBytes(a.sizeBytes)})</span>
                  </span>
            ))}
          </div>
        </div>
      )}

      {older.length > 0 && (
        <div style={{ marginTop: 24 }}>
          <h3>Previous versions</h3>
          {older.map(r => (
            <div key={r.id} style={{ borderBottom: '1px solid var(--border)', padding: '12px 0' }}>
              <button
                onClick={() => toggleExpanded(r.id)}
                style={{ background: 'none', border: 'none', color: 'inherit', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8, padding: 0, font: 'inherit', width: '100%', textAlign: 'left' }}
              >
                <span style={{ fontWeight: 600 }}>{r.name || r.tagName}</span>
                {r.prerelease && <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>(pre-release)</span>}
                {r.publishedAt && <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginLeft: 'auto' }}>{new Date(r.publishedAt).toLocaleDateString()}</span>}
              </button>
              {expanded.has(r.id) && (
                <div style={{ marginTop: 12 }}>
                  {r.body && <pre style={{ whiteSpace: 'pre-wrap', fontFamily: 'inherit', fontSize: '0.85rem', color: 'var(--text-muted)' }}>{r.body}</pre>}
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                    {r.assets.map(a => (
                      qualifies
                        ? <a key={a.name} href={a.url} style={{ fontSize: '0.85rem' }}>{a.name} ({formatBytes(a.sizeBytes)})</a>
                        : <span key={a.name} style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>{a.name} ({formatBytes(a.sizeBytes)})</span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
