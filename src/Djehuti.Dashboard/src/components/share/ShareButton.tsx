import { useState } from 'react'
import {
  Share2,
  Link as LinkIcon,
  Mail,
  MessageSquare,
  MessageCircle,
  Send,
  Check,
} from 'lucide-react'

export type ShareButtonProps = {
  title: string
  text?: string
  url?: string
  className?: string
}

// lucide-react doesn't ship brand/logo marks, so these three use small inline
// glyphs instead of pulling in a third-party icon package.
function FacebookGlyph() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M22 12.06C22 6.5 17.52 2 12 2S2 6.5 2 12.06c0 5 3.66 9.15 8.44 9.94v-7.03H7.9v-2.9h2.54V9.84c0-2.5 1.49-3.89 3.77-3.89 1.1 0 2.24.2 2.24.2v2.46h-1.26c-1.24 0-1.63.78-1.63 1.58v1.89h2.78l-.44 2.9h-2.34V22c4.78-.79 8.44-4.94 8.44-9.94Z" />
    </svg>
  )
}

function XGlyph() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M18.9 2h3.1l-6.78 7.75L23 22h-6.6l-5.17-6.77L5 22H1.9l7.26-8.3L2 2h6.76l4.67 6.17L18.9 2Zm-1.09 18h1.72L8.26 3.9H6.42L17.81 20Z" />
    </svg>
  )
}

function LinkedInGlyph() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M20.45 20.45h-3.56v-5.57c0-1.33-.02-3.03-1.85-3.03-1.85 0-2.14 1.44-2.14 2.94v5.66H9.35V9h3.41v1.56h.05c.48-.9 1.64-1.85 3.38-1.85 3.6 0 4.27 2.37 4.27 5.46v6.28ZM5.34 7.43a2.07 2.07 0 1 1 0-4.15 2.07 2.07 0 0 1 0 4.15ZM7.12 20.45H3.56V9h3.56v11.45Z" />
    </svg>
  )
}

function WhatsAppGlyph() {
  return <MessageCircle size={14} />
}

function TelegramGlyph() {
  return <Send size={14} />
}

type ShareLink = {
  key: string
  label: string
  icon: () => JSX.Element
  href: string
}

/**
 * Reusable share control. Uses the native Web Share API where supported
 * (mobile OS share sheet); otherwise opens a compact fallback menu with
 * copy-link, email, SMS, and social share links for the current page.
 */
export function ShareButton({ title, text, url, className }: ShareButtonProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [copied, setCopied] = useState(false)

  const shareUrl = url ?? window.location.href
  const shareText = text ?? title

  const canNativeShare = typeof navigator !== 'undefined' && typeof navigator.share === 'function'

  async function handleClick() {
    if (canNativeShare) {
      try {
        await navigator.share({ title, text: shareText, url: shareUrl })
      } catch (err) {
        if ((err as Error)?.name !== 'AbortError') {
          console.error('Share failed', err)
        }
      }
      return
    }
    setIsOpen((open) => !open)
  }

  async function copyLink() {
    try {
      await navigator.clipboard.writeText(shareUrl)
      setCopied(true)
      setTimeout(() => setCopied(false), 1800)
    } catch (err) {
      console.error('Copy to clipboard failed', err)
    }
  }

  const encodedUrl = encodeURIComponent(shareUrl)
  const encodedTitle = encodeURIComponent(title)
  const encodedText = encodeURIComponent(shareText)

  const links: ShareLink[] = [
    { key: 'facebook', label: 'Facebook', icon: FacebookGlyph, href: `https://www.facebook.com/sharer/sharer.php?u=${encodedUrl}` },
    { key: 'twitter', label: 'X (Twitter)', icon: XGlyph, href: `https://twitter.com/intent/tweet?text=${encodedTitle}&url=${encodedUrl}` },
    { key: 'linkedin', label: 'LinkedIn', icon: LinkedInGlyph, href: `https://www.linkedin.com/sharing/share-offsite/?url=${encodedUrl}` },
    { key: 'whatsapp', label: 'WhatsApp', icon: WhatsAppGlyph, href: `https://wa.me/?text=${encodedText}%20${encodedUrl}` },
    { key: 'telegram', label: 'Telegram', icon: TelegramGlyph, href: `https://t.me/share/url?url=${encodedUrl}&text=${encodedTitle}` },
  ]

  return (
    <div className={`share-button-container ${className ?? ''}`.trim()}>
      <button
        className="icon-button share-button-trigger"
        type="button"
        onClick={handleClick}
        title="Share this page"
        aria-label="Share this page"
      >
        <Share2 size={18} />
        <span className="share-button-label">Share</span>
      </button>

      {isOpen && !canNativeShare && (
        <div className="share-menu-dropdown" role="menu">
          <button className="share-menu-item" type="button" onClick={copyLink} role="menuitem">
            {copied ? <Check size={14} /> : <LinkIcon size={14} />}
            <span>{copied ? 'Copied!' : 'Copy link'}</span>
          </button>

          <a
            className="share-menu-item"
            role="menuitem"
            href={`mailto:?subject=${encodedTitle}&body=${encodeURIComponent(`${shareText}\n\n${shareUrl}`)}`}
            onClick={() => setIsOpen(false)}
          >
            <Mail size={14} />
            <span>Email</span>
          </a>

          <a
            className="share-menu-item"
            role="menuitem"
            href={`sms:?body=${encodeURIComponent(`${shareText} ${shareUrl}`)}`}
            onClick={() => setIsOpen(false)}
          >
            <MessageSquare size={14} />
            <span>SMS</span>
          </a>

          <div className="share-menu-divider" />

          {links.map(({ key, label, icon: Icon, href }) => (
            <a
              key={key}
              className="share-menu-item"
              role="menuitem"
              href={href}
              target="_blank"
              rel="noopener noreferrer"
              onClick={() => setIsOpen(false)}
            >
              <Icon />
              <span>{label}</span>
            </a>
          ))}
        </div>
      )}

      {isOpen && !canNativeShare && (
        <button className="share-menu-backdrop" type="button" onClick={() => setIsOpen(false)} aria-label="Close share menu" />
      )}
    </div>
  )
}
