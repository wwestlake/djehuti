import { useEffect, useState } from 'react'
import { forumApi, ForumForum, ForumThread } from '../components/forum/forumApi'
import { useAuth } from '../contexts/AuthContext'
import NewThreadModal from '../components/forum/NewThreadModal'

interface Props {
  forumId: string
  onNavigateThread: (threadId: string) => void
  onNavigateHome: () => void
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

  const handleThreadCreated = (t: ForumThread) => {
    setThreads(prev => [t, ...prev])
    setShowNewThread(false)
  }

  if (loading) return <div className="forum-loading">Loading…</div>

  return (
    <div className="forum-page">
      <div className="forum-breadcrumb">
        <button className="breadcrumb-link" onClick={onNavigateHome}>Forum</button>
        {' › '}
        <span>{forum?.name ?? 'Forum'}</span>
      </div>
      <div className="forum-forum-header">
        <h1>{forum?.name ?? 'Forum'}</h1>
        {user && (
          <button className="btn-primary" onClick={() => setShowNewThread(true)}>
            New Thread
          </button>
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
          onCreated={handleThreadCreated}
          onClose={() => setShowNewThread(false)}
        />
      )}
    </div>
  )
}
