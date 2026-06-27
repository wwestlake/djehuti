import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, Pin, Star, Upload } from 'lucide-react'
import { blogApi } from '../../api/blogApi'
import type { BlogSection, BlogArticle, BlogTag } from '../../api/blogApi'
import { useAuth } from '../../contexts/AuthContext'


function readingTime(content: string) {
  const words = (content || '').trim().split(/\s+/).length
  return `${Math.max(1, Math.round(words / 200))} min read`
}

export default function BlogPage() {
  const navigate = useNavigate()
  const onNavigateArticle = (slug: string) => navigate('/blog/' + slug)
  const onNavigateEditor = (articleId?: string) => navigate(articleId ? '/blog/editor/' + articleId : '/blog/editor')
  const { user } = useAuth()
  const [sections, setSections] = useState<BlogSection[]>([])
  const [tags, setTags] = useState<BlogTag[]>([])
  const [articles, setArticles] = useState<BlogArticle[]>([])
  const [activeSection, setActiveSection] = useState<string | undefined>()
  const [activeTag, setActiveTag] = useState<string | undefined>()
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [fetchError, setFetchError] = useState<string | null>(null)
  const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    blogApi.getSections().then(setSections)
    blogApi.getTags().then(setTags)
  }, [])

  // Debounce search input
  useEffect(() => {
    if (searchTimer.current) clearTimeout(searchTimer.current)
    searchTimer.current = setTimeout(() => setDebouncedSearch(search), 350)
    return () => { if (searchTimer.current) clearTimeout(searchTimer.current) }
  }, [search])

  useEffect(() => {
    setLoading(true)
    setFetchError(null)
    blogApi.getArticles({ sectionId: activeSection, search: debouncedSearch || undefined, tag: activeTag })
      .then(setArticles)
      .catch(e => setFetchError(String(e)))
      .finally(() => setLoading(false))
  }, [activeSection, debouncedSearch, activeTag])

  const pinned = articles.filter(a => a.pinned)
  const featured = articles.filter(a => a.featured && !a.pinned)
  const rest = articles.filter(a => !a.pinned && !a.featured)

  return (
    <div className="community-page">
      <div className="blog-header">
        <h1 className="community-page-title">Articles</h1>
        {user && (
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="tiptap-action-btn secondary" onClick={() => onNavigateEditor()}>
              <Upload size={14} /> Import
            </button>
            <button className="tiptap-action-btn primary" onClick={() => onNavigateEditor()}>
              Write Article
            </button>
          </div>
        )}
      </div>

      {/* Search bar */}
      <div className="blog-search-bar">
        <Search size={15} className="blog-search-icon" />
        <input className="blog-search-input" value={search} onChange={e => setSearch(e.target.value)}
          placeholder="Search articles…" />
        {search && <button className="blog-search-clear" onClick={() => setSearch('')}><span>✕</span></button>}
      </div>

      {/* Section tabs */}
      <div className="blog-section-tabs">
        <button className={`blog-tab${!activeSection ? ' active' : ''}`} onClick={() => setActiveSection(undefined)}>All</button>
        {sections.map(s => (
          <button key={s.id} className={`blog-tab${activeSection === s.id ? ' active' : ''}`}
            onClick={() => setActiveSection(activeSection === s.id ? undefined : s.id)}>
            {s.name}
          </button>
        ))}
      </div>

      {/* Tag filter */}
      {tags.length > 0 && (
        <div className="blog-tag-filter">
          {tags.map(t => (
            <button key={t.id} className={`blog-tag-filter-btn${activeTag === t.slug ? ' active' : ''}`}
              onClick={() => setActiveTag(activeTag === t.slug ? undefined : t.slug)}>
              {t.name}
            </button>
          ))}
        </div>
      )}

      {fetchError && <p className="auth-error">Failed to load articles: {fetchError}</p>}
      {loading ? (
        <div className="forum-loading">Loading articles…</div>
      ) : articles.length === 0 ? (
        <p className="forum-empty">{search ? `No results for "${search}".` : 'No articles yet.'}</p>
      ) : (
        <div className="blog-feed">
          {/* Pinned */}
          {pinned.length > 0 && (
            <div className="blog-feed-section">
              {pinned.map(a => (
                <article key={a.id} className="blog-card blog-card-pinned" onClick={() => onNavigateArticle(a.slug)}>
                  <div className="blog-card-pin-badge"><Pin size={11} /> Pinned</div>
                  {a.coverUrl && <img src={a.coverUrl} alt={a.title} className="blog-card-cover" />}
                  <div className="blog-card-body">
                    <h2 className="blog-card-title">{a.title}</h2>
                    {a.subtitle && <p className="blog-card-subtitle">{a.subtitle}</p>}
                    {a.excerpt && <p className="blog-card-excerpt">{a.excerpt}</p>}
                    <div className="blog-card-meta">
                      {a.publishedAt && <span>{new Date(a.publishedAt).toLocaleDateString()}</span>}
                      <span>{readingTime(a.content)}</span>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}

          {/* Featured row */}
          {featured.length > 0 && (
            <div className="blog-featured-row">
              {featured.map(a => (
                <article key={a.id} className="blog-card blog-card-featured" onClick={() => onNavigateArticle(a.slug)}>
                  <div className="blog-card-star-badge"><Star size={11} /> Featured</div>
                  {a.coverUrl && <img src={a.coverUrl} alt={a.title} className="blog-card-cover" />}
                  <div className="blog-card-body">
                    <h2 className="blog-card-title">{a.title}</h2>
                    {a.subtitle && <p className="blog-card-subtitle">{a.subtitle}</p>}
                    {a.excerpt && <p className="blog-card-excerpt">{a.excerpt}</p>}
                    <div className="blog-card-meta">
                      {a.publishedAt && <span>{new Date(a.publishedAt).toLocaleDateString()}</span>}
                      <span>{readingTime(a.content)}</span>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}

          {/* Main list */}
          <div className="blog-article-list">
            {rest.map(a => (
              <article key={a.id} className="blog-card" onClick={() => onNavigateArticle(a.slug)}>
                {a.coverUrl && <img src={a.coverUrl} alt={a.title} className="blog-card-cover blog-card-cover-inline" />}
                <div className="blog-card-body">
                  <h2 className="blog-card-title">{a.title}</h2>
                  {a.subtitle && <p className="blog-card-subtitle">{a.subtitle}</p>}
                  {a.excerpt && <p className="blog-card-excerpt">{a.excerpt}</p>}
                  <div className="blog-card-meta">
                    {a.publishedAt && <span>{new Date(a.publishedAt).toLocaleDateString()}</span>}
                    <span>{readingTime(a.content)}</span>
                  </div>
                </div>
              </article>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
