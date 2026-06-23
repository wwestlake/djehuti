import { useEffect, useState } from 'react'
import { blogApi } from '../components/blog/blogApi'
import type { BlogSection, BlogArticle } from '../components/blog/blogApi'
import { useAuth } from '../contexts/AuthContext'

interface Props {
  onNavigateArticle: (slug: string) => void
  onNavigateEditor: (articleId?: string) => void
}

export default function BlogPage({ onNavigateArticle, onNavigateEditor }: Props) {
  const { user } = useAuth()
  const [sections, setSections] = useState<BlogSection[]>([])
  const [articles, setArticles] = useState<BlogArticle[]>([])
  const [activeSection, setActiveSection] = useState<string | undefined>()
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    blogApi.getSections().then(setSections)
  }, [])

  useEffect(() => {
    setLoading(true)
    blogApi.getArticles(activeSection)
      .then(setArticles)
      .finally(() => setLoading(false))
  }, [activeSection])

  return (
    <div className="blog-page">
      <div className="blog-header">
        <h1 className="blog-title">Articles</h1>
        {user && (
          <button className="btn-primary" onClick={() => onNavigateEditor()}>
            Write Article
          </button>
        )}
      </div>

      <div className="blog-section-tabs">
        <button
          className={`blog-tab${!activeSection ? ' active' : ''}`}
          onClick={() => setActiveSection(undefined)}
        >
          All
        </button>
        {sections.map(s => (
          <button
            key={s.id}
            className={`blog-tab${activeSection === s.id ? ' active' : ''}`}
            onClick={() => setActiveSection(s.id)}
          >
            {s.name}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="forum-loading">Loading articles…</div>
      ) : articles.length === 0 ? (
        <p className="forum-empty">No articles yet.</p>
      ) : (
        <div className="blog-article-list">
          {articles.map(a => (
            <article key={a.id} className="blog-card" onClick={() => onNavigateArticle(a.slug)}>
              {a.coverUrl && <img src={a.coverUrl} alt={a.title} className="blog-card-cover" />}
              <div className="blog-card-body">
                <h2 className="blog-card-title">{a.title}</h2>
                {a.excerpt && <p className="blog-card-excerpt">{a.excerpt}</p>}
                <div className="blog-card-meta">
                  {a.publishedAt && <span>{new Date(a.publishedAt).toLocaleDateString()}</span>}
                </div>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  )
}
