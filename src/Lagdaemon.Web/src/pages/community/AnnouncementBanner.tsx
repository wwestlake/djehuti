import { useEffect, useState } from 'react'

const BASE = '/djehuti'
const DISMISSED_KEY = 'ann_dismissed'

interface Announcement {
  id: string
  title: string
  body: string
  priority: number
  publishedAt: string | null
}

function getDismissed(): Set<string> {
  try { return new Set(JSON.parse(sessionStorage.getItem(DISMISSED_KEY) || '[]')) }
  catch { return new Set() }
}

function addDismissed(id: string) {
  const s = getDismissed(); s.add(id)
  sessionStorage.setItem(DISMISSED_KEY, JSON.stringify([...s]))
}

interface Props {
  onViewAll: () => void
}

export default function AnnouncementBanner({ onViewAll }: Props) {
  const [announcements, setAnnouncements] = useState<Announcement[]>([])
  const [dismissed, setDismissed] = useState<Set<string>>(getDismissed)

  useEffect(() => {
    fetch(`${BASE}/api/announcements?limit=3`, { credentials: 'include' })
      .then(r => r.ok ? r.json() : [])
      .then(setAnnouncements)
      .catch(() => {})
  }, [])

  const visible = announcements.filter(a => !dismissed.has(a.id))
  if (visible.length === 0) return null

  const dismiss = (id: string) => {
    addDismissed(id)
    setDismissed(getDismissed())
  }

  return (
    <div className="ann-banner">
      {visible.slice(0, 2).map(a => (
        <div key={a.id} className={`ann-banner-item${a.priority > 0 ? ' high-priority' : ''}`}>
          <div className="ann-banner-content">
            {a.priority > 0 && <span className="ann-priority-badge">Important</span>}
            <strong className="ann-banner-title">{a.title}</strong>
            <button className="ann-view-all-link" onClick={onViewAll}>View all →</button>
          </div>
          <button className="ann-dismiss-btn" onClick={() => dismiss(a.id)} aria-label="Dismiss">✕</button>
        </div>
      ))}
    </div>
  )
}
