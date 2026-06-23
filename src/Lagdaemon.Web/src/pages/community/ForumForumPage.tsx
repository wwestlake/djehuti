import { useEffect, useState } from 'react'
import { forumApi } from '../../api/forumApi'
import type { ForumForum, ForumThread } from '../../api/forumApi'
import { useAuth } from '../../contexts/AuthContext'

interface Props {
  forumId: string
  onNavigateThread: (threadId: string) => void
  onNavigateHome: () => void
}

function NewThreadModal({ forumId, onCreated, onClose }: {
  forumId: string
  onCreated: (t: ForumThread) => void
  onClose: () => void
}) {
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!title.trim() || !content.trim()) return
    setSubmitting(true)
    setError(null)
    try {
      const thread = await forumApi.createThread(forumId, title.trim(), content.trim())
      onCreated(thread)
    } catch {
      setError('Failed to create thread.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="modal-overlay open" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 560 }}>
        <button className="modal-close" onClick={onClose}>✕</button>
        <h2 style={{ marginBottom: '1.2rem' }}>New Thread</h2>
        <form onSubmit={handleSubmit} className="auth-form">
          <label>
            <span>Title</span>
            <input type="text" value={title} onChange={e => setTitle(e.target.value)}
              placeholder="Thread title" required maxLength={200} />
          </label>
          <label>
            <span>Post</span>
            <textarea value={content} onChange={e => setContent(e.target.value)}
              placeholder="Write your post (Markdown supported)" required rows={8}
              style={{ resize: 'vertical', fontFamily: 'inherit' }} />
          </label>
          {error && <p className="auth-error">{error}</p>}
          <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
            <button type="button" onClick={onClose} disabled={submitting}>Cancel</button>
            <button type="submit" className="primary-action" disabled={submitting || !title.trim() || !content.trim()}>
              {submitting ? 'Posting…' : 'Post Thread'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export default function ForumForumPage({ forumId, onNavigateThread, onNavigateHome }: Props) {
  const { user } = useAuth()
  const [forum, setForum] = useState<ForumForum | null>(null)
  const [threads, setThreads] = useState<ForumThread[]>([])
  const [loading, setLoading] = useState(true)
  const [showNewThread, setShowNewThread] = useState(false)

  useEffect(() => {
    Promise.all([
      forumApi.getForumById(forumId),
      forumApi.getThreads(forumId),
    ]).then(([f, t]) => {
      setForum(f)
      setThreads(t)
    }).finally(() => setLoading(false))
  }, [forumId])

  if (loading) return <div className="forum-loading">Loading…</div>

  return (
    <div className="community-page">
      <div className="forum-breadcrumb">
        <button className="breadcrumb-link" onClick={onNavigateHome}>Forum</button>
        {' › '}
        <span>{forum?.name ?? 'Forum'}</span>
      </div>
      <div className="forum-forum-header">
        <h1>{forum?.name ?? 'Forum'}</h1>
        {user && (
          <button className="primary-action" onClick={() => setShowNewThread(true)}>New Thread</button>
        )}
      </div>
      <div className="thread-list">
        {threads.length === 0
          ? <p className="forum-empty">No threads yet. Be the first to post!</p>
          : threads.map(t => (
              <button key={t.id} className="thread-list-item" onClick={() => onNavigateThread(t.id)}>
                <div className="thread-list-item-badges">
                  {t.isPinned && <span className="badge badge-pinned">Pinned</span>}
                  {t.isLocked && <span className="badge badge-locked">Locked</span>}
                </div>
                <div className="thread-list-item-info">
                  <span className="thread-title">{t.title}</span>
                  <span className="thread-meta">
                    {t.postCount} {t.postCount === 1 ? 'reply' : 'replies'} · {t.viewCount} views
                  </span>
                </div>
              </button>
            ))
        }
      </div>
      {showNewThread && (
        <NewThreadModal
          forumId={forumId}
          onCreated={t => { setThreads(prev => [t, ...prev]); setShowNewThread(false) }}
          onClose={() => setShowNewThread(false)}
        />
      )}
    </div>
  )
}
