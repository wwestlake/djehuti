import { iconForPlatform } from '../SocialLinkIcon'
import type { ExternalLink } from '../../api/profileApi'

export default function SocialLinksDisplay({ links }: { links: ExternalLink[] }) {
  if (!links || links.length === 0) return null
  return (
    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
      {links.map((link, i) => {
        const Icon = iconForPlatform(link.platform)
        return (
          <a key={i} href={link.url} target="_blank" rel="noreferrer noopener" title={link.platform} style={{
            display: 'flex', alignItems: 'center', gap: 6, padding: '6px 12px', borderRadius: 20,
            background: 'var(--surface)', border: '1px solid var(--border)', fontSize: '0.82rem',
            color: 'inherit', textDecoration: 'none',
          }}>
            <Icon size={14} />
            <span>{link.platform}</span>
          </a>
        )
      })}
    </div>
  )
}
