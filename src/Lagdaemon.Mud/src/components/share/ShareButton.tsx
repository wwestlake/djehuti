import { useEffect, useRef, useState } from 'react'
import { Check, Copy, Facebook, Linkedin, Mail, MessageCircle, MessageSquare, Send, Share2, Twitter } from 'lucide-react'

export interface ShareButtonProps {
  /** Page title used by the native share sheet and as link/tweet text. */
  title: string
  /** Optional longer description; falls back to `title`. */
  text?: string
  /** Exact URL to share; defaults to the current page location. */
  url?: string
  className?: string
}

/**
 * Reusable share control: uses the native OS share sheet where available
 * (`navigator.share`), otherwise opens a compact fallback menu with the
 * usual link/email/sms/social destinations. Client-side only -- no backend,
 * no third-party share widget.
 */
export default function ShareButton({ title, text, url, className }: ShareButtonProps) {
  const [open, setOpen] = useState(false)
  const [copied, setCopied] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)

  const shareUrl = url ?? (typeof window !== 'undefined' ? window.location.href : '')
  const shareText = text ?? title
  const canNativeShare = typeof navigator !== 'undefined' && typeof navigator.share === 'function'

  useEffect(() => {
    if (!open) return
    const onDocClick = (event: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) setOpen(false)
    }
    const onEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false)
    }
    document.addEventListener('mousedown', onDocClick)
    document.addEventListener('keydown', onEscape)
    return () => {
      document.removeEventListener('mousedown', onDocClick)
      document.removeEventListener('keydown', onEscape)
    }
  }, [open])

  const copyLink = async () => {
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(shareUrl)
      } else {
        const el = document.createElement('textarea')
        el.value = shareUrl
        el.style.position = 'fixed'
        el.style.opacity = '0'
        document.body.appendChild(el)
        el.select()
        document.execCommand('copy')
        document.body.removeChild(el)
      }
      setCopied(true)
      setTimeout(() => setCopied(false), 1800)
    } catch {
      setCopied(false)
    }
  }

  const handleTriggerClick = async () => {
    if (canNativeShare) {
      try {
        await navigator.share({ title, text: shareText, url: shareUrl })
      } catch (err) {
        if ((err as DOMException)?.name !== 'AbortError') setOpen(true)
      }
      return
    }
    setOpen(prev => !prev)
  }

  const encodedUrl = encodeURIComponent(shareUrl)
  const encodedTitle = encodeURIComponent(title)
  const encodedTextWithUrl = encodeURIComponent(`${shareText} ${shareUrl}`)

  const links = [
    { key: 'email', label: 'Email', icon: Mail, href: `mailto:?subject=${encodedTitle}&body=${encodeURIComponent(`${shareText}\n${shareUrl}`)}` },
    { key: 'sms', label: 'SMS', icon: MessageSquare, href: `sms:?body=${encodedTextWithUrl}` },
    { key: 'facebook', label: 'Facebook', icon: Facebook, href: `https://www.facebook.com/sharer/sharer.php?u=${encodedUrl}`, external: true },
    { key: 'twitter', label: 'X', icon: Twitter, href: `https://twitter.com/intent/tweet?text=${encodedTitle}&url=${encodedUrl}`, external: true },
    { key: 'linkedin', label: 'LinkedIn', icon: Linkedin, href: `https://www.linkedin.com/sharing/share-offsite/?url=${encodedUrl}`, external: true },
    { key: 'whatsapp', label: 'WhatsApp', icon: MessageCircle, href: `https://wa.me/?text=${encodedTextWithUrl}`, external: true },
    { key: 'telegram', label: 'Telegram', icon: Send, href: `https://t.me/share/url?url=${encodedUrl}&text=${encodedTitle}`, external: true },
  ]

  return (
    <div className={`mud-share${className ? ` ${className}` : ''}`} ref={rootRef}>
      <button
        type="button"
        className="mud-share-trigger"
        onClick={handleTriggerClick}
        aria-haspopup={canNativeShare ? undefined : 'menu'}
        aria-expanded={canNativeShare ? undefined : open}
      >
        <Share2 size={16} />
        <span>Share</span>
      </button>

      {!canNativeShare && open && (
        <div className="mud-share-menu" role="menu">
          <button type="button" className="mud-share-menu-item" role="menuitem" onClick={copyLink}>
            {copied ? <Check size={15} /> : <Copy size={15} />}
            <span>{copied ? 'Copied!' : 'Copy link'}</span>
          </button>
          {links.map(({ key, label, icon: Icon, href, external }) => (
            <a
              key={key}
              className="mud-share-menu-item"
              role="menuitem"
              href={href}
              target={external ? '_blank' : undefined}
              rel={external ? 'noopener noreferrer' : undefined}
              onClick={() => setOpen(false)}
            >
              <Icon size={15} />
              <span>{label}</span>
            </a>
          ))}
        </div>
      )}
    </div>
  )
}
