const BADGE_IMAGES: Record<string, string> = {
  'curious-mind':    '/badges/curious-mind.svg',
  'lab-assistant':   '/badges/lab-assistant.svg',
  'research-fellow': '/badges/research-fellow.svg',
  'professor':       '/badges/professor.svg',
  'dean':            '/badges/dean.svg',
}

interface Props {
  tierId: string
  tierName: string
  badgeColor: string
  badgeLabel: string
  size?: 'sm' | 'md'
}

export default function TierBadge({ tierId, tierName, size = 'sm' }: Props) {
  const src = BADGE_IMAGES[tierId]
  if (!src) return null
  const px = size === 'md' ? 40 : 24
  return (
    <img
      src={src}
      alt={tierName}
      title={tierName}
      width={px}
      height={px}
      className={`tier-badge-img tier-badge-img-${size}`}
    />
  )
}
