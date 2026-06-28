interface Props {
  tierName: string
  badgeColor: string
  badgeLabel: string
  size?: 'sm' | 'md'
}

export default function TierBadge({ tierName, badgeColor, badgeLabel, size = 'sm' }: Props) {
  return (
    <span
      className={`tier-badge tier-badge-${size}`}
      style={{ background: badgeColor }}
      title={tierName}
    >
      {badgeLabel}
    </span>
  )
}
