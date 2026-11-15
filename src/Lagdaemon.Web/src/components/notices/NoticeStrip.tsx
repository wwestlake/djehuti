export type NoticeLevel = 'info' | 'important' | 'urgent'

export interface NoticeItem {
  id: string
  level: NoticeLevel
  title: string
  linkLabel?: string
  onLinkClick?: () => void
  onDismiss?: () => void
}

const LEVEL_LABEL: Record<NoticeLevel, string | null> = {
  info: null,
  important: 'Important',
  urgent: 'Urgent',
}

interface NoticeStripProps {
  items: NoticeItem[]
  max?: number
}

export default function NoticeStrip({ items, max = 2 }: NoticeStripProps) {
  if (items.length === 0) return null

  return (
    <div className="notice-strip">
      {items.slice(0, max).map(item => {
        const label = LEVEL_LABEL[item.level]
        return (
          <div key={item.id} className={`notice-item notice-item--${item.level}`}>
            <span className="notice-dot" aria-hidden="true" />
            <div className="notice-content">
              {label && <span className={`notice-badge notice-badge--${item.level}`}>{label}</span>}
              <span className="notice-title">{item.title}</span>
              {item.onLinkClick && (
                <button className="notice-link" onClick={item.onLinkClick}>
                  {item.linkLabel ?? 'View all'}
                </button>
              )}
            </div>
            {item.onDismiss && (
              <button className="notice-dismiss" onClick={item.onDismiss} aria-label="Dismiss">✕</button>
            )}
          </div>
        )
      })}
    </div>
  )
}
