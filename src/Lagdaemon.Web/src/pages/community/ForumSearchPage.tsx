import { useState, useRef } from 'react'

interface SearchHit {
  hitType: 'thread' | 'post'
  threadId: string
  title: string
  forumId: string
  createdAt: string
  authorId: string
  postId?: string
  postSnippet?: string
  rank: number
}

interface Props {
  onNavigateThread: (threadId: string) => void
  onNavigateForum: (forumId: string) => void
  initialQuery?: string
}

export default function ForumSearchPage({ onNavigateThread, onNavigateForum, initialQuery = '' }: Props) {
  const [query, setQuery] = useState(initialQuery)
  const [author, setAuthor] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [showFilters, setShowFilters] = useState(false)
  const [results, setResults] = useState<SearchHit[]>([])
  const [loading, setLoading] = useState(false)
  const [searched, setSearched] = useState(false)
  const [error, setError] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  const search = async (q = query) => {
    if (q.trim().length < 2) return
    setLoading(true)
    setError('')
    setSearched(true)
    try {
      const params = new URLSearchParams({ q: q.trim(), page: '1', pageSize: '30' })
      if (author) params.set('author', author)
      if (fromDate) params.set('fromDate', fromDate)
      if (toDate) params.set('toDate', toDate)
      const res = await fetch(`/djehuti/api/forum/search?${params}`, { credentials: 'include' })
      if (!res.ok) { setError('Search failed. Try a different query.'); setResults([]); return }
      setResults(await res.json())
    } catch {
      setError('Search unavailable.')
    } finally {
      setLoading(false)
    }
  }

  const handleKey = (e: React.KeyboardEvent) => { if (e.key === 'Enter') search() }

  return (
    <div className="community-page forum-search-page">
      <h1 className="forum-search-title">Search Forum</h1>

      <div className="forum-search-bar">
        <input
          ref={inputRef}
          className="forum-search-input"
          placeholder="Search threads and posts…"
          value={query}
          onChange={e => setQuery(e.target.value)}
          onKeyDown={handleKey}
          autoFocus
        />
        <button className="primary-action forum-search-btn" onClick={() => search()} disabled={loading || query.trim().length < 2}>
          {loading ? 'Searching…' : 'Search'}
        </button>
        <button className="forum-search-filter-toggle" onClick={() => setShowFilters(v => !v)} type="button">
          {showFilters ? 'Hide filters' : 'Filters'}
        </button>
      </div>

      {showFilters && (
        <div className="forum-search-filters">
          <label>
            <span>Author ID</span>
            <input className="forum-search-filter-input" placeholder="User UUID" value={author} onChange={e => setAuthor(e.target.value)} />
          </label>
          <label>
            <span>From</span>
            <input type="date" className="forum-search-filter-input" value={fromDate} onChange={e => setFromDate(e.target.value)} />
          </label>
          <label>
            <span>To</span>
            <input type="date" className="forum-search-filter-input" value={toDate} onChange={e => setToDate(e.target.value)} />
          </label>
        </div>
      )}

      {error && <p className="forum-error-msg">{error}</p>}

      {searched && !loading && !error && (
        <p className="forum-search-count">
          {results.length === 0 ? 'No results.' : `${results.length} result${results.length === 1 ? '' : 's'}`}
        </p>
      )}

      <div className="forum-search-results">
        {results.map((hit, i) => (
          <div key={`${hit.hitType}-${hit.postId ?? hit.threadId}-${i}`} className="forum-search-hit">
            <div className="forum-search-hit-type">{hit.hitType === 'thread' ? 'Thread' : 'Reply'}</div>
            <div className="forum-search-hit-title">
              <button
                className="breadcrumb-link forum-search-hit-link"
                onClick={() => onNavigateThread(hit.threadId)}
              >
                {hit.title}
              </button>
            </div>
            {hit.postSnippet && (
              <p className="forum-search-hit-snippet">{hit.postSnippet}</p>
            )}
            <div className="forum-search-hit-meta">
              <button className="breadcrumb-link forum-search-forum-link" onClick={() => onNavigateForum(hit.forumId)}>
                View forum
              </button>
              <span className="forum-search-hit-date">{new Date(hit.createdAt).toLocaleDateString()}</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
