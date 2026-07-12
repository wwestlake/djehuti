import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'

const BASE = '/djehuti'

interface Product {
  id: string
  slug: string
  name: string
  description: string | null
  requiredTierId: string | null
}

export default function DownloadsPage() {
  const [products, setProducts] = useState<Product[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch(`${BASE}/api/products`)
      .then(res => res.json())
      .then(setProducts)
      .catch(() => setProducts([]))
      .finally(() => setLoading(false))
  }, [])

  return (
    <div style={{ maxWidth: 900, margin: '2rem auto', padding: '0 1rem' }}>
      <h1>Downloads</h1>
      <p style={{ color: 'var(--text-muted)' }}>Desktop apps built on the Djehuti/LagDaemon ecosystem.</p>

      {loading && <p style={{ color: 'var(--text-muted)' }}>Loading…</p>}
      {!loading && products.length === 0 && <p style={{ color: 'var(--text-muted)' }}>No downloads published yet.</p>}

      <div style={{ display: 'grid', gap: 16, marginTop: 24 }}>
        {products.map(p => (
          <Link key={p.id} to={`/downloads/${p.slug}`} style={{
            display: 'block', textDecoration: 'none', color: 'inherit',
            background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius)',
            padding: '1.25rem 1.5rem',
          }}>
            <h3 style={{ margin: '0 0 4px' }}>{p.name}</h3>
            {p.description && <p style={{ margin: 0, color: 'var(--text-muted)', fontSize: '0.9rem' }}>{p.description}</p>}
          </Link>
        ))}
      </div>
    </div>
  )
}
