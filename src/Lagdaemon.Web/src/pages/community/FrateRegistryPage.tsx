import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { frateApi } from '../../api/frateApi'
import type { PodSummary } from '../../api/frateApi'

export default function FrateRegistryPage() {
  const [pods, setPods] = useState<PodSummary[]>([])
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    const handle = setTimeout(() => {
      frateApi.searchPods(query || undefined)
        .then(setPods)
        .catch(() => setPods([]))
        .finally(() => setLoading(false))
    }, 250)
    return () => clearTimeout(handle)
  }, [query])

  return (
    <div style={{ maxWidth: 900, margin: '2rem auto', padding: '0 1rem' }}>
      <h1>Frate Pods</h1>
      <p style={{ color: 'var(--text-muted)' }}>
        The package registry for Frust -- browse published pods, their exports, and license.
        Install with <code>frate add &lt;name&gt;</code>.
      </p>

      <input
        className="admin-search-input"
        placeholder="Search pods…"
        value={query}
        onChange={e => setQuery(e.target.value)}
        style={{ width: '100%', maxWidth: 360, margin: '16px 0' }}
      />

      {loading && <p style={{ color: 'var(--text-muted)' }}>Loading…</p>}
      {!loading && pods.length === 0 && <p style={{ color: 'var(--text-muted)' }}>No pods found.</p>}

      <div style={{ display: 'grid', gap: 16, marginTop: 8 }}>
        {pods.map(p => (
          <Link key={p.name} to={`/frate/${p.name}`} style={{ textDecoration: 'none', color: 'inherit' }}>
            <div style={{
              background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius)',
              padding: '1.25rem 1.5rem',
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 12, flexWrap: 'wrap' }}>
                <h3 style={{ margin: 0 }}>{p.name}</h3>
                <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>v{p.latestVersion} · {p.license}</span>
              </div>
              {p.description && <p style={{ margin: '8px 0 0', color: 'var(--text-muted)', fontSize: '0.9rem' }}>{p.description}</p>}
              {p.exports.length > 0 && (
                <p style={{ margin: '10px 0 0', fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                  Exports: {p.exports.join(', ')}
                </p>
              )}
            </div>
          </Link>
        ))}
      </div>
    </div>
  )
}
