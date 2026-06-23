import { useEffect, useState } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { blogApi, BlogArticle, BlogComment } from '../components/blog/blogApi'
import { useAuth } from '../contexts/AuthContext'

interface Props {
  slug: string
  onNavigateBack: () => void
  onNavigateEditor: (articleId: string) => void
}

export default function BlogArticlePage({ slug, onNavigateBack, onNavigateEditor }: Props) {
  const { user } = useAuth()
  const [article, setArticle] = useState<BlogArticle | null>(null)
  const [comments, setComments] = useState<BlogComment[]>([])
  const [loading, setLoading] = useState(true)
  const [commentText, setCommentText] = useState('')
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    Promise.all([
      blogApi.getArticle(slug),
      blogApi.getComments('').catch(() => [] as BlogComment[]),
    ]).then(([a]) => {
      setArticle(a)
      return blogApi.getComments(a.id)
    }).then(setComments).finally(() => setLoading(false))
  }, [slug])

  const handleComment = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!article || !commentText.trim()) return
    setSubmitting(true)
    try {
      const c = await blogApi.createComment(article.id, commentText.trim())
      setComments(prev => [...prev, c])
      setCommentText('')
    } finally {
      setSubmitting(false)
    }
  }

  const handleDeleteComment = async (id: string) => {
    if (!confirm('Delete comment?')) return
    await blogApi.deleteComment(id)
    setComments(prev => prev.filter(c => c.id !== id))
  }

  if (loading) return <div className="forum-loading">Loading…</div>
  if (!article) return <div className="forum-error">Article not found.</div>

  const isAuthor = user?.id === article.authorId
  const isAdmin = user?.role === 'admin'

  return (
    <div className="blog-article-page">
      <div className="forum-breadcrumb">
        <button className="breadcrumb-link" onClick={onNavigateBack}>Articles</button>
        {' › '}
        <span>{article.title}</span>
      </div>

      {article.coverUrl && (
        <img src={article.coverUrl} alt={article.title} className="blog-article-cover" />
      )}

      <div className="blog-article-header">
        <h1>{article.title}</h1>
        <div className="blog-article-meta">
          {article.publishedAt && (
            <span>{new Date(article.publishedAt).toLocaleDateString()}</span>
          )}
          <span className={`blog-status-badge blog-status-${article.status}`}>{article.status}</span>
        </div>
        {(isAuthor || isAdmin) && (
          <div className="blog-article-actions">
            <button onClick={() => onNavigateEditor(article.id)}>Edit</button>
            {article.status === 'draft' && (
              <button onClick={() => blogApi.setStatus(article.id, 'submitted').then(setArticle)}>
                Submit for Review
              </button>
            )}
            {isAdmin && article.status === 'submitted' && (
              <>
                <button className="btn-primary" onClick={() => blogApi.setStatus(article.id, 'published').then(setArticle)}>
                  Publish
                </button>
                <button onClick={() => blogApi.setStatus(article.id, 'rejected').then(setArticle)}>
                  Reject
                </button>
              </>
            )}
          </div>
        )}
      </div>

      <div className="blog-article-content">
        <ReactMarkdown remarkPlugins={[remarkGfm]}>{article.content}</ReactMarkdown>
      </div>

      <div className="blog-comments">
        <h3>{comments.length} {comments.length === 1 ? 'Comment' : 'Comments'}</h3>
        {comments.map(c => (
          <div key={c.id} className="blog-comment">
            <p className="blog-comment-content">{c.content}</p>
            <div className="blog-comment-footer">
              <span className="post-meta">{new Date(c.createdAt).toLocaleDateString()}</span>
              {(user?.id === c.authorId || isAdmin) && (
                <button className="post-action post-action-delete" onClick={() => handleDeleteComment(c.id)}>
                  Delete
                </button>
              )}
            </div>
          </div>
        ))}

        {user ? (
          <form className="blog-comment-form" onSubmit={handleComment}>
            <textarea
              value={commentText}
              onChange={e => setCommentText(e.target.value)}
              placeholder="Add a comment…"
              rows={4}
              required
            />
            <button type="submit" className="btn-primary" disabled={submitting || !commentText.trim()}>
              {submitting ? 'Posting…' : 'Post Comment'}
            </button>
          </form>
        ) : (
          <p className="forum-login-prompt">Sign in to comment.</p>
        )}
      </div>
    </div>
  )
}
