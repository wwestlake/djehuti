import { useEffect, useRef, useState } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { papersApi } from '../components/papers/papersApi'
import type { Paper, PaperSection } from '../components/papers/papersApi'
import { apiBase } from '../lib/apiBase'

interface Props {
  paperId: string
  onBack: () => void
}

export default function PaperWorkspacePage({ paperId, onBack }: Props) {
  const [paper, setPaper] = useState<Paper | null>(null)
  const [sections, setSections] = useState<PaperSection[]>([])
  const [activeSection, setActiveSection] = useState<PaperSection | null>(null)
  const [editTitle, setEditTitle] = useState('')
  const [editContent, setEditContent] = useState('')
  const [preview, setPreview] = useState(false)
  const [aiPrompt, setAiPrompt] = useState('')
  const [aiResponse, setAiResponse] = useState('')
  const [aiLoading, setAiLoading] = useState(false)
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
    setAiResponse('')
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

  const handleAiAssist = async () => {
    if (!aiPrompt.trim()) return
    setAiLoading(true)
    setAiResponse('')
    try {
      const sectionContext = activeSection
        ? `\n\nCurrent section "${activeSection.title}":\n${editContent}`
        : ''
      const res = await fetch(`${apiBase}/analyst/ask`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          question: aiPrompt + sectionContext,
          datasetIds: [],
        }),
      })
      if (res.ok) {
        const data = await res.json()
        setAiResponse(data.answer ?? '')
      } else {
        setAiResponse('AI assistant unavailable.')
      }
    } finally {
      setAiLoading(false)
    }
  }

  const insertAiResponse = () => {
    if (!aiResponse || !textareaRef.current) return
    const el = textareaRef.current
    const start = el.selectionStart
    const end = el.selectionEnd
    const newContent = editContent.slice(0, start) + '\n\n' + aiResponse + '\n\n' + editContent.slice(end)
    setEditContent(newContent)
    setAiResponse('')
    setAiPrompt('')
  }

  if (!paper) return <div className="forum-loading">Loading…</div>

  return (
    <div className="paper-workspace">
      <div className="paper-workspace-sidebar">
        <button className="breadcrumb-link" onClick={onBack}>← Papers</button>
        <h3 className="paper-workspace-title">{paper.title}</h3>

        <div className="paper-section-list">
          {sections.map(s => (
            <div
              key={s.id}
              className={`paper-section-item${activeSection?.id === s.id ? ' active' : ''}`}
              onClick={() => activateSection(s)}
            >
              <span>{s.title}</span>
              <button
                className="paper-section-delete"
                onClick={e => { e.stopPropagation(); handleDeleteSection(s.id) }}
                title="Delete section"
              >×</button>
            </div>
          ))}
        </div>

        <form className="paper-add-section" onSubmit={handleAddSection}>
          <input
            value={newSectionTitle}
            onChange={e => setNewSectionTitle(e.target.value)}
            placeholder="New section…"
            maxLength={100}
          />
          <button type="submit" disabled={addingSection || !newSectionTitle.trim()}>+</button>
        </form>
      </div>

      <div className="paper-workspace-main">
        {activeSection ? (
          <>
            <div className="paper-editor-toolbar">
              <input
                className="paper-section-title-input"
                value={editTitle}
                onChange={e => setEditTitle(e.target.value)}
                maxLength={100}
              />
              <div style={{ display: 'flex', gap: '6px' }}>
                <button
                  type="button"
                  className={`blog-tab${preview ? ' active' : ''}`}
                  onClick={() => setPreview(v => !v)}
                >
                  {preview ? 'Edit' : 'Preview'}
                </button>
                <button type="button" className="btn-primary" onClick={handleSaveSection} disabled={saving}>
                  {saving ? 'Saving…' : 'Save'}
                </button>
              </div>
            </div>

            {preview ? (
              <div className="blog-article-content blog-editor-preview">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>{editContent || '*Empty section.*'}</ReactMarkdown>
              </div>
            ) : (
              <textarea
                ref={textareaRef}
                className="blog-editor-textarea paper-editor-textarea"
                value={editContent}
                onChange={e => setEditContent(e.target.value)}
                placeholder="Write this section in Markdown…"
              />
            )}

            <div className="paper-ai-panel">
              <h4 className="paper-ai-heading">AI Assistant</h4>
              <div className="paper-ai-input-row">
                <input
                  className="paper-ai-input"
                  value={aiPrompt}
                  onChange={e => setAiPrompt(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && handleAiAssist()}
                  placeholder="Ask the AI to help with this section…"
                />
                <button className="btn-primary" onClick={handleAiAssist} disabled={aiLoading || !aiPrompt.trim()}>
                  {aiLoading ? '…' : 'Ask'}
                </button>
              </div>
              {aiResponse && (
                <div className="paper-ai-response">
                  <div className="blog-article-content">
                    <ReactMarkdown remarkPlugins={[remarkGfm]}>{aiResponse}</ReactMarkdown>
                  </div>
                  <button className="btn-primary" onClick={insertAiResponse}>Insert into section</button>
                </div>
              )}
            </div>
          </>
        ) : (
          <div className="paper-empty">Add a section using the sidebar to start writing.</div>
        )}
      </div>
    </div>
  )
}
