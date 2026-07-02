const BASE = '/djehuti/api/mud'

export interface MudExitView {
  direction: string
  exitType: string
  label?: string
  targetRoomId: string
  targetRoomName: string
}

export interface MudMapRoomView {
  roomId: string
  roomName: string
  slug: string
  x: number
  y: number
  current: boolean
}

export interface MudMapExitView {
  fromRoomId: string
  toRoomId: string
  direction: string
  exitType: string
  label?: string
}

export interface MudItemView {
  name: string
  slug: string
  description?: string
  portable: boolean
  readable: boolean
}

export interface MudStats {
  presence: number
  wit: number
  resolve: number
  lore: number
  craft: number
  guile: number
}

export interface MudSkills {
  searching: number
  crafting: number
  navigation: number
  lorekeeping: number
  negotiation: number
  devices: number
  survival: number
}

export interface MudRoomState {
  characterId: string
  characterName: string
  realmSlug: string
  realmName: string
  stats: MudStats
  skills: MudSkills
  roomId: string
  roomName: string
  roomDescription?: string
  zoneName: string
  mudTierName: string
  visibleItems: MudItemView[]
  inventoryItems: MudItemView[]
  mapRooms: MudMapRoomView[]
  mapExits: MudMapExitView[]
  exits: MudExitView[]
}

export interface MudCommandResult {
  success: boolean
  command: string
  message: string
  state?: MudRoomState
}

export interface MudRealmAvailability {
  realmSlug: string
  realmName: string
  characterCount: number
  freeStarterUsed: boolean
  canCreateFreeStarter: boolean
}

export interface MudCharacterSummary {
  id: string
  name: string
  displayName: string
  realmSlug: string
  realmName: string
  isSelected: boolean
  currentRoomName: string
  mudTierName: string
  inventoryCount: number
  stats: MudStats
  skills: MudSkills
  createdAt: string
}

export interface MudRosterView {
  selectedCharacterId?: string
  characters: MudCharacterSummary[]
  realms: MudRealmAvailability[]
  paidSlotsTotal: number
  paidSlotsUsed: number
  paidSlotsRemaining: number
  bonusSlots: number
}

const opts = { credentials: 'include' as RequestCredentials }

async function readJsonOrThrow<T>(response: Response): Promise<T> {
  const text = await response.text()
  const data = text
    ? (() => {
        try {
          return JSON.parse(text)
        } catch {
          return text
        }
      })()
    : null
  if (!response.ok) {
    const message =
      typeof data === 'string' ? data
      : data?.message ?? data?.detail ?? response.statusText ?? 'Request failed'
    throw new Error(message)
  }
  return data as T
}

export const mudApi = {
  getRoster: (): Promise<MudRosterView> =>
    fetch(`${BASE}/roster`, opts).then(readJsonOrThrow<MudRosterView>),

  getMe: (): Promise<MudRoomState | null> =>
    fetch(`${BASE}/me`, opts).then(readJsonOrThrow<MudRoomState>),

  createCharacter: (body: { realmSlug: string; name: string; displayName?: string }): Promise<MudRosterView> =>
    fetch(`${BASE}/characters`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }).then(readJsonOrThrow<MudRosterView>),

  selectCharacter: (characterId: string): Promise<MudRoomState> =>
    fetch(`${BASE}/characters/${characterId}/select`, {
      ...opts,
      method: 'POST',
    }).then(readJsonOrThrow<MudRoomState>),

  deleteCharacter: (characterId: string): Promise<MudRosterView> =>
    fetch(`${BASE}/characters/${characterId}`, {
      ...opts,
      method: 'DELETE',
    }).then(readJsonOrThrow<MudRosterView>),

  command: (command: string): Promise<MudCommandResult> =>
    fetch(`${BASE}/command`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ command }),
    }).then(readJsonOrThrow<MudCommandResult>),
}
