import { useCallback, useEffect, useRef, useState } from 'react'
import { useEditor, EditorContent, BubbleMenu } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import Underline from '@tiptap/extension-underline'
import Link from '@tiptap/extension-link'
import Image from '@tiptap/extension-image'
import Placeholder from '@tiptap/extension-placeholder'
import CharacterCount from '@tiptap/extension-character-count'
import {
  Bold, Italic, UnderlineIcon, Strikethrough, Code, Link2, Image as ImageIcon,
  Heading1, Heading2, Heading3, List, ListOrdered, Quote, Minus,
  Eye, EyeOff, Send, Save, ChevronLeft, X, Tag, Check, Upload,
} from 'lucide-react'
import { blogApi } from '../../api/blogApi'
import type { BlogArticle, BlogSection, BlogTag } from '../../api/blogApi'
import BlogUploadModal from './BlogUploadModal'

interface Props {
  articleId?: string
  onSaved: (slug: string) => void
  onCancel: () => void
}

const AUTOSAVE_MS = 30_000

function readingTime(text: string): string {
  const words = text.trim().split(/\s+/).length
  const mins = Math.max(1, Math.round(words / 200))
  return `${mins} min read`
}

export default function BlogEditorPage({ articleId, onSaved, onCancel }: Props) {
  const [sections, setSections] = useState<BlogSection[]>([])
  const [allTags, setAllTags] = useState<BlogTag[]>([])
  const [article, setArticle] = useState<BlogArticle | null>(null)

  // Metadata fields
  const [sectionId, setSectionId] = useState('')
  const [title, setTitle] = useState('')
  const [subtitle, setSubtitle] = useState('')
  const [excerpt, setExcerpt] = useState('')
  const [coverUrl, setCoverUrl] = useState('')
  const [visibility, setVisibility] = useState<'public' | 'unlisted' | 'private'>('public')
  const [selectedTagIds, setSelectedTagIds] = useState<string[]>([])
  const [tagSearch, setTagSearch] = useState('')
  const [showTagPicker, setShowTagPicker] = useState(false)

  const [preview, setPreview] = useState(false)
  const [saving, setSaving] = useState(false)
  const [submitConfirm, setSubmitConfirm] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [lastSaved, setLastSaved] = useState<Date | null>(null)
  const [linkUrl, setLinkUrl] = useState('')
  const [showLinkInput, setShowLinkInput] = useState(false)
  const [imageUrl, setImageUrl] = useState('')
  const [showImageInput, setShowImageInput] = useState(false)
  const [showUpload, setShowUpload] = useState(false)

  const autosaveTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const savedArticleId = useRef<string | null>(articleId ?? null)

  const editor = useEditor({
    extensions: [
      StarterKit.configure({ codeBlock: { HTMLAttributes: { class: 'tiptap-code-block' } } }),
      Underline,
      Link.configure({ openOnClick: false, HTMLAttributes: { rel: 'noopener noreferrer' } }),
      Image.configure({ HTMLAttributes: { class: 'tiptap-image' } }),
      Placeholder.configure({ placeholder: 'Start writing your article…' }),
      CharacterCount,
    ],
    editorProps: {
      attributes: { class: 'tiptap-editor-content' },
    },
    onUpdate: () => {
      scheduleAutosave()
    },
  })

  // Load initial data
  useEffect(() => {
    blogApi.getSections().then(s => {
      setSections(s)
      if (s.length > 0 && !sectionId) setSectionId(s[0].id)
    })
    blogApi.getTags().then(setAllTags)
  }, [])

  useEffect(() => {
    if (!articleId) return
    blogApi.getMyArticles().then(articles => {
      const a = articles.find(x => x.id === articleId)
      if (!a) return
      setArticle(a)
      setTitle(a.title)
      setSubtitle(a.subtitle ?? '')
      setExcerpt(a.excerpt ?? '')
      setCoverUrl(a.coverUrl ?? '')
      setVisibility(a.visibility)
      setSectionId(a.sectionId)
      if (a.bodyJson) {
        try { editor?.commands.setContent(JSON.parse(a.bodyJson)) } catch { /* ignore */ }
      } else if (a.content) {
        editor?.commands.setContent(`<p>${a.content}</p>`)
      }
    })
    blogApi.getArticleTags(articleId).then(t => setSelectedTagIds(t.map(x => x.id)))
  }, [articleId, editor])

  const getContent = useCallback(() => {
    if (!editor) return { html: '', json: '', text: '' }
    return {
      html: editor.getHTML(),
      json: JSON.stringify(editor.getJSON()),
      text: editor.getText(),
    }
  }, [editor])

  const doSave = useCallback(async (quiet = false) => {
    if (!title.trim()) return
    if (!quiet) setSaving(true)
    setError(null)
    const { html, json } = getContent()
    try {
      let saved: BlogArticle
      if (savedArticleId.current) {
        saved = await blogApi.updateArticle(savedArticleId.current, {
          title: title.trim(), subtitle: subtitle.trim(), content: html,
          bodyJson: json, excerpt: excerpt.trim(), coverUrl: coverUrl.trim(), visibility,
        })
      } else {
        saved = await blogApi.createArticle({
          sectionId, title: title.trim(), subtitle: subtitle.trim(),
          content: html, bodyJson: json, excerpt: excerpt.trim(), visibility,
        })
        savedArticleId.current = saved.id
      }
      if (selectedTagIds.length > 0) {
        await blogApi.setArticleTags(saved.id, selectedTagIds)
      }
      setArticle(saved)
      setLastSaved(new Date())
      if (!quiet) onSaved(saved.slug)
    } catch {
      if (!quiet) setError('Failed to save. Please try again.')
    } finally {
      if (!quiet) setSaving(false)
    }
  }, [title, subtitle, excerpt, coverUrl, visibility, sectionId, selectedTagIds, getContent, onSaved])

  const scheduleAutosave = useCallback(() => {
    if (autosaveTimer.current) clearTimeout(autosaveTimer.current)
    autosaveTimer.current = setTimeout(() => doSave(true), AUTOSAVE_MS)
  }, [doSave])

  useEffect(() => () => { if (autosaveTimer.current) clearTimeout(autosaveTimer.current) }, [])

  const handleSubmit = async () => {
    await doSave(true)
    if (savedArticleId.current) {
      await blogApi.setStatus(savedArticleId.current, 'submitted')
      setSubmitConfirm(false)
      alert('Article submitted for review!')
    }
  }

  const applyLink = () => {
    if (!linkUrl) { editor?.chain().focus().unsetLink().run(); return }
    editor?.chain().focus().setLink({ href: linkUrl }).run()
    setLinkUrl('')
    setShowLinkInput(false)
  }

  const applyImage = () => {
    if (!imageUrl) return
    editor?.chain().focus().setImage({ src: imageUrl }).run()
    setImageUrl('')
    setShowImageInput(false)
  }

  const toggleTag = (id: string) =>
    setSelectedTagIds(ids => ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id])

  const filteredTags = allTags.filter(t =>
    t.name.toLowerCase().includes(tagSearch.toLowerCase()))

  const selectedTagNames = allTags.filter(t => selectedTagIds.includes(t.id))

  const isReadOnly = article?.status === 'submitted' || article?.status === 'under_review'
  const wordCount = editor?.storage.characterCount?.words() ?? 0

  return (
    <div className="blog-editor-shell">
      {/* ── Top toolbar ── */}
      <div className="blog-editor-topbar">
        <button className="tiptap-toolbar-btn icon-only" onClick={onCancel} title="Back">
          <ChevronLeft size={18} />
        </button>
        <div className="blog-editor-topbar-title">
          {article ? `Editing: ${article.title}` : 'New Article'}
          {isReadOnly && <span className="blog-status-badge submitted">Under Review</span>}
        </div>
        <div className="blog-editor-topbar-actions">
          {lastSaved && (
            <span className="blog-autosave-label">
              Saved {lastSaved.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </span>
          )}
          <button className="tiptap-toolbar-btn icon-only" onClick={() => setShowUpload(true)} title="Import document">
            <Upload size={16} />
          </button>
          <button className="tiptap-toolbar-btn icon-only" onClick={() => setPreview(v => !v)}
            title={preview ? 'Edit' : 'Preview'}>
            {preview ? <EyeOff size={16} /> : <Eye size={16} />}
          </button>
          <button className="tiptap-action-btn secondary" onClick={() => doSave()} disabled={saving || !title.trim()}>
            <Save size={14} /> {saving ? 'Saving…' : 'Save Draft'}
          </button>
          {!isReadOnly && (
            <button className="tiptap-action-btn primary" onClick={() => setSubmitConfirm(true)}
              disabled={!title.trim() || !savedArticleId.current}>
              <Send size={14} /> Submit
            </button>
          )}
        </div>
      </div>

      {error && <div className="tiptap-error">{error} <button onClick={() => setError(null)}><X size={14} /></button></div>}

      <div className="blog-editor-body">
        {/* ── Editor column ── */}
        <div className="blog-editor-main">
          {/* Title */}
          <input className="blog-editor-title-input" value={title}
            onChange={e => setTitle(e.target.value)}
            placeholder="Article title" maxLength={200} disabled={isReadOnly} />
          <input className="blog-editor-subtitle-input" value={subtitle}
            onChange={e => setSubtitle(e.target.value)}
            placeholder="Subtitle (optional)" maxLength={300} disabled={isReadOnly} />

          {/* Formatting toolbar */}
          {!preview && !isReadOnly && (
            <div className="tiptap-toolbar">
              <div className="tiptap-toolbar-group">
                <button className={`tiptap-toolbar-btn${editor?.isActive('bold') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleBold().run() }} title="Bold">
                  <Bold size={15} />
                </button>
                <button className={`tiptap-toolbar-btn${editor?.isActive('italic') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleItalic().run() }} title="Italic">
                  <Italic size={15} />
                </button>
                <button className={`tiptap-toolbar-btn${editor?.isActive('underline') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleUnderline().run() }} title="Underline">
                  <UnderlineIcon size={15} />
                </button>
                <button className={`tiptap-toolbar-btn${editor?.isActive('strike') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleStrike().run() }} title="Strikethrough">
                  <Strikethrough size={15} />
                </button>
                <button className={`tiptap-toolbar-btn${editor?.isActive('code') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleCode().run() }} title="Inline code">
                  <Code size={15} />
                </button>
              </div>
              <div className="tiptap-toolbar-sep" />
              <div className="tiptap-toolbar-group">
                <button className={`tiptap-toolbar-btn${editor?.isActive('heading', { level: 1 }) ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleHeading({ level: 1 }).run() }} title="Heading 1">
                  <Heading1 size={15} />
                </button>
                <button className={`tiptap-toolbar-btn${editor?.isActive('heading', { level: 2 }) ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleHeading({ level: 2 }).run() }} title="Heading 2">
                  <Heading2 size={15} />
                </button>
                <button className={`tiptap-toolbar-btn${editor?.isActive('heading', { level: 3 }) ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleHeading({ level: 3 }).run() }} title="Heading 3">
                  <Heading3 size={15} />
                </button>
              </div>
              <div className="tiptap-toolbar-sep" />
              <div className="tiptap-toolbar-group">
                <button className={`tiptap-toolbar-btn${editor?.isActive('bulletList') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleBulletList().run() }} title="Bullet list">
                  <List size={15} />
                </button>
                <button className={`tiptap-toolbar-btn${editor?.isActive('orderedList') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleOrderedList().run() }} title="Ordered list">
                  <ListOrdered size={15} />
                </button>
                <button className={`tiptap-toolbar-btn${editor?.isActive('blockquote') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleBlockquote().run() }} title="Blockquote">
                  <Quote size={15} />
                </button>
                <button className="tiptap-toolbar-btn"
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().setHorizontalRule().run() }} title="Divider">
                  <Minus size={15} />
                </button>
              </div>
              <div className="tiptap-toolbar-sep" />
              <div className="tiptap-toolbar-group">
                <button className={`tiptap-toolbar-btn${editor?.isActive('link') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); setShowLinkInput(v => !v) }} title="Link">
                  <Link2 size={15} />
                </button>
                <button className="tiptap-toolbar-btn"
                  onMouseDown={e => { e.preventDefault(); setShowImageInput(v => !v) }} title="Image URL">
                  <ImageIcon size={15} />
                </button>
                <button className={`tiptap-toolbar-btn${editor?.isActive('codeBlock') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleCodeBlock().run() }} title="Code block">
                  {'</>'}
                </button>
              </div>
            </div>
          )}

          {/* Link input */}
          {showLinkInput && (
            <div className="tiptap-inline-input-row">
              <input value={linkUrl} onChange={e => setLinkUrl(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && applyLink()}
                placeholder="https://…" className="tiptap-inline-input" autoFocus />
              <button className="tiptap-action-btn primary small" onClick={applyLink}>Apply</button>
              <button className="tiptap-action-btn secondary small" onClick={() => setShowLinkInput(false)}>Cancel</button>
            </div>
          )}

          {/* Image URL input */}
          {showImageInput && (
            <div className="tiptap-inline-input-row">
              <input value={imageUrl} onChange={e => setImageUrl(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && applyImage()}
                placeholder="Image URL…" className="tiptap-inline-input" autoFocus />
              <button className="tiptap-action-btn primary small" onClick={applyImage}>Insert</button>
              <button className="tiptap-action-btn secondary small" onClick={() => setShowImageInput(false)}>Cancel</button>
            </div>
          )}

          {/* Bubble menu on text selection */}
          {editor && !preview && (
            <BubbleMenu editor={editor} tippyOptions={{ duration: 100 }}>
              <div className="tiptap-bubble-menu">
                <button className={`tiptap-toolbar-btn small${editor.isActive('bold') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor.chain().focus().toggleBold().run() }}><Bold size={13} /></button>
                <button className={`tiptap-toolbar-btn small${editor.isActive('italic') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor.chain().focus().toggleItalic().run() }}><Italic size={13} /></button>
                <button className={`tiptap-toolbar-btn small${editor.isActive('underline') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor.chain().focus().toggleUnderline().run() }}><UnderlineIcon size={13} /></button>
                <button className={`tiptap-toolbar-btn small${editor.isActive('code') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor.chain().focus().toggleCode().run() }}><Code size={13} /></button>
                <button className={`tiptap-toolbar-btn small${editor.isActive('link') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); setShowLinkInput(true) }}><Link2 size={13} /></button>
              </div>
            </BubbleMenu>
          )}

          {/* Editor or Preview */}
          {preview ? (
            <div className="blog-article-content tiptap-preview-area"
              dangerouslySetInnerHTML={{ __html: editor?.getHTML() ?? '' }} />
          ) : (
            <div className={`tiptap-editor-wrap${isReadOnly ? ' readonly' : ''}`}>
              <EditorContent editor={editor} />
            </div>
          )}

          <div className="tiptap-word-count">
            {wordCount.toLocaleString()} words · {readingTime(editor?.getText() ?? '')}
          </div>
        </div>

        {/* ── Metadata sidebar ── */}
        <aside className="blog-editor-sidebar">
          <div className="blog-editor-sidebar-section">
            <label className="blog-editor-sidebar-label">Section</label>
            <select value={sectionId} onChange={e => setSectionId(e.target.value)} disabled={isReadOnly}>
              {sections.length === 0 && <option value="">General (default)</option>}
              {sections.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
          </div>

          <div className="blog-editor-sidebar-section">
            <label className="blog-editor-sidebar-label">Excerpt</label>
            <textarea value={excerpt} onChange={e => setExcerpt(e.target.value)}
              placeholder="Short summary shown in article listings" maxLength={300}
              rows={3} disabled={isReadOnly} className="blog-editor-sidebar-textarea" />
          </div>

          <div className="blog-editor-sidebar-section">
            <label className="blog-editor-sidebar-label">Cover image URL</label>
            <input value={coverUrl} onChange={e => setCoverUrl(e.target.value)}
              placeholder="https://…" disabled={isReadOnly} />
            {coverUrl && <img src={coverUrl} alt="" className="blog-editor-cover-preview" />}
          </div>

          <div className="blog-editor-sidebar-section">
            <label className="blog-editor-sidebar-label">Visibility</label>
            <select value={visibility} onChange={e => setVisibility(e.target.value as typeof visibility)}
              disabled={isReadOnly}>
              <option value="public">Public</option>
              <option value="unlisted">Unlisted</option>
              <option value="private">Private (draft only)</option>
            </select>
          </div>

          <div className="blog-editor-sidebar-section">
            <label className="blog-editor-sidebar-label">
              <Tag size={13} style={{ marginRight: 4 }} />Tags
            </label>
            <div className="blog-tag-selected">
              {selectedTagNames.map(t => (
                <span key={t.id} className="blog-tag-chip">
                  {t.name}
                  {!isReadOnly && <button onClick={() => toggleTag(t.id)}><X size={10} /></button>}
                </span>
              ))}
            </div>
            {!isReadOnly && (
              <>
                <button className="tiptap-action-btn secondary small"
                  onClick={() => setShowTagPicker(v => !v)}>
                  {showTagPicker ? 'Close' : '+ Add tags'}
                </button>
                {showTagPicker && (
                  <div className="blog-tag-picker">
                    <input value={tagSearch} onChange={e => setTagSearch(e.target.value)}
                      placeholder="Search tags…" className="blog-tag-search" autoFocus />
                    <div className="blog-tag-list">
                      {filteredTags.map(t => (
                        <button key={t.id} className={`blog-tag-option${selectedTagIds.includes(t.id) ? ' selected' : ''}`}
                          onClick={() => toggleTag(t.id)}>
                          {selectedTagIds.includes(t.id) && <Check size={12} />} {t.name}
                        </button>
                      ))}
                      {filteredTags.length === 0 && <p className="blog-tag-empty">No tags found</p>}
                    </div>
                  </div>
                )}
              </>
            )}
          </div>

          {article && (
            <div className="blog-editor-sidebar-section">
              <label className="blog-editor-sidebar-label">Status</label>
              <span className={`blog-status-badge ${article.status}`}>{article.status}</span>
            </div>
          )}
        </aside>
      </div>

      {/* ── Upload modal ── */}
      {showUpload && (
        <BlogUploadModal
          onClose={() => setShowUpload(false)}
          onContentReady={(html, filename) => {
            editor?.commands.setContent(html)
            if (!title.trim()) setTitle(filename.replace(/\.[^.]+$/, ''))
            setShowUpload(false)
          }}
        />
      )}

      {/* ── Submit confirmation modal ── */}
      {submitConfirm && (
        <div className="tiptap-modal-overlay" onClick={() => setSubmitConfirm(false)}>
          <div className="tiptap-modal" onClick={e => e.stopPropagation()}>
            <h3>Submit for review?</h3>
            <p>Your article will be sent to a moderator for review before publishing. You won't be able to edit it while it's under review.</p>
            <div className="tiptap-modal-actions">
              <button className="tiptap-action-btn secondary" onClick={() => setSubmitConfirm(false)}>Cancel</button>
              <button className="tiptap-action-btn primary" onClick={handleSubmit}>Submit</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
