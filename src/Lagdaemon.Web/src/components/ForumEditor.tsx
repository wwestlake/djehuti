import { useEditor, EditorContent } from '@tiptap/react'
import { BubbleMenu } from '@tiptap/react/menus'
import StarterKit from '@tiptap/starter-kit'
import Underline from '@tiptap/extension-underline'
import Link from '@tiptap/extension-link'
import Placeholder from '@tiptap/extension-placeholder'
import CodeBlockLowlight from '@tiptap/extension-code-block-lowlight'
import Mention from '@tiptap/extension-mention'
import { common, createLowlight } from 'lowlight'
import { Bold, Italic, UnderlineIcon, Strikethrough, Code, Link2, List, ListOrdered, Quote, Code2 } from 'lucide-react'
import { useState, useRef, useEffect, useCallback } from 'react'
import type { SuggestionProps, SuggestionKeyDownProps } from '@tiptap/suggestion'

const lowlight = createLowlight(common)

interface UserSuggestion {
  id: string
  displayName: string
  avatarUrl?: string
}

interface MentionListProps {
  items: UserSuggestion[]
  command: (item: { id: string; label: string }) => void
}

function MentionList({ items, command }: MentionListProps) {
  const [selectedIndex, setSelectedIndex] = useState(0)

  const selectItem = useCallback((index: number) => {
    const item = items[index]
    if (item) command({ id: item.id, label: item.displayName })
  }, [items, command])

  useEffect(() => setSelectedIndex(0), [items])

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'ArrowUp') { e.preventDefault(); setSelectedIndex(i => (i + items.length - 1) % items.length) }
      if (e.key === 'ArrowDown') { e.preventDefault(); setSelectedIndex(i => (i + 1) % items.length) }
      if (e.key === 'Enter') { e.preventDefault(); selectItem(selectedIndex) }
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [items, selectedIndex, selectItem])

  if (!items.length) return null

  return (
    <div className="mention-list">
      {items.map((item, i) => (
        <button
          key={item.id}
          className={`mention-item${i === selectedIndex ? ' mention-item-selected' : ''}`}
          onMouseEnter={() => setSelectedIndex(i)}
          onClick={() => selectItem(i)}
          type="button"
        >
          {item.avatarUrl
            ? <img src={item.avatarUrl} className="mention-avatar" alt="" />
            : <span className="mention-avatar mention-avatar-placeholder">{item.displayName[0]?.toUpperCase()}</span>
          }
          <span>{item.displayName}</span>
        </button>
      ))}
    </div>
  )
}

interface Props {
  placeholder?: string
  initialContent?: string
  onChange: (html: string) => void
  minHeight?: number
}

export default function ForumEditor({ placeholder, initialContent, onChange, minHeight = 160 }: Props) {
  const [linkUrl, setLinkUrl] = useState('')
  const [showLinkInput, setShowLinkInput] = useState(false)
  const [mentionItems, setMentionItems] = useState<UserSuggestion[]>([])
  const [mentionCommand, setMentionCommand] = useState<((item: { id: string; label: string }) => void) | null>(null)
  const [mentionPos, setMentionPos] = useState<{ top: number; left: number } | null>(null)
  const popupRef = useRef<HTMLDivElement>(null)

  const editor = useEditor({
    extensions: [
      StarterKit.configure({ codeBlock: false }),
      Underline,
      Link.configure({ openOnClick: false, HTMLAttributes: { rel: 'noopener noreferrer' } }),
      CodeBlockLowlight.configure({ lowlight }),
      Placeholder.configure({ placeholder: placeholder ?? 'Write something…' }),
      Mention.configure({
        HTMLAttributes: { class: 'mention', 'data-mention': '' },
        renderHTML({ options, node }) {
          return ['span', { class: 'mention', 'data-mention': node.attrs.label, 'data-id': node.attrs.id }, `${options.suggestion.char}${node.attrs.label}`]
        },
        suggestion: {
          items: async ({ query }: { query: string }) => {
            if (query.length < 1) return []
            const res = await fetch(`/djehuti/api/users/search?q=${encodeURIComponent(query)}&limit=8`, { credentials: 'include' })
            if (!res.ok) return []
            return (await res.json()) as UserSuggestion[]
          },
          render: () => {
            let component: { props: SuggestionProps<UserSuggestion> } | null = null
            return {
              onStart: (props: SuggestionProps<UserSuggestion>) => {
                component = { props }
                const rect = props.clientRect?.()
                if (rect) setMentionPos({ top: rect.bottom + window.scrollY, left: rect.left + window.scrollX })
                setMentionItems(props.items as UserSuggestion[])
                setMentionCommand(() => props.command as (item: { id: string; label: string }) => void)
              },
              onUpdate: (props: SuggestionProps<UserSuggestion>) => {
                component = { props }
                const rect = props.clientRect?.()
                if (rect) setMentionPos({ top: rect.bottom + window.scrollY, left: rect.left + window.scrollX })
                setMentionItems(props.items as UserSuggestion[])
                setMentionCommand(() => props.command as (item: { id: string; label: string }) => void)
              },
              onExit: () => {
                component = null
                setMentionPos(null)
                setMentionItems([])
                setMentionCommand(null)
              },
              onKeyDown: (_props: SuggestionKeyDownProps) => false,
            }
          },
        },
      }),
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
        <BubbleMenu editor={editor}>
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

      {mentionPos && mentionCommand && mentionItems.length > 0 && (
        <div
          ref={popupRef}
          className="mention-popup"
          style={{ position: 'fixed', top: mentionPos.top, left: mentionPos.left, zIndex: 9999 }}
        >
          <MentionList items={mentionItems} command={mentionCommand} />
        </div>
      )}
    </div>
  )
}
