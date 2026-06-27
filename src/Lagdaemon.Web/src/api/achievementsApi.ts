const API = '/djehuti/api'
const opts = { credentials: 'include' as const }

export interface Achievement {
  id: string
  slug: string
  name: string
  description: string
  icon: string
  tier: 'bronze' | 'silver' | 'gold' | 'platinum' | 'legendary'
  category: string
  points: number
  hidden: boolean
}

export interface UserAchievement {
  id: string
  userId: string
  achievementId: string
  slug: string
  name: string
  description: string
  icon: string
  tier: string
  category: string
  points: number
  awardedAt: string
  notified: boolean
}

export interface UserMetrics {
  userId: string
  postCount: number
  threadCount: number
  voteReceived: number
  answerCount: number
  reactionCount: number
  daysActive: number
  loginStreak: number
  lastActiveDay: string | null
}

export const achievementsApi = {
  getDictionary: (): Promise<Achievement[]> =>
    fetch(`${API}/achievements`, opts).then(r => r.ok ? r.json() : []),

  getMyAchievements: (): Promise<UserAchievement[]> =>
    fetch(`${API}/users/me/achievements`, opts).then(r => r.ok ? r.json() : []),

  getUserAchievements: (userId: string): Promise<UserAchievement[]> =>
    fetch(`${API}/users/${userId}/achievements`, opts).then(r => r.ok ? r.json() : []),

  getMyMetrics: (): Promise<UserMetrics | null> =>
    fetch(`${API}/users/me/metrics`, opts).then(r => r.ok ? r.json() : null),

  adminRecompute: (): Promise<void> =>
    fetch(`${API}/admin/achievements/recompute`, { ...opts, method: 'POST' }).then(() => undefined),
}
