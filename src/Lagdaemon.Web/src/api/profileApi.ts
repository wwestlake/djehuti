const USERS_BASE = '/djehuti/api/users'
const opts = { credentials: 'include' as RequestCredentials }

export interface ExternalLink {
  platform: string
  url: string
}

export interface UserProfile {
  displayName: string | null
  bio: string | null
  avatarUrl: string | null
  pronouns: string | null
  location: string | null
  externalLinks: ExternalLink[]
}

export interface UpdateProfileInput {
  displayName: string
  bio: string
  avatarUrl: string
  pronouns: string
  location: string
  externalLinks: ExternalLink[]
}

export interface PublicProfile {
  id: string
  displayName: string
  email: string
  bio: string | null
  avatarUrl: string | null
  pronouns: string | null
  location: string | null
  createdAt: string
  externalLinks: ExternalLink[]
  patreonTierId: string | null
}

export interface ActivityItem {
  type_: 'post' | 'thread' | 'article'
  id: string
  title: string
  createdAt: string
}

export interface ActivityFeed {
  activity: ActivityItem[]
}

export interface Achievement {
  id: string
  slug: string
  name: string
  description: string
  icon: string
  tier: string
  category: string
  points: number
  awardedAt: string
}

export const profileApi = {
  // "Me" = the signed-in user's own profile, same users-table-backed source
  // the public profile view and Settings' General tab both read from --
  // this used to point at a separate, disconnected user_profiles table that
  // nothing else ever read, so self-edits silently never showed up anywhere.
  getMe: (): Promise<UserProfile> =>
    fetch(`${USERS_BASE}/me/profile`, opts).then(r => { if (!r.ok) throw new Error(r.statusText); return r.json() }),

  updateMe: async (input: UpdateProfileInput): Promise<UserProfile> => {
    const res = await fetch(`${USERS_BASE}/me/profile`, {
      ...opts,
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
    })
    if (!res.ok) throw new Error(res.statusText)
    return res.json()
  },

  getPublicProfile: (userId: string): Promise<PublicProfile> =>
    fetch(`${USERS_BASE}/${userId}/public`, opts).then(r => { if (!r.ok) throw new Error(r.statusText); return r.json() }),

  getActivity: (userId: string): Promise<ActivityFeed> =>
    fetch(`${USERS_BASE}/${userId}/activity`, opts).then(r => { if (!r.ok) throw new Error(r.statusText); return r.json() }),

  getAchievements: (userId: string): Promise<Achievement[]> =>
    fetch(`${USERS_BASE}/${userId}/achievements`, opts).then(r => { if (!r.ok) throw new Error(r.statusText); return r.json() }),

  linkPatreonAccount: (memberId: string): Promise<{status: string; memberId: string}> => {
    return fetch(`${USERS_BASE}/patreon/link`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ memberId }),
    }).then(r => { if (!r.ok) throw new Error(r.statusText); return r.json() })
  },
}
