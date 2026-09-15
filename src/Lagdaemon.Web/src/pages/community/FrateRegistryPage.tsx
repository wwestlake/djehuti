import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { frateApi } from '../../api/frateApi'
import type { PodSummary } from '../../api/frateApi'

const PAGE_SIZE = 10

export default function FrateRegistryPage() {
  const [pods, setPods] = useState<PodSummary[]>([])
  const [total, setTotal] = useState(0)
  const [query, setQuery] = useState('')
  const [license, setLicense] = useState('')
  const [licenses, setLicenses] = useState<string[]>([])
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    frateApi.getFacets().then(f => setLicenses(f.licenses)).catch(() => setLicenses([]))
  }, [])

  useEffect(() => {
    setLoading(true)
    const handle = setTimeout(() => {
      frateApi.browsePods(query, license, page, PAGE_SIZE)
        .then(res => { setPods(res.pods); setTotal(res.total) })
        .catch(() => { setPods([]); setTotal(0) })
        .finally(() => setLoading(false))
    }, 250)
    return () => clearTimeout(handle)
  }, [query, license, page])

  // Reset to page 1 whenever the search/filter criteria change, not when
  // just paging through an unchanged result set.
  useEffect(() => { setPage(1) }, [query, license])

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  return (
    <div style={{ maxWidth: 900, margin: '2rem auto', padding: '0 1rem' }}>
      <h1>Frate Pods</h1>
      <p style={{ color: 'var(--text-muted)' }}>
        The package registry for Frust -- browse published pods, their exports, and license.
        Install with <code>frate add &lt;name&gt;</code>. No sign-in required to browse or install.
      </p>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, margin: '16px 0' }}>
        <input
          className="admin-search-input"
          placeholder="Search pods…"
          value={query}
          onChange={e => setQuery(e.target.value)}
          style={{ flex: '1 1 240px' }}
        />
        <select
          className="admin-role-select"
          value={license}
          onChange={e => setLicense(e.target.value)}
          style={{ flex: '0 1 180px' }}
        >
          <option value="">All licenses</option>
          {licenses.map(l => (
            <option key={l} value={l}>{l}</option>
          ))}
        </select>
      </div>

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

      {!loading && total > PAGE_SIZE && (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 16, marginTop: 24 }}>
          <button className="post-action" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>← Prev</button>
          <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>Page {page} of {totalPages} ({total} pods)</span>
          <button className="post-action" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Next →</button>
        </div>
      )}
    </div>
  )
}
