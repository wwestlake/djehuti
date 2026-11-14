import { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { frateApi } from '../../api/frateApi'
import type { PodDetail } from '../../api/frateApi'
import ShareButton from '../../components/share/ShareButton'
import { usePageSeo } from '../../components/share/usePageSeo'

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export default function FratePodDetailPage() {
  const { name } = useParams<{ name: string }>()
  const [pod, setPod] = useState<PodDetail | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!name) return
    setLoading(true)
    frateApi.getPod(name).then(setPod).finally(() => setLoading(false))
  }, [name])

  usePageSeo({
    title: pod ? `${pod.name} — Frate Pods — Lag Daemon` : 'Frate Pod — Lag Daemon',
    description: pod?.description || undefined,
  })

  if (loading) return <div style={{ maxWidth: 700, margin: '2rem auto', padding: '0 1rem' }}><p style={{ color: 'var(--text-muted)' }}>Loading…</p></div>
  if (!pod) return (
    <div style={{ maxWidth: 700, margin: '2rem auto', padding: '0 1rem' }}>
      <p style={{ color: 'var(--text-muted)' }}>No such pod.</p>
      <Link to="/frate">← Back to Frate Pods</Link>
    </div>
  )

  return (
    <div style={{ maxWidth: 700, margin: '2rem auto', padding: '0 1rem' }}>
      <Link to="/frate" style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>← Back to Frate Pods</Link>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 8 }}>
        <h1 style={{ marginBottom: 4 }}>{pod.name}</h1>
        <ShareButton title={`${pod.name} — Frate Pods — Lag Daemon`} text={pod.description} />
      </div>
      <p style={{ color: 'var(--text-muted)' }}>{pod.description}</p>
      <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>License: {pod.license}</p>

      <h3 style={{ marginTop: 28 }}>Versions</h3>
      <div style={{ display: 'grid', gap: 8 }}>
        {pod.versions.map(v => (
          <div key={v.version} style={{
            display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap',
            padding: '0.75rem 1rem', borderRadius: 'var(--radius)',
            background: 'var(--surface)', border: '1px solid var(--border)',
            opacity: v.yanked ? 0.55 : 1,
          }}>
            <div>
              <code>{v.version}</code>
              {v.yanked && <span style={{ marginLeft: 8, fontSize: '0.75rem', color: 'var(--danger, #e5484d)' }}>yanked</span>}
              {v.description && <span style={{ marginLeft: 8, fontSize: '0.85rem', color: 'var(--text-muted)' }}>{v.description}</span>}
            </div>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
              {formatBytes(v.sizeBytes)} · {new Date(v.createdAt).toLocaleDateString()}
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}
