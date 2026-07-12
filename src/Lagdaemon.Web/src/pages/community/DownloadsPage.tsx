import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
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
  tagName: string
  name: string | null
  prerelease: boolean
  assets: ReleaseAsset[]
  publishedAt: string | null
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export default function DownloadsPage() {
  const { user } = useAuth()
  const [products, setProducts] = useState<Product[]>([])
  const [latestByProduct, setLatestByProduct] = useState<Record<string, Release | null>>({})
  const [entitlements, setEntitlements] = useState<string[] | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch(`${BASE}/api/products`)
      .then(res => res.json())
      .then((list: Product[]) => {
        setProducts(list)
        Promise.all(list.map(p =>
          fetch(`${BASE}/api/products/${p.slug}/releases`)
            .then(res => res.ok ? res.json() : { releases: [] })
            .then(data => [p.slug, (data.releases?.[0] as Release) ?? null] as const)
            .catch(() => [p.slug, null] as const)
        )).then(entries => setLatestByProduct(Object.fromEntries(entries)))
      })
      .catch(() => setProducts([]))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    if (!user) { setEntitlements(null); return }
    fetch(`${BASE}/api/users/entitlements`, { credentials: 'include' })
      .then(res => res.ok ? res.json() : { entitlements: [] })
      .then(data => setEntitlements(data.entitlements ?? []))
      .catch(() => setEntitlements([]))
  }, [user])

  return (
    <div style={{ maxWidth: 900, margin: '2rem auto', padding: '0 1rem' }}>
      <h1>Downloads</h1>
      <p style={{ color: 'var(--text-muted)' }}>Desktop apps built on the Djehuti/LagDaemon ecosystem.</p>

      {loading && <p style={{ color: 'var(--text-muted)' }}>Loading…</p>}
      {!loading && products.length === 0 && <p style={{ color: 'var(--text-muted)' }}>No downloads published yet.</p>}

      <div style={{ display: 'grid', gap: 16, marginTop: 24 }}>
        {products.map(p => {
          const latest = latestByProduct[p.slug]
          const gated = !!p.requiredTierId
          const qualifies = !gated || (entitlements?.includes(p.slug) ?? false)
          return (
            <div key={p.id} style={{
              background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius)',
              padding: '1.25rem 1.5rem',
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 16, flexWrap: 'wrap' }}>
                <div>
                  <Link to={`/downloads/${p.slug}`} style={{ textDecoration: 'none', color: 'inherit' }}>
                    <h3 style={{ margin: '0 0 4px' }}>{p.name}</h3>
                  </Link>
                  {p.description && <p style={{ margin: 0, color: 'var(--text-muted)', fontSize: '0.9rem' }}>{p.description}</p>}
                  {latest && (
                    <p style={{ margin: '8px 0 0', fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                      {latest.name || latest.tagName}
                      {latest.prerelease && ' (pre-release)'}
                      {latest.publishedAt && ` · ${new Date(latest.publishedAt).toLocaleDateString()}`}
                    </p>
                  )}
                  {latestByProduct[p.slug] === null && (
                    <p style={{ margin: '8px 0 0', fontSize: '0.8rem', color: 'var(--text-muted)' }}>No releases published yet.</p>
                  )}
                </div>

                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                  {latest && gated && !qualifies && (
                    <span style={{ padding: '0.5rem 0.9rem', borderRadius: 6, background: 'var(--bg)', color: 'var(--text-muted)', fontSize: '0.85rem' }}>
                      Requires higher Patreon tier
                    </span>
                  )}
                  {latest && qualifies && latest.assets.map(a => (
                    <a key={a.name} href={a.url} className="primary-action auth-button" style={{ display: 'inline-block', textDecoration: 'none', width: 'auto', padding: '0.5rem 0.9rem', fontSize: '0.85rem' }}>
                      {a.name} <span style={{ opacity: 0.7 }}>({formatBytes(a.sizeBytes)})</span>
                    </a>
                  ))}
                  {latest && (
                    <Link to={`/downloads/${p.slug}`} style={{ alignSelf: 'center', fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                      Version history →
                    </Link>
                  )}
                </div>
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
