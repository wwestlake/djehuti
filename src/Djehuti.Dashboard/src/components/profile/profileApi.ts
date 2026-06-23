const BASE = '/api/profiles'

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

async function get(url: string) {
  const res = await fetch(url, { credentials: 'include' })
  if (!res.ok) throw new Error(res.statusText)
  return res.json()
}

export const profileApi = {
  getMe: (): Promise<UserProfile> => get(`${BASE}/me`),
  getUser: (userId: string): Promise<UserProfile> => get(`${BASE}/${userId}`),

  updateMe: async (input: UpdateProfileInput): Promise<UserProfile> => {
    const res = await fetch(`${BASE}/me`, {
      method: 'PUT',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
    })
    if (!res.ok) throw new Error(res.statusText)
    return res.json()
  },
}
