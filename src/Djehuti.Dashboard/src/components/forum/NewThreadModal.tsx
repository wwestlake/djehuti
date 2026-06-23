import { useState } from 'react'
import { forumApi } from './forumApi'
import type { ForumThread } from './forumApi'

interface Props {
  forumId: string
  onCreated: (thread: ForumThread) => void
  onClose: () => void
}

export default function NewThreadModal({ forumId, onCreated, onClose }: Props) {
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!title.trim() || !content.trim()) return
    setSubmitting(true)
    setError(null)
    try {
      const thread = await forumApi.createThread(forumId, title.trim(), content.trim())
      onCreated(thread)
    } catch {
      setError('Failed to create thread. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-box" onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <h2>New Thread</h2>
          <button className="modal-close" onClick={onClose}>✕</button>
        </div>
        <form onSubmit={handleSubmit} className="modal-form">
          <label>
            Title
            <input
              type="text"
              value={title}
              onChange={e => setTitle(e.target.value)}
              placeholder="Thread title"
              required
              maxLength={200}
            />
          </label>
          <label>
            Post
            <textarea
              value={content}
              onChange={e => setContent(e.target.value)}
              placeholder="Write your post (Markdown supported)"
              required
              rows={8}
            />
          </label>
          {error && <p className="form-error">{error}</p>}
          <div className="modal-actions">
            <button type="button" onClick={onClose} disabled={submitting}>Cancel</button>
            <button type="submit" className="btn-primary" disabled={submitting || !title.trim() || !content.trim()}>
              {submitting ? 'Posting…' : 'Post Thread'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
