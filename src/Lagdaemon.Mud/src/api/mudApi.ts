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
  visited: boolean
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
  currencyBalance: number
  currencyName: string
  currencyNamePlural: string
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

export interface MudCompanionSettings {
  characterId: string
  enabled: boolean
  mode: string
  model: string
  disclosure: string
  allowOnlineConcurrency: boolean
  useByoOpenAiKey: boolean
  hasByoOpenAiKey: boolean
  keyLastSetAt?: string
  lastStatus?: string
  lastError?: string
  eligible: boolean
  eligibilityReason: string
  updatedAt?: string
}

export interface MudRealmSummary {
  slug: string
  name: string
  description: string
  zoneCount: number
  roomCount: number
}

export interface MudLandingStats {
  roomCount: number
  zoneCount: number
  recipeCount: number
  realms: MudRealmSummary[]
}

export interface MudChatMessageView {
  id: string
  channel: string
  senderName: string
  recipientName?: string
  roomName?: string
  body: string
  createdAt: string
  self: boolean
}

export interface MudChatSyncView {
  messages: MudChatMessageView[]
  here: string[]
  onlineCount: number
  partyName?: string
  serverTime: string
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
  getLandingStats: (): Promise<MudLandingStats> =>
    fetch(`${BASE}/landing-stats`).then(readJsonOrThrow<MudLandingStats>),

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

  getCompanion: (characterId: string): Promise<MudCompanionSettings> =>
    fetch(`${BASE}/companions/${characterId}`, opts).then(readJsonOrThrow<MudCompanionSettings>),

  saveCompanion: (
    characterId: string,
    body: {
      enabled: boolean
      mode: string
      model: string
      disclosure: string
      allowOnlineConcurrency: boolean
      useByoOpenAiKey: boolean
      openAiApiKey?: string
    },
  ): Promise<MudCompanionSettings> =>
    fetch(`${BASE}/companions/${characterId}`, {
      ...opts,
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }).then(readJsonOrThrow<MudCompanionSettings>),

  removeCompanionKey: (characterId: string): Promise<MudCompanionSettings> =>
    fetch(`${BASE}/companions/${characterId}/key`, {
      ...opts,
      method: 'DELETE',
    }).then(readJsonOrThrow<MudCompanionSettings>),

  command: (command: string): Promise<MudCommandResult> =>
    fetch(`${BASE}/command`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ command }),
    }).then(readJsonOrThrow<MudCommandResult>),

  chatSync: (since?: string): Promise<MudChatSyncView | null> =>
    fetch(`${BASE}/chat/sync${since ? `?since=${encodeURIComponent(since)}` : ''}`, opts)
      .then(readJsonOrThrow<MudChatSyncView | null>),

  chatPost: (body: { channel: string; text: string; target?: string }): Promise<{ success: boolean; message: string }> =>
    fetch(`${BASE}/chat`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }).then(readJsonOrThrow<{ success: boolean; message: string }>),
}
