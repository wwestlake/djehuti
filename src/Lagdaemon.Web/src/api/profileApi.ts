const BASE = '/djehuti/api/profiles'
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
}
