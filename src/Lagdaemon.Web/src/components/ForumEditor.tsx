import { useEditor, EditorContent, BubbleMenu } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import Underline from '@tiptap/extension-underline'
import Link from '@tiptap/extension-link'
import Placeholder from '@tiptap/extension-placeholder'
import CodeBlockLowlight from '@tiptap/extension-code-block-lowlight'
import { common, createLowlight } from 'lowlight'
import { Bold, Italic, UnderlineIcon, Strikethrough, Code, Link2, List, ListOrdered, Quote, Code2 } from 'lucide-react'
import { useState } from 'react'

const lowlight = createLowlight(common)

interface Props {
  placeholder?: string
  initialContent?: string
  onChange: (html: string) => void
  minHeight?: number
}

export default function ForumEditor({ placeholder, initialContent, onChange, minHeight = 160 }: Props) {
  const [linkUrl, setLinkUrl] = useState('')
  const [showLinkInput, setShowLinkInput] = useState(false)

  const editor = useEditor({
    extensions: [
      StarterKit.configure({ codeBlock: false }),
      Underline,
      Link.configure({ openOnClick: false, HTMLAttributes: { rel: 'noopener noreferrer' } }),
      CodeBlockLowlight.configure({ lowlight }),
      Placeholder.configure({ placeholder: placeholder ?? 'Write something…' }),
    ],
    content: initialContent ?? '',
    editorProps: {
      attributes: { class: 'tiptap-editor-content forum-editor-content', style: `min-height:${minHeight}px` },
    },
    onUpdate: ({ editor }) => onChange(editor.getHTML()),
  })

  if (!editor) return null

  const setLink = () => {
    if (!linkUrl) { editor.chain().focus().unsetLink().run(); setShowLinkInput(false); return }
    editor.chain().focus().setLink({ href: linkUrl }).run()
    setLinkUrl(''); setShowLinkInput(false)
  }

  return (
    <div className="forum-editor-wrap">
      <div className="tiptap-toolbar forum-toolbar">
        <button type="button" title="Bold" className={`tiptap-action-btn${editor.isActive('bold') ? ' active' : ''}`}
          onClick={() => editor.chain().focus().toggleBold().run()}><Bold size={14} /></button>
        <button type="button" title="Italic" className={`tiptap-action-btn${editor.isActive('italic') ? ' active' : ''}`}
          onClick={() => editor.chain().focus().toggleItalic().run()}><Italic size={14} /></button>
        <button type="button" title="Underline" className={`tiptap-action-btn${editor.isActive('underline') ? ' active' : ''}`}
          onClick={() => editor.chain().focus().toggleUnderline().run()}><UnderlineIcon size={14} /></button>
        <button type="button" title="Strikethrough" className={`tiptap-action-btn${editor.isActive('strike') ? ' active' : ''}`}
          onClick={() => editor.chain().focus().toggleStrike().run()}><Strikethrough size={14} /></button>
        <span className="tiptap-divider" />
        <button type="button" title="Inline code" className={`tiptap-action-btn${editor.isActive('code') ? ' active' : ''}`}
          onClick={() => editor.chain().focus().toggleCode().run()}><Code size={14} /></button>
        <button type="button" title="Code block" className={`tiptap-action-btn${editor.isActive('codeBlock') ? ' active' : ''}`}
          onClick={() => editor.chain().focus().toggleCodeBlock().run()}><Code2 size={14} /></button>
        <span className="tiptap-divider" />
        <button type="button" title="Bullet list" className={`tiptap-action-btn${editor.isActive('bulletList') ? ' active' : ''}`}
          onClick={() => editor.chain().focus().toggleBulletList().run()}><List size={14} /></button>
        <button type="button" title="Numbered list" className={`tiptap-action-btn${editor.isActive('orderedList') ? ' active' : ''}`}
          onClick={() => editor.chain().focus().toggleOrderedList().run()}><ListOrdered size={14} /></button>
        <button type="button" title="Blockquote" className={`tiptap-action-btn${editor.isActive('blockquote') ? ' active' : ''}`}
          onClick={() => editor.chain().focus().toggleBlockquote().run()}><Quote size={14} /></button>
        <span className="tiptap-divider" />
        <button type="button" title="Link" className={`tiptap-action-btn${editor.isActive('link') ? ' active' : ''}`}
          onClick={() => setShowLinkInput(v => !v)}><Link2 size={14} /></button>
        {showLinkInput && (
          <span className="tiptap-link-input-row">
            <input value={linkUrl} onChange={e => setLinkUrl(e.target.value)}
              placeholder="https://…" onKeyDown={e => e.key === 'Enter' && setLink()}
              className="tiptap-link-input" autoFocus />
            <button type="button" className="tiptap-action-btn" onClick={setLink}>OK</button>
          </span>
        )}
      </div>

      {editor && (
        <BubbleMenu editor={editor} tippyOptions={{ duration: 100 }}>
          <div className="tiptap-bubble-menu">
            <button type="button" className={`tiptap-action-btn${editor.isActive('bold') ? ' active' : ''}`}
              onClick={() => editor.chain().focus().toggleBold().run()}><Bold size={12} /></button>
            <button type="button" className={`tiptap-action-btn${editor.isActive('italic') ? ' active' : ''}`}
              onClick={() => editor.chain().focus().toggleItalic().run()}><Italic size={12} /></button>
            <button type="button" className={`tiptap-action-btn${editor.isActive('code') ? ' active' : ''}`}
              onClick={() => editor.chain().focus().toggleCode().run()}><Code size={12} /></button>
          </div>
        </BubbleMenu>
      )}

      <EditorContent editor={editor} />
    </div>
  )
}
