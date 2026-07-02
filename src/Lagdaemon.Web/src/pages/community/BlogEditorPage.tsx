import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useEditor, EditorContent } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import Underline from '@tiptap/extension-underline'
import Link from '@tiptap/extension-link'
import Image from '@tiptap/extension-image'
import Placeholder from '@tiptap/extension-placeholder'
import CharacterCount from '@tiptap/extension-character-count'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import {
  Bold, Italic, UnderlineIcon, Strikethrough, Code, Link2, Image as ImageIcon,
  Heading1, Heading2, Heading3, List, ListOrdered, Quote, Minus,
  Eye, EyeOff, Send, Save, ChevronLeft, X, Tag, Check, Upload,
} from 'lucide-react'
import { blogApi } from '../../api/blogApi'
import type { BlogArticle, BlogSection, BlogTag } from '../../api/blogApi'
import BlogUploadModal from './BlogUploadModal'
import { uploadToS3 } from '../../api/mediaApi'

const AUTOSAVE_MS = 30_000

function readingTime(text: string): string {
  const words = text.trim().split(/\s+/).filter(Boolean).length
  const mins = Math.max(1, Math.round(words / 200))
  return `${mins} min read`
}

function markdownToHtml(markdown: string): string {
  const lines = markdown.replace(/\r\n/g, '\n').split('\n')
  const out: string[] = []
  let inCode = false
  let inUl = false
  let inOl = false

  const closeLists = () => {
    if (inUl) {
      out.push('</ul>')
      inUl = false
    }
    if (inOl) {
      out.push('</ol>')
      inOl = false
    }
  }

  const inline = (s: string) =>
    s.replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.+?)\*/g, '<em>$1</em>')
      .replace(/`(.+?)`/g, '<code>$1</code>')
      .replace(/\[(.+?)\]\((.+?)\)/g, '<a href="$2">$1</a>')

  for (const line of lines) {
    if (line.startsWith('```')) {
      closeLists()
      if (inCode) {
        out.push('</code></pre>')
        inCode = false
      } else {
        out.push('<pre><code>')
        inCode = true
      }
      continue
    }

    if (inCode) {
      out.push(line.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'))
      continue
    }

    if (line.startsWith('### ')) { closeLists(); out.push(`<h3>${inline(line.slice(4))}</h3>`); continue }
    if (line.startsWith('## ')) { closeLists(); out.push(`<h2>${inline(line.slice(3))}</h2>`); continue }
    if (line.startsWith('# ')) { closeLists(); out.push(`<h1>${inline(line.slice(2))}</h1>`); continue }

    if (line.startsWith('- ') || line.startsWith('* ')) {
      if (inOl) {
        out.push('</ol>')
        inOl = false
      }
      if (!inUl) {
        out.push('<ul>')
        inUl = true
      }
      out.push(`<li>${inline(line.slice(2))}</li>`)
      continue
    }

    if (/^\d+\. /.test(line)) {
      if (inUl) {
        out.push('</ul>')
        inUl = false
      }
      if (!inOl) {
        out.push('<ol>')
        inOl = true
      }
      out.push(`<li>${inline(line.replace(/^\d+\. /, ''))}</li>`)
      continue
    }

    if (/^---+$/.test(line.trim())) {
      closeLists()
      out.push('<hr/>')
      continue
    }

    if (line.trim() === '') {
      closeLists()
      continue
    }

    closeLists()
    out.push(`<p>${inline(line)}</p>`)
  }

  closeLists()
  if (inCode) out.push('</code></pre>')
  return out.join('\n')
}

export default function BlogEditorPage() {
  const { articleId } = useParams<{ articleId: string }>()
  const navigate = useNavigate()
  const onCancel = () => navigate('/blog')

  const [sections, setSections] = useState<BlogSection[]>([])
  const [allTags, setAllTags] = useState<BlogTag[]>([])
  const [article, setArticle] = useState<BlogArticle | null>(null)

  const [sectionId, setSectionId] = useState('')
  const [title, setTitle] = useState('')
  const [subtitle, setSubtitle] = useState('')
  const [excerpt, setExcerpt] = useState('')
  const [coverUrl, setCoverUrl] = useState('')
  const [visibility, setVisibility] = useState<'public' | 'unlisted' | 'private'>('public')
  const [selectedTagIds, setSelectedTagIds] = useState<string[]>([])
  const [tagSearch, setTagSearch] = useState('')
  const [showTagPicker, setShowTagPicker] = useState(false)
  const [editorMode, setEditorMode] = useState<'rich' | 'markdown'>('rich')
  const [markdownText, setMarkdownText] = useState('')

  const [preview, setPreview] = useState(false)
  const [saving, setSaving] = useState(false)
  const [submitConfirm, setSubmitConfirm] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [lastSaved, setLastSaved] = useState<Date | null>(null)
  const [linkUrl, setLinkUrl] = useState('')
  const [showLinkInput, setShowLinkInput] = useState(false)
  const [imageUrl, setImageUrl] = useState('')
  const [showImageInput, setShowImageInput] = useState(false)
  const [showUpload, setShowUpload] = useState(false)
  const [imageUploading, setImageUploading] = useState(false)
  const imageFileRef = useRef<HTMLInputElement>(null)

  const autosaveTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const savedArticleId = useRef<string | null>(articleId ?? null)

  const editor = useEditor({
    extensions: [
      StarterKit.configure({ codeBlock: { HTMLAttributes: { class: 'tiptap-code-block' } } }),
      Underline,
      Link.configure({ openOnClick: false, HTMLAttributes: { rel: 'noopener noreferrer' } }),
      Image.configure({ HTMLAttributes: { class: 'tiptap-image' } }),
      Placeholder.configure({ placeholder: 'Start writing your article...' }),
      CharacterCount,
    ],
    editorProps: {
      attributes: { class: 'tiptap-editor-content' },
    },
    onUpdate: () => {
      if (editorMode === 'rich') scheduleAutosave()
    },
  })

  useEffect(() => {
    blogApi.getSections().then(items => {
      setSections(items)
      if (items.length > 0 && !sectionId) setSectionId(items[0].id)
    })
    blogApi.getTags().then(setAllTags)
  }, [])

  useEffect(() => {
    if (!articleId) return
    blogApi.getMyArticles().then(articles => {
      const current = articles.find(x => x.id === articleId)
      if (!current) return
      setArticle(current)
      setTitle(current.title)
      setSubtitle(current.subtitle ?? '')
      setExcerpt(current.excerpt ?? '')
      setCoverUrl(current.coverUrl ?? '')
      setVisibility(current.visibility)
      setSectionId(current.sectionId)
      if (current.bodyJson) {
        try {
          const parsed = JSON.parse(current.bodyJson)
          if (parsed?.format === 'markdown' && typeof parsed.markdown === 'string') {
            setEditorMode('markdown')
            setPreview(true)
            setMarkdownText(parsed.markdown)
          } else {
            editor?.commands.setContent(parsed)
          }
        } catch {
          if (current.content) editor?.commands.setContent(current.content)
        }
      } else if (current.content) {
        editor?.commands.setContent(current.content)
      }
    })
    blogApi.getArticleTags(articleId).then(tags => setSelectedTagIds(tags.map(x => x.id)))
  }, [articleId, editor])

  useEffect(() => {
    if (editorMode === 'markdown') scheduleAutosave()
  }, [markdownText, editorMode])

  useEffect(() => () => {
    if (autosaveTimer.current) clearTimeout(autosaveTimer.current)
  }, [])

  const markdownHtml = useMemo(() => markdownToHtml(markdownText), [markdownText])

  const getContent = useCallback(() => {
    if (editorMode === 'markdown') {
      return {
        html: markdownHtml,
        json: JSON.stringify({ format: 'markdown', markdown: markdownText }),
        text: markdownText,
      }
    }
    if (!editor) return { html: '', json: '', text: '' }
    return {
      html: editor.getHTML(),
      json: JSON.stringify(editor.getJSON()),
      text: editor.getText(),
    }
  }, [editor, editorMode, markdownHtml, markdownText])

  const syncEditorRoute = useCallback((savedId: string) => {
    if (!articleId) navigate(`/blog/editor/${savedId}`, { replace: true })
  }, [articleId, navigate])

  const doSave = useCallback(async (quiet = false) => {
    if (!title.trim()) return null
    if (!quiet) setSaving(true)
    setError(null)
    if (!quiet) setStatusMessage(null)

    const { html, json } = getContent()

    try {
      let saved: BlogArticle
      if (savedArticleId.current) {
        saved = await blogApi.updateArticle(savedArticleId.current, {
          title: title.trim(),
          subtitle: subtitle.trim(),
          content: html,
          bodyJson: json,
          excerpt: excerpt.trim(),
          coverUrl: coverUrl.trim(),
          visibility,
        })
      } else {
        saved = await blogApi.createArticle({
          sectionId,
          title: title.trim(),
          subtitle: subtitle.trim(),
          content: html,
          bodyJson: json,
          excerpt: excerpt.trim(),
          visibility,
        })
        savedArticleId.current = saved.id
        syncEditorRoute(saved.id)
      }

      if (selectedTagIds.length > 0) {
        await blogApi.setArticleTags(saved.id, selectedTagIds)
      }

      setArticle(saved)
      setLastSaved(new Date())
      if (!quiet) setStatusMessage('Draft saved. You can keep editing here.')
      return saved
    } catch {
      if (!quiet) setError('Failed to save. Please try again.')
      return null
    } finally {
      if (!quiet) setSaving(false)
    }
  }, [coverUrl, excerpt, getContent, sectionId, selectedTagIds, subtitle, syncEditorRoute, title, visibility])

  const scheduleAutosave = useCallback(() => {
    if (autosaveTimer.current) clearTimeout(autosaveTimer.current)
    autosaveTimer.current = setTimeout(() => { void doSave(true) }, AUTOSAVE_MS)
  }, [doSave])

  const handleSubmit = async () => {
    const saved = await doSave(true)
    const current = saved ?? article
    if (savedArticleId.current && current) {
      await blogApi.setStatus(savedArticleId.current, 'submitted')
      setSubmitConfirm(false)
      navigate('/blog/' + current.slug)
    }
  }

  const applyLink = () => {
    if (!linkUrl) {
      editor?.chain().focus().unsetLink().run()
      return
    }
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

  const handleImageFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setImageUploading(true)
    try {
      const record = await uploadToS3(file, 'blog', savedArticleId.current ?? undefined)
      editor?.chain().focus().setImage({ src: record.url }).run()
    } finally {
      setImageUploading(false)
      if (imageFileRef.current) imageFileRef.current.value = ''
    }
  }

  const switchToMarkdown = () => {
    if (editorMode === 'markdown') return
    const richText = editor?.getText({ blockSeparator: '\n\n' }) ?? ''
    setMarkdownText(prev => prev || richText)
    setEditorMode('markdown')
    setPreview(true)
  }

  const switchToRich = () => {
    if (editorMode === 'rich') return
    editor?.commands.setContent(markdownHtml)
    setEditorMode('rich')
  }

  const toggleTag = (id: string) =>
    setSelectedTagIds(ids => ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id])

  const filteredTags = allTags.filter(t => t.name.toLowerCase().includes(tagSearch.toLowerCase()))
  const selectedTagNames = allTags.filter(t => selectedTagIds.includes(t.id))
  const isReadOnly = article?.status === 'submitted' || article?.status === 'under_review'
  const wordCount = editorMode === 'markdown'
    ? markdownText.trim().split(/\s+/).filter(Boolean).length
    : (editor?.storage.characterCount?.words() ?? 0)

  return (
    <div className="blog-editor-shell">
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
          <button className={`blog-tab${editorMode === 'rich' ? ' active' : ''}`} onClick={switchToRich}>
            Rich Text
          </button>
          <button className={`blog-tab${editorMode === 'markdown' ? ' active' : ''}`} onClick={switchToMarkdown}>
            Markdown
          </button>
          <button className="tiptap-toolbar-btn icon-only" onClick={() => setShowUpload(true)} title="Import document">
            <Upload size={16} />
          </button>
          <button
            className="tiptap-toolbar-btn icon-only"
            onClick={() => setPreview(value => !value)}
            title={preview ? 'Hide preview' : 'Show preview'}
          >
            {preview ? <EyeOff size={16} /> : <Eye size={16} />}
          </button>
          <button className="tiptap-action-btn secondary" onClick={() => void doSave()} disabled={saving || !title.trim()}>
            <Save size={14} /> {saving ? 'Saving...' : savedArticleId.current ? 'Save Draft' : 'Create Draft'}
          </button>
          {!isReadOnly && (
            <button className="tiptap-action-btn primary" onClick={() => setSubmitConfirm(true)} disabled={!title.trim()}>
              <Send size={14} /> Submit
            </button>
          )}
        </div>
      </div>

      {error && <div className="tiptap-error">{error} <button onClick={() => setError(null)}><X size={14} /></button></div>}
      {statusMessage && <div className="tiptap-success">{statusMessage} <button onClick={() => setStatusMessage(null)}><X size={14} /></button></div>}

      <div className="blog-editor-body">
        <div className="blog-editor-main">
          <input
            className="blog-editor-title-input"
            value={title}
            onChange={e => setTitle(e.target.value)}
            placeholder="Article title"
            maxLength={200}
            disabled={isReadOnly}
          />
          <input
            className="blog-editor-subtitle-input"
            value={subtitle}
            onChange={e => setSubtitle(e.target.value)}
            placeholder="Subtitle (optional)"
            maxLength={300}
            disabled={isReadOnly}
          />
          {!article && (
            <p className="blog-editor-helper-text">
              Add the title, then create the draft once. After that, you stay here and keep editing.
            </p>
          )}

          {editorMode === 'rich' && !preview && !isReadOnly && (
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
                <button className={`tiptap-toolbar-btn${imageUploading ? ' uploading' : ''}`}
                  onMouseDown={e => { e.preventDefault(); imageFileRef.current?.click() }}
                  title="Upload image" disabled={imageUploading}>
                  <Upload size={15} />
                </button>
                <input ref={imageFileRef} type="file" accept="image/*" style={{ display: 'none' }} onChange={handleImageFileUpload} />
                <button className={`tiptap-toolbar-btn${editor?.isActive('codeBlock') ? ' active' : ''}`}
                  onMouseDown={e => { e.preventDefault(); editor?.chain().focus().toggleCodeBlock().run() }} title="Code block">
                  {'</>'}
                </button>
              </div>
            </div>
          )}

          {editorMode === 'rich' && showLinkInput && (
            <div className="tiptap-inline-input-row">
              <input
                value={linkUrl}
                onChange={e => setLinkUrl(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && applyLink()}
                placeholder="https://..."
                className="tiptap-inline-input"
                autoFocus
              />
              <button className="tiptap-action-btn primary small" onClick={applyLink}>Apply</button>
              <button className="tiptap-action-btn secondary small" onClick={() => setShowLinkInput(false)}>Cancel</button>
            </div>
          )}

          {editorMode === 'rich' && showImageInput && (
            <div className="tiptap-inline-input-row">
              <input
                value={imageUrl}
                onChange={e => setImageUrl(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && applyImage()}
                placeholder="Image URL..."
                className="tiptap-inline-input"
                autoFocus
              />
              <button className="tiptap-action-btn primary small" onClick={applyImage}>Insert</button>
              <button className="tiptap-action-btn secondary small" onClick={() => setShowImageInput(false)}>Cancel</button>
            </div>
          )}

          {editorMode === 'markdown' ? (
            <div className={`blog-markdown-shell${preview ? ' split' : ''}`}>
              <textarea
                className="blog-markdown-textarea"
                value={markdownText}
                onChange={e => setMarkdownText(e.target.value)}
                placeholder="Write markdown here..."
                disabled={isReadOnly}
              />
              {preview && (
                <div className="blog-markdown-preview blog-article-content">
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>{markdownText || '_Preview appears here._'}</ReactMarkdown>
                </div>
              )}
            </div>
          ) : preview ? (
            <div className="blog-article-content tiptap-preview-area" dangerouslySetInnerHTML={{ __html: editor?.getHTML() ?? '' }} />
          ) : (
            <div className={`tiptap-editor-wrap${isReadOnly ? ' readonly' : ''}`}>
              <EditorContent editor={editor} />
            </div>
          )}

          <div className="tiptap-word-count">
            {wordCount.toLocaleString()} words · {readingTime(getContent().text)}
          </div>
        </div>

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
            <textarea
              value={excerpt}
              onChange={e => setExcerpt(e.target.value)}
              placeholder="Short summary shown in article listings"
              maxLength={300}
              rows={3}
              disabled={isReadOnly}
              className="blog-editor-sidebar-textarea"
            />
          </div>

          <div className="blog-editor-sidebar-section">
            <label className="blog-editor-sidebar-label">Cover image URL</label>
            <input value={coverUrl} onChange={e => setCoverUrl(e.target.value)} placeholder="https://..." disabled={isReadOnly} />
            {coverUrl && <img src={coverUrl} alt="" className="blog-editor-cover-preview" />}
          </div>

          <div className="blog-editor-sidebar-section">
            <label className="blog-editor-sidebar-label">Visibility</label>
            <select value={visibility} onChange={e => setVisibility(e.target.value as typeof visibility)} disabled={isReadOnly}>
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
                <button className="tiptap-action-btn secondary small" onClick={() => setShowTagPicker(v => !v)}>
                  {showTagPicker ? 'Close' : '+ Add tags'}
                </button>
                {showTagPicker && (
                  <div className="blog-tag-picker">
                    <input
                      value={tagSearch}
                      onChange={e => setTagSearch(e.target.value)}
                      placeholder="Search tags..."
                      className="blog-tag-search"
                      autoFocus
                    />
                    <div className="blog-tag-list">
                      {filteredTags.map(t => (
                        <button
                          key={t.id}
                          className={`blog-tag-option${selectedTagIds.includes(t.id) ? ' selected' : ''}`}
                          onClick={() => toggleTag(t.id)}
                        >
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

      {showUpload && (
        <BlogUploadModal
          onClose={() => setShowUpload(false)}
          onContentReady={(html, filename) => {
            if (editorMode === 'markdown') {
              setMarkdownText(prev => prev || `# ${filename.replace(/\.[^.]+$/, '')}\n\n`)
            } else {
              editor?.commands.setContent(html)
            }
            if (!title.trim()) setTitle(filename.replace(/\.[^.]+$/, ''))
            setShowUpload(false)
          }}
        />
      )}

      {submitConfirm && (
        <div className="tiptap-modal-overlay" onClick={() => setSubmitConfirm(false)}>
          <div className="tiptap-modal" onClick={e => e.stopPropagation()}>
            <h3>Submit for review?</h3>
            <p>Your article will be sent to a moderator for review before publishing. You will not be able to edit it while it is under review.</p>
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
