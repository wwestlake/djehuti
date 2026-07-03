import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import ReactMarkdown from 'react-markdown'
import { papersApi } from '../../api/papersApi'
import type { PublicPaper, PublicPaperDetail } from '../../api/papersApi'
import { useAuth } from '../../contexts/AuthContext'

export default function PublicPapersPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [papers, setPapers] = useState<PublicPaper[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    papersApi.publicList()
      .then(setPapers)
      .catch(() => setError('Could not load papers.'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="forum-loading">Loading…</div>

  return (
    <div className="community-page">
      <div className="paper-public-head">
        <h1 className="community-page-title">Papers</h1>
        {user && (
          <button className="primary-action" onClick={() => navigate('/papers/workspace')}>
            My workspace
          </button>
        )}
      </div>
      <p className="paper-public-intro">
        Published research from LagDaemon. Reading is open to everyone; authoring requires an account.
      </p>

      {error && <p className="auth-error">{error}</p>}

      {!error && papers.length === 0 ? (
        <p className="forum-empty">No published papers yet.</p>
      ) : (
        <div className="papers-grid">
          {papers.map(p => (
            <div key={p.id} className="paper-card" onClick={() => navigate('/papers/read/' + p.id)}>
              <div className="paper-card-header">
                <h3 className="paper-card-title">{p.title}</h3>
              </div>
              {p.abstract && (
                <p className="paper-card-abstract">
                  {p.abstract.replace(/[*_`#]/g, '').slice(0, 280)}
                  {p.abstract.length > 280 ? '…' : ''}
                </p>
              )}
              <div className="paper-card-footer">
                <span className="post-meta">{p.authorName}</span>
                <span className="post-meta">{new Date(p.updatedAt).toLocaleDateString()}</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

export function PublicPaperReadPage() {
  const { paperId } = useParams<{ paperId: string }>()
  const [detail, setDetail] = useState<PublicPaperDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!paperId) return
    papersApi.publicGet(paperId)
      .then(setDetail)
      .catch(() => setError('This paper is not available.'))
      .finally(() => setLoading(false))
  }, [paperId])

  if (loading) return <div className="forum-loading">Loading…</div>

  if (error || !detail) {
    return (
      <div className="community-page">
        <p className="forum-empty">{error ?? 'This paper is not available.'}</p>
        <Link className="breadcrumb-link" to="/papers">← All papers</Link>
      </div>
    )
  }

  const { paper, sections } = detail

  return (
    <div className="community-page paper-read">
      <Link className="breadcrumb-link" to="/papers">← All papers</Link>
      <h1 className="community-page-title">{paper.title}</h1>
      <p className="paper-read-meta">
        {paper.authorName} · {new Date(paper.updatedAt).toLocaleDateString()}
      </p>

      {paper.abstract && (
        <div className="paper-read-abstract">
          <h2>Abstract</h2>
          <div className="paper-read-body">
            <ReactMarkdown>{paper.abstract}</ReactMarkdown>
          </div>
        </div>
      )}

      {sections.map(section => (
        <section key={section.id} className="paper-read-section">
          <h2>{section.title}</h2>
          <div className="paper-read-body">
            <ReactMarkdown>{section.content}</ReactMarkdown>
          </div>
        </section>
      ))}
    </div>
  )
}
