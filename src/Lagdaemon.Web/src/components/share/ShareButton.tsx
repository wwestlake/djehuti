import { useEffect, useRef, useState } from 'react'
import { Share2, Link2, Mail, MessageSquare, Facebook, Linkedin, Check } from 'lucide-react'

export type ShareButtonProps = {
  /** Page title to share. */
  title: string
  /** Short description/body text to include where the destination supports it. */
  text?: string
  /** URL to share — defaults to the current page (including query params). */
  url?: string
  /** Extra class names for the wrapping element, for layout tweaks on a given page. */
  className?: string
  /** Icon-only trigger (no "Share" label) for tight spaces, e.g. inline per-post actions. */
  compact?: boolean
}

type MenuLink = {
  label: string
  icon: React.ReactNode
  href: string
  external?: boolean
}

/**
 * Reusable share control. Uses the native OS share sheet (Web Share API) when the
 * browser/device supports it; otherwise falls back to a small dropdown menu with
 * copy-link and the standard social/messaging targets. Works for anonymous visitors —
 * it only ever reads the current page's own title/url, nothing account-related.
 */
export default function ShareButton({ title, text, url, className, compact }: ShareButtonProps) {
  const [open, setOpen] = useState(false)
  const [copied, setCopied] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onDocClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDocClick)
    return () => document.removeEventListener('mousedown', onDocClick)
  }, [open])

  const shareUrl = url ?? (typeof window !== 'undefined' ? window.location.href : '')

  const handleClick = async () => {
    if (typeof navigator !== 'undefined' && navigator.share) {
      try {
        await navigator.share({ title, text, url: shareUrl })
      } catch (err) {
        // AbortError is just the user cancelling the OS share sheet — ignore it.
        if (err instanceof Error && err.name !== 'AbortError') {
          setOpen(true)
        }
      }
      return
    }
    setOpen(o => !o)
  }

  const copyLink = async () => {
    try {
      await navigator.clipboard.writeText(shareUrl)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      // Clipboard API unavailable (very old browser / insecure context) — nothing more we can do.
    }
  }

  const encodedUrl = encodeURIComponent(shareUrl)
  const encodedTitle = encodeURIComponent(title)
  const encodedText = encodeURIComponent(text ?? title)

  const links: MenuLink[] = [
    { label: 'Email', icon: <Mail size={15} />, href: `mailto:?subject=${encodedTitle}&body=${encodeURIComponent((text ? text + '\n\n' : '') + shareUrl)}` },
    { label: 'SMS', icon: <MessageSquare size={15} />, href: `sms:?body=${encodeURIComponent(title + ' ' + shareUrl)}` },
    { label: 'Facebook', icon: <Facebook size={15} />, href: `https://www.facebook.com/sharer/sharer.php?u=${encodedUrl}`, external: true },
    { label: 'X / Twitter', icon: <XIcon />, href: `https://twitter.com/intent/tweet?text=${encodedTitle}&url=${encodedUrl}`, external: true },
    { label: 'LinkedIn', icon: <Linkedin size={15} />, href: `https://www.linkedin.com/sharing/share-offsite/?url=${encodedUrl}`, external: true },
    { label: 'WhatsApp', icon: <WhatsAppIcon />, href: `https://wa.me/?text=${encodedText}%20${encodedUrl}`, external: true },
    { label: 'Telegram', icon: <TelegramIcon />, href: `https://t.me/share/url?url=${encodedUrl}&text=${encodedTitle}`, external: true },
  ]

  return (
    <div className={`share-wrap${className ? ' ' + className : ''}`} ref={ref}>
      <button
        type="button"
        className={`share-trigger${compact ? ' share-trigger-compact' : ''}`}
        onClick={handleClick}
        aria-label="Share this page"
        title="Share"
      >
        <Share2 size={compact ? 13 : 15} />
        {!compact && <span>Share</span>}
      </button>

      {open && (
        <div className="share-dropdown" role="menu">
          <button type="button" className="share-item" role="menuitem" onClick={copyLink}>
            {copied ? <Check size={15} /> : <Link2 size={15} />}
            <span>{copied ? 'Copied!' : 'Copy link'}</span>
          </button>
          {links.map(link => (
            <a
              key={link.label}
              className="share-item"
              role="menuitem"
              href={link.href}
              target={link.external ? '_blank' : undefined}
              rel={link.external ? 'noopener noreferrer' : undefined}
              onClick={() => setOpen(false)}
            >
              {link.icon}
              <span>{link.label}</span>
            </a>
          ))}
        </div>
      )}
    </div>
  )
}

function XIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M18.9 2H22l-7.6 8.7L23 22h-6.8l-5.3-6.9L4.7 22H1.6l8.1-9.3L1 2h6.9l4.8 6.3L18.9 2Zm-2.4 18h1.9L7.6 4H5.6l10.9 16Z" />
    </svg>
  )
}

function WhatsAppIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M17.47 14.38c-.3-.15-1.73-.85-2-.95-.27-.1-.47-.15-.66.15-.2.3-.76.95-.93 1.14-.17.2-.34.22-.63.08-.3-.15-1.24-.46-2.36-1.46-.87-.78-1.46-1.74-1.63-2.03-.17-.3-.02-.46.13-.6.15-.15.33-.38.5-.57.17-.2.22-.33.33-.56.11-.22.06-.4-.03-.56-.1-.15-.9-2.18-1.04-2.4-.14-.23-.28-.2-.4-.2h-.4c-.14 0-.35.05-.53.2-.18.15-.68.66-.68 1.6s.7 1.86.8 2 1.42 2.18 3.44 2.97c2.03.8 2.03.53 2.4.5.37-.03 1.2-.49 1.37-.96.17-.48.17-.89.12-.97-.05-.08-.18-.12-.37-.21Zm-5.46 6.44h-.01a8.8 8.8 0 0 1-4.5-1.23l-.32-.19-3.34.88.9-3.26-.21-.33a8.8 8.8 0 1 1 7.48 4.13Zm7.47-16.3A10.44 10.44 0 0 0 12 1.5 10.5 10.5 0 0 0 2.9 16.9L1.5 22.5l5.74-1.5a10.5 10.5 0 0 0 5.03 1.28h.01A10.5 10.5 0 0 0 23.5 11.5a10.46 10.46 0 0 0-3.02-7.4Z" />
    </svg>
  )
}

function TelegramIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M21.9 4.3 2.7 11.9c-1.3.5-1.3 1.2-.2 1.6l4.9 1.5 1.9 5.9c.2.6.5.8.9.8.3 0 .5-.1.7-.4l2.6-2.6 5 3.7c.9.5 1.5.2 1.7-.9l3.1-14.7c.3-1.3-.5-1.9-1.4-1.5Zm-4 3.5L9.6 14.1l-.3 3.6-1.5-4.6 12.6-7.9c.4-.3.7-.1.5.3Z" />
    </svg>
  )
}
