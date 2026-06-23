import { useEffect, useState } from 'react'
import { papersApi } from '../../api/papersApi'
import type { Paper } from '../../api/papersApi'
import { useAuth } from '../../contexts/AuthContext'

interface Props {
  onOpen: (paperId: string) => void
}

const STATUS_LABELS: Record<string, string> = {
  draft: 'Draft', in_progress: 'In Progress', review: 'Review',
  published: 'Published', archived: 'Archived',
}

export default function PapersListPage({ onOpen }: Props) {
  const { user } = useAuth()
  const [papers, setPapers] = useState<Paper[]>([])
  const [loading, setLoading] = useState(true)
  const [creating, setCreating] = useState(false)
  const [newTitle, setNewTitle] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!user) { setLoading(false); return }
    papersApi.list().then(setPapers).finally(() => setLoading(false))
  }, [user])

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newTitle.trim()) return
    setCreating(true)
    setError(null)
    try {
      const p = await papersApi.create(newTitle.trim(), '')
      setPapers(prev => [p, ...prev])
      setNewTitle('')
      onOpen(p.id)
    } catch {
      setError('Failed to create paper.')
    } finally {
      setCreating(false)
    }
  }

  const handleDelete = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation()
    if (!confirm('Delete this paper?')) return
    try {
      await papersApi.delete(id)
      setPapers(prev => prev.filter(p => p.id !== id))
    } catch {
      setError('Failed to delete paper.')
    }
  }

  if (!user) return <p className="forum-login-prompt">Sign in to use the Papers tool.</p>
  if (loading) return <div className="forum-loading">Loading…</div>

  return (
    <div className="community-page">
      <h1 className="community-page-title">My Papers</h1>

      <form className="papers-new-form" onSubmit={handleCreate}>
        <input className="papers-new-input" value={newTitle} onChange={e => setNewTitle(e.target.value)}
          placeholder="New paper title…" maxLength={200} />
        <button type="submit" className="primary-action" disabled={creating || !newTitle.trim()}>
          {creating ? 'Creating…' : 'New Paper'}
        </button>
      </form>

      {error && <p className="auth-error">{error}</p>}

      {papers.length === 0 ? (
        <p className="forum-empty">No papers yet. Start one above.</p>
      ) : (
        <div className="papers-grid">
          {papers.map(p => (
            <div key={p.id} className="paper-card" onClick={() => onOpen(p.id)}>
              <div className="paper-card-header">
                <h3 className="paper-card-title">{p.title}</h3>
                <span className={`blog-status-badge blog-status-${p.status}`}>
                  {STATUS_LABELS[p.status] ?? p.status}
                </span>
              </div>
              {p.abstract && <p className="paper-card-abstract">{p.abstract}</p>}
              <div className="paper-card-footer">
                <span className="post-meta">{new Date(p.updatedAt).toLocaleDateString()}</span>
                <button className="post-action post-action-delete" onClick={e => handleDelete(p.id, e)}>Delete</button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
