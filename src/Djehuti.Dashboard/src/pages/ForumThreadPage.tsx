import { useEffect, useState } from 'react'
import ReactMarkdown from 'react-markdown'
import { forumApi, ForumThread, ForumPost } from '../components/forum/forumApi'
import { useAuth } from '../contexts/AuthContext'

interface Props {
  threadId: string
  onNavigateHome: () => void
  onNavigateForum: (forumId: string) => void
}

export default function ForumThreadPage({ threadId, onNavigateHome, onNavigateForum }: Props) {
  const { user } = useAuth()
  const [thread, setThread] = useState<ForumThread | null>(null)
  const [posts, setPosts] = useState<ForumPost[]>([])
  const [loading, setLoading] = useState(true)
  const [replyContent, setReplyContent] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editContent, setEditContent] = useState('')

  useEffect(() => {
    Promise.all([
      forumApi.getThread(threadId),
      forumApi.getPosts(threadId),
    ]).then(([t, p]) => {
      setThread(t)
      setPosts(p)
    }).finally(() => setLoading(false))
  }, [threadId])

  const handleReply = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!replyContent.trim()) return
    setSubmitting(true)
    try {
      const post = await forumApi.createPost(threadId, replyContent.trim())
      setPosts(prev => [...prev, post])
      setReplyContent('')
    } finally {
      setSubmitting(false)
    }
  }

  const handleEdit = async (postId: string) => {
    if (!editContent.trim()) return
    const updated = await forumApi.updatePost(postId, editContent.trim())
    setPosts(prev => prev.map(p => p.id === postId ? updated : p))
    setEditingId(null)
  }

  const handleDelete = async (postId: string) => {
    if (!confirm('Delete this post?')) return
    await forumApi.deletePost(postId)
    setPosts(prev => prev.filter(p => p.id !== postId))
  }

  const handleVote = async (postId: string) => {
    const result = await forumApi.votePost(postId)
    if (result.voted) {
      setPosts(prev => prev.map(p => p.id === postId ? { ...p, voteCount: p.voteCount + 1 } : p))
    }
  }

  const handleMarkAnswer = async (postId: string) => {
    if (!thread) return
    await forumApi.markAnswer(postId)
    setPosts(prev => prev.map(p => ({ ...p, isAnswer: p.id === postId })))
  }

  if (loading) return <div className="forum-loading">Loading…</div>
  if (!thread) return <div className="forum-error">Thread not found.</div>

  const isThreadAuthor = user?.id === thread.authorId
  const isAdmin = user?.role === 'admin'

  return (
    <div className="forum-page">
      <div className="forum-breadcrumb">
        <button className="breadcrumb-link" onClick={onNavigateHome}>Forum</button>
        {' › '}
        <button className="breadcrumb-link" onClick={() => onNavigateForum(thread.forumId)}>Back</button>
        {' › '}
        <span>{thread.title}</span>
      </div>

      <div className="thread-header">
        <h1>{thread.title}</h1>
        <div className="thread-header-badges">
          {thread.isPinned && <span className="badge badge-pinned">Pinned</span>}
          {thread.isLocked && <span className="badge badge-locked">Locked</span>}
        </div>
        {isAdmin && (
          <div className="thread-mod-actions">
            <button onClick={() => forumApi.pinThread(thread.id, !thread.isPinned).then(() => setThread(t => t ? { ...t, isPinned: !t.isPinned } : t))}>
              {thread.isPinned ? 'Unpin' : 'Pin'}
            </button>
            <button onClick={() => forumApi.lockThread(thread.id, !thread.isLocked).then(() => setThread(t => t ? { ...t, isLocked: !t.isLocked } : t))}>
              {thread.isLocked ? 'Unlock' : 'Lock'}
            </button>
          </div>
        )}
      </div>

      <div className="post-list">
        {posts.map((post, idx) => (
          <div key={post.id} className={`post-item${post.isAnswer ? ' post-answer' : ''}`}>
            {post.isAnswer && <div className="post-answer-badge">✓ Accepted Answer</div>}
            <div className="post-body">
              {editingId === post.id ? (
                <div className="post-edit">
                  <textarea value={editContent} onChange={e => setEditContent(e.target.value)} rows={6} />
                  <div className="post-edit-actions">
                    <button onClick={() => setEditingId(null)}>Cancel</button>
                    <button className="btn-primary" onClick={() => handleEdit(post.id)}>Save</button>
                  </div>
                </div>
              ) : (
                <div className="post-content">
                  <ReactMarkdown>{post.content}</ReactMarkdown>
                </div>
              )}
            </div>
            <div className="post-footer">
              <span className="post-meta">
                {new Date(post.createdAt).toLocaleDateString()}
                {post.updatedAt !== post.createdAt && ' (edited)'}
              </span>
              <div className="post-actions">
                {user && (
                  <button className="post-action" onClick={() => handleVote(post.id)}>
                    ▲ {post.voteCount}
                  </button>
                )}
                {isThreadAuthor && idx > 0 && !post.isAnswer && (
                  <button className="post-action" onClick={() => handleMarkAnswer(post.id)}>
                    Mark Answer
                  </button>
                )}
                {user?.id === post.authorId && !thread.isLocked && (
                  <button className="post-action" onClick={() => { setEditingId(post.id); setEditContent(post.content) }}>
                    Edit
                  </button>
                )}
                {(user?.id === post.authorId || isAdmin) && (
                  <button className="post-action post-action-delete" onClick={() => handleDelete(post.id)}>
                    Delete
                  </button>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>

      {user && !thread.isLocked && (
        <form className="reply-form" onSubmit={handleReply}>
          <h3>Reply</h3>
          <textarea
            value={replyContent}
            onChange={e => setReplyContent(e.target.value)}
            placeholder="Write your reply (Markdown supported)"
            rows={6}
            required
          />
          <button type="submit" className="btn-primary" disabled={submitting || !replyContent.trim()}>
            {submitting ? 'Posting…' : 'Post Reply'}
          </button>
        </form>
      )}

      {!user && <p className="forum-login-prompt">Sign in to reply.</p>}
      {thread.isLocked && <p className="forum-locked-notice">This thread is locked. No new replies.</p>}
    </div>
  )
}
