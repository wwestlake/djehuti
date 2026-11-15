import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import NoticeStrip, { type NoticeLevel } from '../../components/notices/NoticeStrip'

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

function levelFor(priority: number): NoticeLevel {
  if (priority >= 2) return 'urgent'
  if (priority >= 1) return 'important'
  return 'info'
}

export default function AnnouncementBanner() {
  const navigate = useNavigate()
  const onViewAll = () => navigate('/announcements')
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
    <NoticeStrip
      items={visible.slice(0, 2).map(a => ({
        id: a.id,
        level: levelFor(a.priority),
        title: a.title,
        linkLabel: 'View all',
        onLinkClick: onViewAll,
        onDismiss: () => dismiss(a.id),
      }))}
    />
  )
}
