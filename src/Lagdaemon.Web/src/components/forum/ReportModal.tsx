import { useState } from 'react'
import { forumApi } from '../../api/forumApi'

interface Props {
  targetType: 'post' | 'thread'
  targetId: string
  onClose: () => void
}

const REASONS = [
  'Spam or self-promotion',
  'Harassment or hate speech',
  'Misinformation',
  'Off-topic or low quality',
  'Other',
]

export default function ReportModal({ targetType, targetId, onClose }: Props) {
  const [reason, setReason] = useState(REASONS[0])
  const [custom, setCustom] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [done, setDone] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    const finalReason = reason === 'Other' ? custom.trim() : reason
    if (!finalReason) return
    setSubmitting(true)
    try {
      await forumApi.reportContent(targetType, targetId, finalReason)
      setDone(true)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-box" onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Report {targetType}</h2>
          <button className="modal-close" onClick={onClose}>✕</button>
        </div>
        {done ? (
          <div className="modal-body">
            <p>Your report has been submitted. Thank you.</p>
            <button className="primary-action" onClick={onClose}>Close</button>
          </div>
        ) : (
          <form className="modal-body" onSubmit={handleSubmit}>
            <label>
              <span>Reason</span>
              <select value={reason} onChange={e => setReason(e.target.value)}>
                {REASONS.map(r => <option key={r} value={r}>{r}</option>)}
              </select>
            </label>
            {reason === 'Other' && (
              <label>
                <span>Details</span>
                <textarea
                  value={custom}
                  onChange={e => setCustom(e.target.value)}
                  placeholder="Describe the issue…"
                  rows={3}
                />
              </label>
            )}
            <div className="modal-actions">
              <button type="button" onClick={onClose}>Cancel</button>
              <button
                type="submit"
                className="primary-action"
                disabled={submitting || (reason === 'Other' && !custom.trim())}
              >
                {submitting ? 'Submitting…' : 'Submit Report'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  )
}
