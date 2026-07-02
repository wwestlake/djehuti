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

export interface MudRoomState {
  characterId: string
  characterName: string
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
  getMe: (): Promise<MudRoomState | null> =>
    fetch(`${BASE}/me`, opts).then(readJsonOrThrow<MudRoomState>),

  command: (command: string): Promise<MudCommandResult> =>
    fetch(`${BASE}/command`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ command }),
    }).then(readJsonOrThrow<MudCommandResult>),
}
