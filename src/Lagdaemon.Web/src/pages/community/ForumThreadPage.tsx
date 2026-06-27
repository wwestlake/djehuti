import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { forumApi } from '../../api/forumApi'
import type { ForumThread, ForumPost, ForumCategory, ForumForum, Subscription, PollData } from '../../api/forumApi'
import ReportModal from '../../components/forum/ReportModal'
import PollWidget from '../../components/forum/PollWidget'
import { useAuth } from '../../contexts/AuthContext'
import ForumEditor from '../../components/ForumEditor'

type Reaction = { emoji: string; count: number; userReacted: boolean }

const QUICK_EMOJIS = ['👍', '❤️', '😂', '🔥', '👀', '✅']
const THREAD_REF_RE = /#([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/gi

function renderPostContent(html: string, _onThreadNav: (id: string) => void) {
  // Replace #UUID cross-references with clickable anchors (server-side safe: only replaces text nodes conceptually)
  const withRefs = html.replace(THREAD_REF_RE, (_, id) =>
    `<a class="thread-xref" data-thread-id="${id}" href="#">#${id.slice(0, 8)}…</a>`
  )
  return withRefs
}

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

type ModAction = 'move' | 'split' | 'merge' | null

export default function ForumThreadPage() {
  const { threadId = '' } = useParams<{ threadId: string }>()
  const navigate = useNavigate()
  const onNavigateHome = () => navigate('/forum')
  const onNavigateForum = (forumId: string) => navigate('/forum/' + forumId)
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
  const [poll, setPoll] = useState<PollData | null>(null)
  const replyEditorKey = useRef(0)

  // mod actions
  const [modAction, setModAction] = useState<ModAction>(null)
  const [allForums, setAllForums] = useState<{ forum: ForumForum; categoryName: string }[]>([])
  const [moveForumId, setMoveForumId] = useState('')
  const [splitPostIds, setSplitPostIds] = useState<Set<string>>(new Set())
  const [splitTitle, setSplitTitle] = useState('')
  const [mergeTargetId, setMergeTargetId] = useState('')
  const [modBusy, setModBusy] = useState(false)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  useEffect(() => {
    setErrorMsg(null)
    const loads: Promise<unknown>[] = [
      forumApi.getThread(threadId),
      forumApi.getPosts(threadId),
      forumApi.getPoll(threadId),
    ]
    if (user) loads.push(forumApi.getThreadSubscription(threadId))
    Promise.all(loads).then(([t, p, pollData, sub]) => {
      setThread(t as ForumThread | null)
      if (!t) setErrorMsg('Thread not found.')
      setPosts((p as ForumPost[]) ?? [])
      setPoll(pollData as PollData | null)
      if (sub !== undefined) setSubscription(sub as Subscription | null)
    }).catch(() => {
      setThread(prev => { if (!prev) setErrorMsg('Could not load thread.'); return prev })
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

  const openModAction = async (action: ModAction) => {
    if (action === 'move' && allForums.length === 0) {
      const cats: ForumCategory[] = await forumApi.getCategories()
      const forums = await Promise.all(cats.map(c => forumApi.getForums(c.id).then(fs => fs.map(f => ({ forum: f, categoryName: c.name })))))
      setAllForums(forums.flat())
    }
    setSplitPostIds(new Set())
    setSplitTitle('')
    setMergeTargetId('')
    setMoveForumId('')
    setModAction(action)
  }

  const handleMove = async () => {
    if (!thread || !moveForumId) return
    setModBusy(true)
    try {
      await forumApi.moveThread(thread.id, moveForumId)
      const target = allForums.find(f => f.forum.id === moveForumId)
      alert(`Thread moved to ${target?.forum.name ?? 'new forum'}.`)
      setModAction(null)
      onNavigateForum(moveForumId)
    } finally { setModBusy(false) }
  }

  const handleSplit = async () => {
    if (!thread || splitPostIds.size === 0 || !splitTitle.trim()) return
    setModBusy(true)
    try {
      const newThread = await forumApi.splitThread(thread.id, [...splitPostIds], splitTitle.trim())
      setPosts(prev => prev.filter(p => !splitPostIds.has(p.id)))
      setModAction(null)
      if (confirm(`Split complete. Go to new thread "${newThread.title}"?`)) {
        navigate('/forum/thread/' + newThread.id)
      }
    } finally { setModBusy(false) }
  }

  const handleMerge = async () => {
    if (!thread || !mergeTargetId.trim()) return
    if (!confirm('This will move all posts from this thread into the target thread and delete this thread. Continue?')) return
    setModBusy(true)
    try {
      await forumApi.mergeThread(thread.id, mergeTargetId.trim())
      setModAction(null)
      onNavigateForum(thread.forumId)
    } finally { setModBusy(false) }
  }

  if (loading) return <div className="forum-loading">Loading…</div>

  const isThreadAuthor = user?.id === thread?.authorId
  const isAdmin = user?.role === 'admin'
  const isMod = isAdmin || user?.role === 'moderator'
  const replyIsEmpty = !replyHtml.replace(/<[^>]+>/g, '').trim()

  const handleSubscribe = async (level: string) => {
    const sub = await forumApi.subscribeThread(threadId, level)
    setSubscription(sub)
  }

  return (
    <div className="community-page">
      {errorMsg && (
        <div className="forum-error-banner">
          <span>{errorMsg}</span>
          <button className="forum-error-dismiss" onClick={() => setErrorMsg(null)} aria-label="Dismiss">✕</button>
        </div>
      )}
      <div className="forum-breadcrumb">
        <button className="breadcrumb-link" onClick={onNavigateHome}>Forum</button>
        {' › '}
        <button className="breadcrumb-link" onClick={() => thread && onNavigateForum(thread.forumId)}>Back</button>
        {' › '}
        <span>{thread?.title}</span>
      </div>

      <div className="thread-header">
        <h1>{thread?.title}</h1>
        <div className="thread-header-badges">
          {thread?.isPinned && <span className="badge badge-pinned">Pinned</span>}
          {thread?.isLocked && <span className="badge badge-locked">Locked</span>}
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
        {isMod && (
          <div className="thread-mod-actions">
            <button onClick={() => thread && forumApi.pinThread(thread.id, !thread.isPinned).then(() => setThread(t => t ? { ...t, isPinned: !t.isPinned } : t))}>
              {thread?.isPinned ? 'Unpin' : 'Pin'}
            </button>
            <button onClick={() => thread && forumApi.lockThread(thread.id, !thread.isLocked).then(() => setThread(t => t ? { ...t, isLocked: !t.isLocked } : t))}>
              {thread?.isLocked ? 'Unlock' : 'Lock'}
            </button>
            <button onClick={() => openModAction('move')}>Move</button>
            <button onClick={() => openModAction('split')}>Split</button>
            <button onClick={() => openModAction('merge')}>Merge</button>
          </div>
        )}
      </div>

      {poll && (
        <PollWidget
          poll={poll}
          userId={user?.id}
          onRefresh={() => forumApi.getPoll(threadId).then(p => setPoll(p))}
        />
      )}

      <div className="post-list">
        {posts.map((post, idx) => (
          <div key={post.id} className={`post-item${post.isAnswer ? ' post-answer' : ''}${post.isBot ? ' post-bot' : ''}${modAction === 'split' && splitPostIds.has(post.id) ? ' post-split-selected' : ''}`}>
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
                  dangerouslySetInnerHTML={{ __html: renderPostContent(post.content, (id) => onNavigateForum(id)) }}
                  onClick={(e) => {
                    const a = (e.target as HTMLElement).closest('[data-thread-id]')
                    if (a) { e.preventDefault(); /* navigate to thread when we have a thread page handler */ }
                  }}
                />
              )}
            </div>
            <ReactionBar postId={post.id} user={user} />
            <div className="post-footer">
              <span className="post-meta">
                {new Date(post.createdAt).toLocaleDateString()}
                {post.updatedAt !== post.createdAt && ' (edited)'}
              </span>
              {modAction === 'split' && idx > 0 && (
                <label className="split-select-label">
                  <input type="checkbox" checked={splitPostIds.has(post.id)}
                    onChange={e => setSplitPostIds(prev => {
                      const next = new Set(prev)
                      e.target.checked ? next.add(post.id) : next.delete(post.id)
                      return next
                    })} />
                  {' '}Split to new thread
                </label>
              )}
              <div className="post-actions">
                {user && !post.isBot && (
                  <button className="post-action" onClick={() => handleVote(post.id)}>▲ {post.voteCount}</button>
                )}
                {isThreadAuthor && idx > 0 && !post.isAnswer && (
                  <button className="post-action" onClick={() => handleMarkAnswer(post.id)}>Mark Answer</button>
                )}
                {user?.id === post.authorId && !thread?.isLocked && (
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

      {user && !thread?.isLocked && (
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
      {thread?.isLocked && <p className="forum-locked-notice">This thread is locked.</p>}
      {reportTarget && (
        <ReportModal
          targetType={reportTarget.type}
          targetId={reportTarget.id}
          onClose={() => setReportTarget(null)}
        />
      )}

      {modAction === 'move' && (
        <div className="mod-modal-overlay">
          <div className="mod-modal">
            <h3>Move Thread</h3>
            <label>Target Forum
              <select value={moveForumId} onChange={e => setMoveForumId(e.target.value)}>
                <option value="">Select a forum…</option>
                {allForums.map(({ forum, categoryName }) => (
                  <option key={forum.id} value={forum.id}>{categoryName} › {forum.name}</option>
                ))}
              </select>
            </label>
            <div className="mod-modal-actions">
              <button onClick={() => setModAction(null)}>Cancel</button>
              <button className="primary-action" onClick={handleMove} disabled={!moveForumId || modBusy}>
                {modBusy ? 'Moving…' : 'Move'}
              </button>
            </div>
          </div>
        </div>
      )}

      {modAction === 'split' && (
        <div className="mod-modal-overlay">
          <div className="mod-modal">
            <h3>Split Thread</h3>
            <p>Check the posts above to move into the new thread, then enter a title.</p>
            <label>New Thread Title
              <input type="text" value={splitTitle} onChange={e => setSplitTitle(e.target.value)} placeholder="Title for new thread" />
            </label>
            <p className="mod-modal-hint">{splitPostIds.size} post{splitPostIds.size !== 1 ? 's' : ''} selected</p>
            <div className="mod-modal-actions">
              <button onClick={() => setModAction(null)}>Cancel</button>
              <button className="primary-action" onClick={handleSplit}
                disabled={splitPostIds.size === 0 || !splitTitle.trim() || modBusy}>
                {modBusy ? 'Splitting…' : 'Split'}
              </button>
            </div>
          </div>
        </div>
      )}

      {modAction === 'merge' && (
        <div className="mod-modal-overlay">
          <div className="mod-modal">
            <h3>Merge Into Another Thread</h3>
            <p>All posts from this thread will be moved into the target thread, and this thread will be deleted.</p>
            <label>Target Thread ID
              <input type="text" value={mergeTargetId} onChange={e => setMergeTargetId(e.target.value)}
                placeholder="Paste target thread UUID" />
            </label>
            <div className="mod-modal-actions">
              <button onClick={() => setModAction(null)}>Cancel</button>
              <button className="primary-action" onClick={handleMerge}
                disabled={!mergeTargetId.trim() || modBusy}>
                {modBusy ? 'Merging…' : 'Merge'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
