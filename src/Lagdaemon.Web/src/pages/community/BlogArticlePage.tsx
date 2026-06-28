import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Link2, Twitter, Linkedin } from 'lucide-react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { blogApi } from '../../api/blogApi'
import type { BlogArticle, BlogComment, BlogTag } from '../../api/blogApi'
import { useAuth } from '../../contexts/AuthContext'


interface TocEntry { id: string; text: string; level: number }

function buildTocFromMarkdown(md: string): TocEntry[] {
  const entries: TocEntry[] = []
  let i = 0
  for (const line of md.split('\n')) {
    const m = line.match(/^(#{1,3})\s+(.+)/)
    if (m) entries.push({ id: `heading-${i++}`, text: m[2].trim(), level: m[1].length })
  }
  return entries
}

let headingCounter = 0
function makeHeadingRenderer(level: 1 | 2 | 3) {
  const Tag = `h${level}` as 'h1' | 'h2' | 'h3'
  return ({ children }: { children?: React.ReactNode }) => (
    <Tag id={`heading-${headingCounter++}`}>{children}</Tag>
  )
}

function readingTime(content: string) {
  const words = (content || '').trim().split(/\s+/).length
  return `${Math.max(1, Math.round(words / 200))} min read`
}

export default function BlogArticlePage() {
  const { slug = '' } = useParams<{ slug: string }>()
  const navigate = useNavigate()
  const onNavigateBack = () => navigate('/blog')
  const onNavigateEditor = (articleId: string) => navigate('/blog/editor/' + articleId)
  const { user } = useAuth()
  const [article, setArticle] = useState<BlogArticle | null>(null)
  const [tags, setTags] = useState<BlogTag[]>([])
  const [comments, setComments] = useState<BlogComment[]>([])
  const [loading, setLoading] = useState(true)
  const [commentText, setCommentText] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [toc, setToc] = useState<TocEntry[]>([])
  const [activeToc, setActiveToc] = useState('')
  const [progress, setProgress] = useState(0)
  const [copied, setCopied] = useState(false)
  const [moderating, setModerating] = useState(false)
  const [modNote, setModNote] = useState('')
  const [showModNote, setShowModNote] = useState<'reject' | 'revision' | null>(null)
  const contentRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    blogApi.getArticle(slug)
      .then(a => {
        setArticle(a)
        setToc(buildTocFromMarkdown(a.content || ''))
        return Promise.all([blogApi.getComments(a.id), blogApi.getArticleTags(a.id)])
      })
      .then(([c, t]) => { setComments(c); setTags(t) })
      .finally(() => setLoading(false))
  }, [slug])

  // Reading progress
  useEffect(() => {
    const onScroll = () => {
      const el = contentRef.current
      if (!el) return
      const rect = el.getBoundingClientRect()
      const total = el.offsetHeight
      const read = Math.max(0, -rect.top)
      setProgress(Math.min(100, Math.round((read / total) * 100)))

      // Active ToC heading
      const headings = el.querySelectorAll('h1[id],h2[id],h3[id]')
      let active = ''
      headings.forEach(h => {
        if (h.getBoundingClientRect().top < 120) active = h.id
      })
      setActiveToc(active)
    }
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [toc])

  const handleComment = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!article || !commentText.trim()) return
    setSubmitting(true)
    try {
      const c = await blogApi.createComment(article.id, commentText.trim())
      setComments(prev => [...prev, c])
      setCommentText('')
    } finally { setSubmitting(false) }
  }

  const handleDeleteComment = async (id: string) => {
    if (!confirm('Delete comment?')) return
    await blogApi.deleteComment(id)
    setComments(prev => prev.filter(c => c.id !== id))
  }

  const handleStatus = async (status: string, note?: string) => {
    if (!article) return
    setModerating(true)
    try {
      const updated = await blogApi.setStatus(article.id, status, note)
      setArticle(updated)
      setShowModNote(null)
      setModNote('')
    } finally { setModerating(false) }
  }

  const copyLink = () => {
    navigator.clipboard.writeText(window.location.href)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  const shareTwitter = () => {
    const text = encodeURIComponent(`${article?.title} — ${window.location.href}`)
    window.open(`https://twitter.com/intent/tweet?text=${text}`, '_blank', 'noopener')
  }

  const shareLinkedIn = () => {
    const url = encodeURIComponent(window.location.href)
    window.open(`https://www.linkedin.com/sharing/share-offsite/?url=${url}`, '_blank', 'noopener')
  }

  if (loading) return <div className="forum-loading">Loading…</div>
  if (!article) return <div className="forum-error-msg">Article not found.</div>

  const isAuthor = user?.id === article.authorId
  const isAdmin = user?.role === 'admin'
  headingCounter = 0

  return (
    <div className="blog-reader-shell">
      {/* Reading progress bar */}
      <div className="blog-progress-bar" style={{ width: `${progress}%` }} />

      <div className="blog-reader-layout">
        {/* ToC sidebar */}
        {toc.length > 2 && (
          <nav className="blog-toc">
            <div className="blog-toc-label">Contents</div>
            {toc.map(entry => (
              <a key={entry.id} href={`#${entry.id}`}
                className={`blog-toc-link blog-toc-h${entry.level}${activeToc === entry.id ? ' active' : ''}`}>
                {entry.text}
              </a>
            ))}
          </nav>
        )}

        {/* Main content */}
        <article className="blog-reader-main">
          <div className="forum-breadcrumb">
            <button className="breadcrumb-link" onClick={onNavigateBack}>Articles</button>
            {' › '}<span>{article.title}</span>
          </div>

          {article.coverUrl && (
            <img src={article.coverUrl} alt={article.title} className="blog-article-cover" />
          )}

          <div className="blog-reader-header">
            <h1 className="blog-reader-title">{article.title}</h1>
            {article.subtitle && <p className="blog-reader-subtitle">{article.subtitle}</p>}
            <div className="blog-article-meta">
              {article.publishedAt && (
                <span>{new Date(article.publishedAt).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })}</span>
              )}
              <span>{readingTime(article.content)}</span>
              {tags.length > 0 && (
                <span className="blog-reader-tags">
                  {tags.map(t => <span key={t.id} className="blog-tag-chip-sm">{t.name}</span>)}
                </span>
              )}
            </div>

            <div className="blog-article-byline">
              By <a className="blog-author-link" href={`/profile/${article.authorId}`}>{article.authorName || 'Anonymous'}</a>
            </div>

            {/* Share */}
            <div className="blog-share-row">
              <span className="blog-share-label">Share</span>
              <button className="blog-share-btn" onClick={copyLink} title="Copy link">
                <Link2 size={15} /> {copied ? 'Copied!' : 'Copy link'}
              </button>
              <button className="blog-share-btn" onClick={shareTwitter} title="Share on X / Twitter">
                <Twitter size={15} /> X
              </button>
              <button className="blog-share-btn" onClick={shareLinkedIn} title="Share on LinkedIn">
                <Linkedin size={15} /> LinkedIn
              </button>
            </div>

            {/* Author / admin actions */}
            {(isAuthor || isAdmin) && (
              <div className="blog-article-actions">
                {(isAuthor || isAdmin) && article.status !== 'published' && (
                  <button className="tiptap-action-btn secondary" onClick={() => onNavigateEditor(article.id)}>Edit</button>
                )}
                {isAuthor && article.status === 'draft' && (
                  <button className="tiptap-action-btn primary" onClick={() => handleStatus('submitted')}>
                    Submit for Review
                  </button>
                )}
                {isAdmin && (article.status === 'submitted' || article.status === 'under_review') && (
                  <>
                    <button className="tiptap-action-btn primary" disabled={moderating}
                      onClick={() => handleStatus('approved')}>Approve</button>
                    <button className="tiptap-action-btn primary" disabled={moderating}
                      onClick={() => handleStatus('published')}>Publish</button>
                    <button className="tiptap-action-btn secondary" disabled={moderating}
                      onClick={() => setShowModNote('revision')}>Request Revision</button>
                    <button className="tiptap-action-btn secondary" disabled={moderating}
                      onClick={() => setShowModNote('reject')}>Reject</button>
                  </>
                )}
                {article.status !== 'draft' && (
                  <span className={`blog-status-badge ${article.status}`}>{article.status}</span>
                )}
              </div>
            )}

            {/* Mod note input */}
            {showModNote && (
              <div className="blog-mod-note-row">
                <textarea className="blog-editor-sidebar-textarea" rows={2} value={modNote}
                  onChange={e => setModNote(e.target.value)}
                  placeholder={showModNote === 'reject' ? 'Reason for rejection (sent to author)…' : 'Describe what needs revision…'} />
                <div style={{ display: 'flex', gap: 8 }}>
                  <button className="tiptap-action-btn secondary small" onClick={() => setShowModNote(null)}>Cancel</button>
                  <button className="tiptap-action-btn primary small" disabled={moderating}
                    onClick={() => handleStatus(showModNote === 'reject' ? 'rejected' : 'needs_revision', modNote)}>
                    Send
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* Article body */}
          <div ref={contentRef} className="blog-article-content">
            <ReactMarkdown
              remarkPlugins={[remarkGfm]}
              components={{
                h1: makeHeadingRenderer(1),
                h2: makeHeadingRenderer(2),
                h3: makeHeadingRenderer(3),
              }}
            >
              {article.content || ''}
            </ReactMarkdown>
          </div>

          {/* Comments */}
          <div className="blog-comments">
            <h3>{comments.length} {comments.length === 1 ? 'Comment' : 'Comments'}</h3>
            {comments.map(c => (
              <div key={c.id} className="blog-comment">
                <p className="blog-comment-content">{c.content}</p>
                <div className="blog-comment-footer">
                  <span className="post-meta">{new Date(c.createdAt).toLocaleDateString()}</span>
                  {(user?.id === c.authorId || isAdmin) && (
                    <button className="post-action post-action-delete" onClick={() => handleDeleteComment(c.id)}>Delete</button>
                  )}
                </div>
              </div>
            ))}
            {user ? (
              <form className="blog-comment-form" onSubmit={handleComment}>
                <textarea value={commentText} onChange={e => setCommentText(e.target.value)}
                  placeholder="Add a comment…" rows={4} required />
                <button type="submit" className="tiptap-action-btn primary" disabled={submitting || !commentText.trim()}>
                  {submitting ? 'Posting…' : 'Post Comment'}
                </button>
              </form>
            ) : (
              <p className="forum-login-prompt">Sign in to comment.</p>
            )}
          </div>
        </article>
      </div>
    </div>
  )
}
