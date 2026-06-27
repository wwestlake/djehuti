import { useEffect, useState } from 'react'
import { achievementsApi } from '../../api/achievementsApi'
import type { UserAchievement, UserMetrics } from '../../api/achievementsApi'
import AchievementBadge from '../../components/achievements/AchievementBadge'
import { useAuth } from '../../contexts/AuthContext'

const TIER_ORDER = ['legendary', 'platinum', 'gold', 'silver', 'bronze']

const TIER_COLORS: Record<string, string> = {
  bronze:    '#cd7f32',
  silver:    '#a0a0a0',
  gold:      '#ffd700',
  platinum:  '#00ccdd',
  legendary: '#c084fc',
}

export default function AchievementsPage() {
  const { user } = useAuth()
  const [achievements, setAchievements] = useState<UserAchievement[]>([])
  const [metrics, setMetrics] = useState<UserMetrics | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!user) { setLoading(false); return }
    Promise.all([
      achievementsApi.getMyAchievements(),
      achievementsApi.getMyMetrics(),
    ]).then(([a, m]) => {
      setAchievements(a)
      setMetrics(m)
    }).finally(() => setLoading(false))
  }, [user])

  if (!user) return <div className="achievements-page"><p>Sign in to view your achievements.</p></div>
  if (loading) return <div className="achievements-page"><p>Loading…</p></div>

  const byTier = TIER_ORDER.map(tier => ({
    tier,
    items: achievements.filter(a => a.tier === tier),
  })).filter(g => g.items.length > 0)

  const totalPoints = achievements.reduce((s, a) => s + a.points, 0)

  return (
    <div className="achievements-page">
      <h1>Your Achievements</h1>

      {metrics && (
        <div className="metrics-grid">
          <div className="metric-card"><span className="metric-value">{metrics.postCount}</span><span className="metric-label">Posts</span></div>
          <div className="metric-card"><span className="metric-value">{metrics.threadCount}</span><span className="metric-label">Threads</span></div>
          <div className="metric-card"><span className="metric-value">{metrics.voteReceived}</span><span className="metric-label">Votes Received</span></div>
          <div className="metric-card"><span className="metric-value">{metrics.answerCount}</span><span className="metric-label">Answers</span></div>
          <div className="metric-card"><span className="metric-value">{metrics.daysActive}</span><span className="metric-label">Days Active</span></div>
          <div className="metric-card"><span className="metric-value">{metrics.loginStreak}</span><span className="metric-label">Login Streak</span></div>
        </div>
      )}

      <div className="achievements-summary">
        <span className="achievements-count">{achievements.length} badge{achievements.length !== 1 ? 's' : ''} earned</span>
        <span className="achievements-points">{totalPoints} pts</span>
      </div>

      {achievements.length === 0 ? (
        <p className="achievements-empty">No achievements yet — keep participating!</p>
      ) : (
        byTier.map(({ tier, items }) => (
          <div key={tier} className="achievement-tier-group">
            <h2 className="achievement-tier-heading" style={{ color: TIER_COLORS[tier] }}>
              {tier.charAt(0).toUpperCase() + tier.slice(1)}
            </h2>
            <div className="achievement-grid">
              {items.map(a => (
                <div key={a.id} className="achievement-card">
                  <AchievementBadge achievement={a} size="lg" />
                  <div className="achievement-card-body">
                    <div className="achievement-card-name">{a.name}</div>
                    <div className="achievement-card-desc">{a.description}</div>
                    <div className="achievement-card-meta">
                      <span className="achievement-card-pts">+{a.points} pts</span>
                      <span className="achievement-card-date">{new Date(a.awardedAt).toLocaleDateString()}</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        ))
      )}
    </div>
  )
}
