import { useEffect, useRef, useState } from 'react'
import { forumApi } from '../../api/forumApi'
import type { ForumThread, ForumPost } from '../../api/forumApi'
import { useAuth } from '../../contexts/AuthContext'
import ForumEditor from '../../components/ForumEditor'

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
  const [replyHtml, setReplyHtml] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editHtml, setEditHtml] = useState('')
  const replyEditorKey = useRef(0)

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
    const text = replyHtml.replace(/<[^>]+>/g, '').trim()
    if (!text) return
    setSubmitting(true)
    try {
      const post = await forumApi.createPost(threadId, replyHtml)
      setPosts(prev => [...prev, post])
      setReplyHtml('')
      replyEditorKey.current += 1
    } finally {
      setSubmitting(false)
    }
  }

  const handleEdit = async (postId: string) => {
    const text = editHtml.replace(/<[^>]+>/g, '').trim()
    if (!text) return
    const updated = await forumApi.updatePost(postId, editHtml)
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
  if (!thread) return <div className="forum-error-msg">Thread not found.</div>

  const isThreadAuthor = user?.id === thread.authorId
  const isAdmin = user?.role === 'admin'
  const replyIsEmpty = !replyHtml.replace(/<[^>]+>/g, '').trim()

  return (
    <div className="community-page">
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
                  <ForumEditor
                    key={`edit-${post.id}`}
                    initialContent={post.content}
                    onChange={setEditHtml}
                    placeholder="Edit your post…"
                  />
                  <div className="post-edit-actions">
                    <button onClick={() => setEditingId(null)}>Cancel</button>
                    <button className="primary-action" onClick={() => handleEdit(post.id)}>Save</button>
                  </div>
                </div>
              ) : (
                <div
                  className="post-content tiptap-render"
                  dangerouslySetInnerHTML={{ __html: post.content }}
                />
              )}
            </div>
            <div className="post-footer">
              <span className="post-meta">
                {new Date(post.createdAt).toLocaleDateString()}
                {post.updatedAt !== post.createdAt && ' (edited)'}
              </span>
              <div className="post-actions">
                {user && (
                  <button className="post-action" onClick={() => handleVote(post.id)}>▲ {post.voteCount}</button>
                )}
                {isThreadAuthor && idx > 0 && !post.isAnswer && (
                  <button className="post-action" onClick={() => handleMarkAnswer(post.id)}>Mark Answer</button>
                )}
                {user?.id === post.authorId && !thread.isLocked && (
                  <button className="post-action" onClick={() => { setEditingId(post.id); setEditHtml(post.content) }}>Edit</button>
                )}
                {(user?.id === post.authorId || isAdmin) && (
                  <button className="post-action post-action-delete" onClick={() => handleDelete(post.id)}>Delete</button>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>

      {user && !thread.isLocked && (
        <form className="reply-form" onSubmit={handleReply}>
          <h3>Reply</h3>
          <ForumEditor
            key={replyEditorKey.current}
            placeholder="Write your reply…"
            onChange={setReplyHtml}
            minHeight={140}
          />
          <button type="submit" className="primary-action" disabled={submitting || replyIsEmpty}
            style={{ marginTop: 8 }}>
            {submitting ? 'Posting…' : 'Post Reply'}
          </button>
        </form>
      )}
      {!user && <p className="forum-login-prompt">Sign in to reply.</p>}
      {thread.isLocked && <p className="forum-locked-notice">This thread is locked.</p>}
    </div>
  )
}
