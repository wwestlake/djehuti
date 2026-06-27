import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { papersApi } from '../../api/papersApi'
import type { Paper, PaperSection } from '../../api/papersApi'

export default function PaperWorkspacePage() {
  const { paperId = '' } = useParams<{ paperId: string }>()
  const navigate = useNavigate()
  const onBack = () => navigate('/papers')
  const [paper, setPaper] = useState<Paper | null>(null)
  const [sections, setSections] = useState<PaperSection[]>([])
  const [activeSection, setActiveSection] = useState<PaperSection | null>(null)
  const [editTitle, setEditTitle] = useState('')
  const [editContent, setEditContent] = useState('')
  const [preview, setPreview] = useState(false)
  const [saving, setSaving] = useState(false)
  const [newSectionTitle, setNewSectionTitle] = useState('')
  const [addingSection, setAddingSection] = useState(false)
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    Promise.all([
      papersApi.get(paperId),
      papersApi.getSections(paperId),
    ]).then(([p, s]) => {
      setPaper(p)
      setSections(s)
      if (s.length > 0) activateSection(s[0])
    })
  }, [paperId])

  const activateSection = (s: PaperSection) => {
    setActiveSection(s)
    setEditTitle(s.title)
    setEditContent(s.content)
    setPreview(false)
  }

  const handleSaveSection = async () => {
    if (!activeSection) return
    setSaving(true)
    try {
      const updated = await papersApi.updateSection(activeSection.id, editTitle, editContent)
      setSections(prev => prev.map(s => s.id === updated.id ? updated : s))
      setActiveSection(updated)
    } finally {
      setSaving(false)
    }
  }

  const handleAddSection = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newSectionTitle.trim()) return
    setAddingSection(true)
    try {
      const s = await papersApi.createSection(paperId, newSectionTitle.trim(), sections.length)
      setSections(prev => [...prev, s])
      setNewSectionTitle('')
      activateSection(s)
    } finally {
      setAddingSection(false)
    }
  }

  const handleDeleteSection = async (id: string) => {
    if (!confirm('Delete this section?')) return
    await papersApi.deleteSection(id)
    const remaining = sections.filter(s => s.id !== id)
    setSections(remaining)
    if (activeSection?.id === id) {
      if (remaining.length > 0) activateSection(remaining[0])
      else setActiveSection(null)
    }
  }

  if (!paper) return <div className="forum-loading">Loading…</div>

  return (
    <div className="paper-workspace">
      <div className="paper-workspace-sidebar">
        <button className="breadcrumb-link" onClick={onBack}>← Papers</button>
        <h3 className="paper-workspace-title">{paper.title}</h3>
        <div className="paper-section-list">
          {sections.map(s => (
            <div key={s.id}
              className={`paper-section-item${activeSection?.id === s.id ? ' active' : ''}`}
              onClick={() => activateSection(s)}
            >
              <span>{s.title}</span>
              <button className="paper-section-delete"
                onClick={e => { e.stopPropagation(); handleDeleteSection(s.id) }}
                title="Delete section">×</button>
            </div>
          ))}
        </div>
        <form className="paper-add-section" onSubmit={handleAddSection}>
          <input value={newSectionTitle} onChange={e => setNewSectionTitle(e.target.value)}
            placeholder="New section…" maxLength={100} />
          <button type="submit" disabled={addingSection || !newSectionTitle.trim()}>+</button>
        </form>
      </div>

      <div className="paper-workspace-main">
        {activeSection ? (
          <>
            <div className="paper-editor-toolbar">
              <input className="paper-section-title-input" value={editTitle}
                onChange={e => setEditTitle(e.target.value)} maxLength={100} />
              <div style={{ display: 'flex', gap: '6px' }}>
                <button type="button" className={`blog-tab${preview ? ' active' : ''}`}
                  onClick={() => setPreview(v => !v)}>
                  {preview ? 'Edit' : 'Preview'}
                </button>
                <button type="button" className="primary-action" onClick={handleSaveSection} disabled={saving}>
                  {saving ? 'Saving…' : 'Save'}
                </button>
              </div>
            </div>
            {preview ? (
              <div className="blog-article-content blog-editor-preview">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>{editContent || '*Empty section.*'}</ReactMarkdown>
              </div>
            ) : (
              <textarea ref={textareaRef} className="blog-editor-textarea paper-editor-textarea"
                value={editContent} onChange={e => setEditContent(e.target.value)}
                placeholder="Write this section in Markdown…" />
            )}
          </>
        ) : (
          <div className="paper-empty">Add a section using the sidebar to start writing.</div>
        )}
      </div>
    </div>
  )
}
