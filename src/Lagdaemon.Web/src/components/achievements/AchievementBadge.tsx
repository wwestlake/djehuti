import type { UserAchievement } from '../../api/achievementsApi'
import { DJEHUTI_SVG_BADGES } from './DjehutiSvgBadges'

const TIER_COLORS: Record<string, string> = {
  bronze:    '#cd7f32',
  silver:    '#a0a0a0',
  gold:      '#ffd700',
  platinum:  '#00ccdd',
  legendary: '#c084fc',
}

interface Props {
  achievement: UserAchievement
  size?: 'sm' | 'md' | 'lg'
  showLabel?: boolean
}

export default function AchievementBadge({ achievement, size = 'md', showLabel = false }: Props) {
  const color = TIER_COLORS[achievement.tier] ?? '#888'
  const px = size === 'sm' ? 28 : size === 'lg' ? 56 : 40
  const fontSize = size === 'sm' ? 14 : size === 'lg' ? 28 : 20

  return (
    <div className="achievement-badge-wrap" title={`${achievement.name} — ${achievement.description}`}>
      <div
        className={`achievement-badge achievement-badge-${size}`}
        style={{ '--badge-color': color, width: px, height: px, fontSize } as React.CSSProperties}
      >
        {DJEHUTI_SVG_BADGES[achievement.slug]
          ? <span dangerouslySetInnerHTML={{ __html: DJEHUTI_SVG_BADGES[achievement.slug] }} style={{ display: 'flex', width: '100%', height: '100%' }} />
          : achievement.icon}
      </div>
      {showLabel && (
        <div className="achievement-badge-label" style={{ color }}>
          {achievement.name}
        </div>
      )}
    </div>
  )
}
