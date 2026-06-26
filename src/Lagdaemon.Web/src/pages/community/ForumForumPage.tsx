import { useEffect, useState } from 'react'
import { forumApi } from '../../api/forumApi'
import type { ForumForum, ForumThread, ForumTag } from '../../api/forumApi'
import { useAuth } from '../../contexts/AuthContext'

interface Props {
  forumId: string
  onNavigateThread: (threadId: string) => void
  onNavigateHome: () => void
}

function NewThreadModal({ forumId, allTags, onCreated, onClose }: {
  forumId: string
  allTags: ForumTag[]
  onCreated: (t: ForumThread) => void
  onClose: () => void
}) {
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [selectedTagIds, setSelectedTagIds] = useState<string[]>([])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const toggleTag = (id: string) =>
    setSelectedTagIds(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!title.trim() || !content.trim()) return
    setSubmitting(true)
    setError(null)
    try {
      const thread = await forumApi.createThread(forumId, title.trim(), content.trim())
      if (selectedTagIds.length > 0) {
        await forumApi.setThreadTags(thread.id, selectedTagIds)
      }
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
          {allTags.length > 0 && (
            <div>
              <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)', display: 'block', marginBottom: 6 }}>Tags</span>
              <div className="forum-tag-picker">
                {allTags.map(tag => (
                  <button key={tag.id} type="button"
                    className={`forum-tag-chip ${selectedTagIds.includes(tag.id) ? 'selected' : ''}`}
                    onClick={() => toggleTag(tag.id)}>
                    {tag.name}
                  </button>
                ))}
              </div>
            </div>
          )}
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
  const [allTags, setAllTags] = useState<ForumTag[]>([])
  const [threadTags, setThreadTags] = useState<Record<string, ForumTag[]>>({})
  const [filterTagId, setFilterTagId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [showNewThread, setShowNewThread] = useState(false)

  useEffect(() => {
    Promise.all([
      forumApi.getForumById(forumId),
      forumApi.getThreads(forumId),
      forumApi.getTags(),
    ]).then(([f, t, tags]) => {
      setForum(f)
      setThreads(t)
      setAllTags(tags)
      // fetch tags for all threads
      Promise.all(t.map(th => forumApi.getThreadTags(th.id).then(tgs => ({ id: th.id, tags: tgs }))))
        .then(results => {
          const map: Record<string, ForumTag[]> = {}
          results.forEach(r => { map[r.id] = r.tags })
          setThreadTags(map)
        })
    }).finally(() => setLoading(false))
  }, [forumId])

  const filteredThreads = filterTagId
    ? threads.filter(t => (threadTags[t.id] ?? []).some(tg => tg.id === filterTagId))
    : threads

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

      {allTags.length > 0 && (
        <div className="forum-tag-filter">
          <button
            className={`forum-tag-chip ${filterTagId === null ? 'selected' : ''}`}
            onClick={() => setFilterTagId(null)}>
            All
          </button>
          {allTags.map(tag => (
            <button key={tag.id}
              className={`forum-tag-chip ${filterTagId === tag.id ? 'selected' : ''}`}
              onClick={() => setFilterTagId(filterTagId === tag.id ? null : tag.id)}>
              {tag.name}
            </button>
          ))}
        </div>
      )}

      <div className="thread-list">
        {filteredThreads.length === 0
          ? <p className="forum-empty">No threads yet. Be the first to post!</p>
          : filteredThreads.map(t => (
              <button key={t.id} className="thread-list-item" onClick={() => onNavigateThread(t.id)}>
                <div className="thread-list-item-badges">
                  {t.isPinned && <span className="badge badge-pinned">Pinned</span>}
                  {t.isLocked && <span className="badge badge-locked">Locked</span>}
                </div>
                <div className="thread-list-item-info">
                  <span className="thread-title">{t.title}</span>
                  {(threadTags[t.id] ?? []).length > 0 && (
                    <div className="forum-tag-row">
                      {(threadTags[t.id] ?? []).map(tg => (
                        <span key={tg.id} className="forum-tag-chip small">{tg.name}</span>
                      ))}
                    </div>
                  )}
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
          allTags={allTags}
          onCreated={t => { setThreads(prev => [t, ...prev]); setShowNewThread(false) }}
          onClose={() => setShowNewThread(false)}
        />
      )}
    </div>
  )
}
