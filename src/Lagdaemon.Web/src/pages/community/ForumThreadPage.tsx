import { useEffect, useRef, useState } from 'react'
import { forumApi } from '../../api/forumApi'
import type { ForumThread, ForumPost, Subscription } from '../../api/forumApi'
import ReportModal from '../../components/forum/ReportModal'
import { useAuth } from '../../contexts/AuthContext'
import ForumEditor from '../../components/ForumEditor'

interface Props {
  threadId: string
  onNavigateHome: () => void
  onNavigateForum: (forumId: string) => void
}

type Reaction = { emoji: string; count: number; userReacted: boolean }

const QUICK_EMOJIS = ['👍', '❤️', '😂', '🔥', '👀', '✅']

function ReactionBar({ postId, user }: { postId: string; user: { id: string } | null }) {
  const [reactions, setReactions] = useState<Reaction[]>([])
  const [showPicker, setShowPicker] = useState(false)

  useEffect(() => {
    forumApi.getReactions(postId).then(setReactions)
  }, [postId])

  const toggle = async (emoji: string) => {
    if (!user) return
    const result = await forumApi.toggleReaction(postId, emoji)
    setReactions(prev => {
      const existing = prev.find(r => r.emoji === emoji)
      if (existing) {
        const newCount = result.added ? existing.count + 1 : existing.count - 1
        if (newCount <= 0) return prev.filter(r => r.emoji !== emoji)
        return prev.map(r => r.emoji === emoji ? { ...r, count: newCount, userReacted: result.added } : r)
      }
      return result.added ? [...prev, { emoji, count: 1, userReacted: true }] : prev
    })
    setShowPicker(false)
  }

  return (
    <div className="reaction-bar">
      {reactions.map(r => (
        <button key={r.emoji}
          className={`reaction-chip${r.userReacted ? ' reacted' : ''}`}
          onClick={() => toggle(r.emoji)}
          disabled={!user}>
          {r.emoji} <span>{r.count}</span>
        </button>
      ))}
      {user && (
        <div className="reaction-picker-wrap">
          <button className="reaction-add-btn" onClick={() => setShowPicker(v => !v)}>+</button>
          {showPicker && (
            <div className="reaction-picker">
              {QUICK_EMOJIS.map(e => (
                <button key={e} className="reaction-picker-emoji" onClick={() => toggle(e)}>{e}</button>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
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
  const [reportTarget, setReportTarget] = useState<{ type: 'post' | 'thread'; id: string } | null>(null)
  const [subscription, setSubscription] = useState<Subscription | null>(null)
  const replyEditorKey = useRef(0)

  useEffect(() => {
    const loads: Promise<unknown>[] = [
      forumApi.getThread(threadId),
      forumApi.getPosts(threadId),
    ]
    if (user) loads.push(forumApi.getThreadSubscription(threadId))
    Promise.all(loads).then(([t, p, sub]) => {
      setThread(t as ForumThread)
      setPosts(p as ForumPost[])
      if (sub !== undefined) setSubscription(sub as Subscription | null)
    }).finally(() => setLoading(false))
  }, [threadId, user])

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

  const handleSubscribe = async (level: string) => {
    const sub = await forumApi.subscribeThread(threadId, level)
    setSubscription(sub)
  }

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
        {user && (
          <div className="thread-subscribe">
            <select
              className="subscribe-select"
              value={subscription?.level ?? ''}
              onChange={e => handleSubscribe(e.target.value)}
              aria-label="Subscribe to thread"
            >
              <option value="" disabled>Watch thread…</option>
              <option value="watching">Watching</option>
              <option value="tracking">Tracking</option>
              <option value="muted">Muted</option>
            </select>
          </div>
        )}
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
          <div key={post.id} className={`post-item${post.isAnswer ? ' post-answer' : ''}${post.isBot ? ' post-bot' : ''}`}>
            {post.isAnswer && <div className="post-answer-badge">✓ Accepted Answer</div>}
            {post.isBot && <div className="post-bot-badge">🤖 AI Persona</div>}
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
            <ReactionBar postId={post.id} user={user} />
            <div className="post-footer">
              <span className="post-meta">
                {new Date(post.createdAt).toLocaleDateString()}
                {post.updatedAt !== post.createdAt && ' (edited)'}
              </span>
              <div className="post-actions">
                {user && !post.isBot && (
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
                {user && user.id !== post.authorId && !post.isBot && (
                  <button className="post-action post-action-report" onClick={() => setReportTarget({ type: 'post', id: post.id })}>Report</button>
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
      {reportTarget && (
        <ReportModal
          targetType={reportTarget.type}
          targetId={reportTarget.id}
          onClose={() => setReportTarget(null)}
        />
      )}
    </div>
  )
}
