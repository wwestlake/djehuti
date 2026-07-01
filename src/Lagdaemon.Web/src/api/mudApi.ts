const BASE = '/djehuti/api/mud'

export interface MudExitView {
  direction: string
  label?: string
  targetRoomId: string
  targetRoomName: string
}

export interface MudRoomState {
  characterId: string
  characterName: string
  roomId: string
  roomName: string
  roomDescription?: string
  zoneName: string
  mudTierName: string
  exits: MudExitView[]
}

export interface MudCommandResult {
  success: boolean
  command: string
  message: string
  state?: MudRoomState
}

const opts = { credentials: 'include' as RequestCredentials }

export const mudApi = {
  getMe: (): Promise<MudRoomState | null> =>
    fetch(`${BASE}/me`, opts).then(r => r.ok ? r.json() : null),

  command: (command: string): Promise<MudCommandResult> =>
    fetch(`${BASE}/command`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ command }),
    }).then(r => r.json()),
}
