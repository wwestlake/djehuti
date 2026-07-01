const BASE = '/djehuti/api/admin/mud'
const opts = { credentials: 'include' as RequestCredentials }

const json = (r: Response) => {
  if (!r.ok) throw new Error(`${r.status} ${r.statusText}`)
  return r.json()
}

export interface MudZone {
  id: string
  name: string
  slug: string
  description?: string
  position: number
  createdAt: string
}

export interface MudRoom {
  id: string
  zoneId: string
  zoneName: string
  zoneSlug: string
  name: string
  slug: string
  description?: string
  position: number
  createdAt: string
}

export interface MudExit {
  id: string
  fromRoomId: string
  fromRoomName: string
  fromRoomSlug: string
  toRoomId: string
  toRoomName: string
  toRoomSlug: string
  direction: string
  label?: string
  createdAt: string
}

export interface MudWorld {
  zones: MudZone[]
  rooms: MudRoom[]
  exits: MudExit[]
}

export const mudAdminApi = {
  getWorld: (): Promise<MudWorld> =>
    fetch(`${BASE}/world`, opts).then(json),

  createZone: (fields: { name: string; slug: string; description?: string; position: number }): Promise<MudZone> =>
    fetch(`${BASE}/zones`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: fields.name,
        slug: fields.slug,
        description: fields.description ?? '',
        position: fields.position,
      }),
    }).then(json),

  createRoom: (fields: { zoneId: string; name: string; slug: string; description?: string; position: number }): Promise<MudRoom> =>
    fetch(`${BASE}/rooms`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        zoneId: fields.zoneId,
        name: fields.name,
        slug: fields.slug,
        description: fields.description ?? '',
        position: fields.position,
      }),
    }).then(json),

  createExit: (fields: { fromRoomId: string; toRoomId: string; direction: string; label?: string }): Promise<MudExit> =>
    fetch(`${BASE}/exits`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        fromRoomId: fields.fromRoomId,
        toRoomId: fields.toRoomId,
        direction: fields.direction,
        label: fields.label ?? '',
      }),
    }).then(json),

  deleteExit: (exitId: string): Promise<void> =>
    fetch(`${BASE}/exits/${exitId}`, { ...opts, method: 'DELETE' }).then(() => {}),
}
