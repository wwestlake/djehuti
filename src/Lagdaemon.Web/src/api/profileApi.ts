const BASE = '/djehuti/api/profiles'
const USERS_BASE = '/djehuti/api/users'
const opts = { credentials: 'include' as RequestCredentials }

export interface UserProfile {
  userId: string
  displayName: string | null
  bio: string | null
  avatarUrl: string | null
  website: string | null
  location: string | null
  createdAt: string
  updatedAt: string
}

export interface UpdateProfileInput {
  displayName: string
  bio: string
  avatarUrl: string
  website: string
  location: string
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

export const profileApi = {
  getMe: (): Promise<UserProfile> =>
    fetch(`${BASE}/me`, opts).then(r => { if (!r.ok) throw new Error(r.statusText); return r.json() }),

  getUser: (userId: string): Promise<UserProfile> =>
    fetch(`${BASE}/${userId}`, opts).then(r => { if (!r.ok) throw new Error(r.statusText); return r.json() }),

  updateMe: async (input: UpdateProfileInput): Promise<UserProfile> => {
    const res = await fetch(`${BASE}/me`, {
      ...opts,
      method: 'PUT',
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
}
