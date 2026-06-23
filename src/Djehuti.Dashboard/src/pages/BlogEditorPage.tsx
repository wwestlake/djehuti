import { useEffect, useState } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { blogApi } from '../components/blog/blogApi'
import type { BlogArticle, BlogSection } from '../components/blog/blogApi'

interface Props {
  articleId?: string
  onSaved: (slug: string) => void
  onCancel: () => void
}

export default function BlogEditorPage({ articleId, onSaved, onCancel }: Props) {
  const [sections, setSections] = useState<BlogSection[]>([])
  const [sectionId, setSectionId] = useState('')
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [excerpt, setExcerpt] = useState('')
  const [preview, setPreview] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [article, setArticle] = useState<BlogArticle | null>(null)

  useEffect(() => {
    blogApi.getSections().then(s => {
      setSections(s)
      if (s.length > 0 && !sectionId) setSectionId(s[0].id)
    })
  }, [])

  useEffect(() => {
    if (!articleId) return
    blogApi.getMyArticles().then(articles => {
      const a = articles.find(x => x.id === articleId)
      if (a) {
        setArticle(a)
        setTitle(a.title)
        setContent(a.content)
        setExcerpt(a.excerpt ?? '')
        setSectionId(a.sectionId)
      }
    })
  }, [articleId])

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!title.trim() || !content.trim() || !sectionId) return
    setSaving(true)
    setError(null)
    try {
      let saved: BlogArticle
      if (article) {
        saved = await blogApi.updateArticle(article.id, title.trim(), content.trim(), excerpt || undefined)
      } else {
        saved = await blogApi.createArticle(sectionId, title.trim(), content.trim(), excerpt || undefined)
      }
      onSaved(saved.slug)
    } catch {
      setError('Failed to save article.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="blog-editor">
      <div className="blog-editor-toolbar">
        <button className="breadcrumb-link" onClick={onCancel}>← Cancel</button>
        <div className="blog-editor-toolbar-right">
          <button
            type="button"
            className={`blog-tab${preview ? ' active' : ''}`}
            onClick={() => setPreview(v => !v)}
          >
            {preview ? 'Edit' : 'Preview'}
          </button>
          <button
            type="button"
            className="btn-primary"
            onClick={handleSave}
            disabled={saving || !title.trim() || !content.trim()}
          >
            {saving ? 'Saving…' : article ? 'Save Changes' : 'Create Draft'}
          </button>
        </div>
      </div>

      {error && <p className="forum-error">{error}</p>}

      <div className="blog-editor-form">
        <label className="blog-editor-label">
          Section
          <select value={sectionId} onChange={e => setSectionId(e.target.value)}>
            {sections.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
        </label>

        <label className="blog-editor-label">
          Title
          <input
            type="text"
            value={title}
            onChange={e => setTitle(e.target.value)}
            placeholder="Article title"
            maxLength={200}
          />
        </label>

        <label className="blog-editor-label">
          Excerpt (optional)
          <input
            type="text"
            value={excerpt}
            onChange={e => setExcerpt(e.target.value)}
            placeholder="Short summary shown in listings"
            maxLength={300}
          />
        </label>

        <label className="blog-editor-label">
          Content (Markdown)
        </label>

        {preview ? (
          <div className="blog-article-content blog-editor-preview">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{content || '*Nothing to preview yet.*'}</ReactMarkdown>
          </div>
        ) : (
          <textarea
            className="blog-editor-textarea"
            value={content}
            onChange={e => setContent(e.target.value)}
            placeholder="Write your article in Markdown…"
            rows={24}
          />
        )}
      </div>
    </div>
  )
}
