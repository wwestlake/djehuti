const BASE = '/djehuti/api/patreon'
const opts = { credentials: 'include' as const }
const json = (r: Response) => r.json()

export interface PatreonTier {
  tierId: string
  tierName: string
  amountCents: number
  displayOrder: number
  badgeColor: string
  badgeLabel: string
  description?: string
}

export interface UserTierInfo {
  userId: string
  tierId: string
  tierName: string
  badgeColor: string
  badgeLabel: string
}

export interface SupporterEntry {
  displayName: string
  tierId: string
  tierName: string
  badgeColor: string
  badgeLabel: string
  displayOrder: number
}

export const patreonApi = {
  getTiers: (): Promise<PatreonTier[]> =>
    fetch(`${BASE}/tiers`, opts).then(json),

  getSupporters: (): Promise<SupporterEntry[]> =>
    fetch(`${BASE}/supporters`, opts).then(json),

  getUserTiers: (userIds: string[]): Promise<UserTierInfo[]> =>
    fetch(`${BASE}/user-tiers?ids=${userIds.join(',')}`, opts).then(r => r.ok ? r.json() : []),
}
