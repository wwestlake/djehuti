import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { MessageSquare, MessagesSquare, FileText } from 'lucide-react'
import { profileApi } from '../../api/profileApi'
import type { ActivityItem } from '../../api/profileApi'

const TYPE_META: Record<ActivityItem['type_'], { icon: typeof MessageSquare; label: string; href: (id: string) => string }> = {
  post: { icon: MessageSquare, label: 'replied in the forum', href: id => `/forum/thread/${id}` },
  thread: { icon: MessagesSquare, label: 'started a thread', href: id => `/forum/thread/${id}` },
  article: { icon: FileText, label: 'published an article', href: id => `/blog/${id}` },
}

export default function ActivityFeed({ userId }: { userId: string }) {
  const [items, setItems] = useState<ActivityItem[] | null>(null)

  useEffect(() => {
    let cancelled = false
    profileApi.getActivity(userId).then(feed => { if (!cancelled) setItems(feed.activity) }).catch(() => setItems([]))
    return () => { cancelled = true }
  }, [userId])

  if (items === null) return null
  if (items.length === 0) return <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>No activity yet.</p>

  return (
    <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 10 }}>
      {items.map(item => {
        const meta = TYPE_META[item.type_]
        const Icon = meta.icon
        return (
          <li key={`${item.type_}-${item.id}`}>
            <Link to={meta.href(item.id)} style={{ display: 'flex', alignItems: 'flex-start', gap: 10, textDecoration: 'none', color: 'inherit' }}>
              <Icon size={16} style={{ marginTop: 2, flexShrink: 0, color: 'var(--accent)' }} />
              <span style={{ fontSize: '0.88rem' }}>
                <span style={{ color: 'var(--text-muted)' }}>{meta.label}</span>
                {item.title && <> — <strong>{item.title}</strong></>}
                <span style={{ color: 'var(--text-muted)', marginLeft: 6, fontSize: '0.78rem' }}>
                  {new Date(item.createdAt).toLocaleDateString()}
                </span>
              </span>
            </Link>
          </li>
        )
      })}
    </ul>
  )
}
