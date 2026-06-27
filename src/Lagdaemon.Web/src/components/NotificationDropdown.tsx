import { useEffect, useRef, useState } from 'react'
import { forumApi } from '../api/forumApi'
import type { Notification } from '../api/forumApi'

export default function NotificationDropdown() {
  const [open, setOpen] = useState(false)
  const [notifications, setNotifications] = useState<Notification[]>([])
  const [unreadCount, setUnreadCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  const load = async () => {
    setLoading(true)
    try {
      const { items, unreadCount: count } = await forumApi.getNotifications()
      setNotifications(items)
      setUnreadCount(count)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
    const interval = setInterval(load, 60_000)
    return () => clearInterval(interval)
  }, [])

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const markRead = async (n: Notification) => {
    if (!n.readAt) {
      await forumApi.markNotificationRead(n.id)
      setNotifications(prev => prev.map(x => x.id === n.id ? { ...x, readAt: new Date().toISOString() } : x))
      setUnreadCount(c => Math.max(0, c - 1))
    }
    if (n.link) window.location.href = n.link
  }

  const markAll = async () => {
    await forumApi.markAllNotificationsRead()
    setNotifications(prev => prev.map(x => ({ ...x, readAt: x.readAt ?? new Date().toISOString() })))
    setUnreadCount(0)
  }

  return (
    <div className="notif-wrap" ref={ref}>
      <button className="notif-bell" onClick={() => setOpen(o => !o)} aria-label="Notifications">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
          <path d="M13.73 21a2 2 0 0 1-3.46 0" />
        </svg>
        {unreadCount > 0 && (
          <span className="notif-badge">{unreadCount > 99 ? '99+' : unreadCount}</span>
        )}
      </button>

      {open && (
        <div className="notif-dropdown">
          <div className="notif-header">
            <span>Notifications</span>
            {unreadCount > 0 && (
              <button className="notif-mark-all" onClick={markAll}>Mark all read</button>
            )}
          </div>
          <div className="notif-list">
            {loading && notifications.length === 0 && (
              <div className="notif-empty">Loading…</div>
            )}
            {!loading && notifications.length === 0 && (
              <div className="notif-empty">No notifications yet.</div>
            )}
            {notifications.map(n => (
              <button
                key={n.id}
                className={`notif-item${n.readAt ? '' : ' notif-unread'}`}
                onClick={() => markRead(n)}
              >
                <div className="notif-body">{n.body}</div>
                <div className="notif-time">{new Date(n.createdAt).toLocaleDateString()}</div>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
