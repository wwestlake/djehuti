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
  exitType: string
  label?: string
  createdAt: string
}

export interface MudWorld {
  zones: MudZone[]
  rooms: MudRoom[]
  exits: MudExit[]
}

export interface MudRecipeIngredient {
  slug: string
  quantity: number
  position: number
}

export interface MudRecipe {
  id: string
  slug: string
  name: string
  outputName: string
  outputSlug: string
  outputDescription: string
  outputReadableText?: string
  position: number
  active: boolean
  createdAt: string
  ingredients: MudRecipeIngredient[]
}

export interface MudRealmMetric {
  realmSlug: string
  characterCount: number
}

export interface MudExitTypeMetric {
  exitType: string
  count: number
}

export interface MudAdminMetrics {
  zoneCount: number
  roomCount: number
  exitCount: number
  recipeCount: number
  itemCount: number
  portableItemCount: number
  readableItemCount: number
  activeCharacterCount: number
  retiredCharacterCount: number
  companionEnabledCount: number
  byoKeyCount: number
  emptyZoneCount: number
  deadEndRoomCount: number
  averageExitsPerRoom: number
  realmCharacterCounts: MudRealmMetric[]
  exitTypeCounts: MudExitTypeMetric[]
}

export const mudAdminApi = {
  getWorld: (): Promise<MudWorld> =>
    fetch(`${BASE}/world`, opts).then(json),

  getMetrics: (): Promise<MudAdminMetrics> =>
    fetch(`${BASE}/metrics`, opts).then(json),

  getRecipes: (): Promise<MudRecipe[]> =>
    fetch(`${BASE}/recipes`, opts).then(json),

  createRecipe: (fields: {
    slug: string
    name: string
    outputName: string
    outputSlug: string
    outputDescription: string
    outputReadableText?: string
    position: number
    active: boolean
    ingredients: MudRecipeIngredient[]
  }): Promise<MudRecipe> =>
    fetch(`${BASE}/recipes`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ...fields,
        outputReadableText: fields.outputReadableText ?? '',
      }),
    }).then(json),

  updateRecipe: (recipeId: string, fields: {
    slug: string
    name: string
    outputName: string
    outputSlug: string
    outputDescription: string
    outputReadableText?: string
    position: number
    active: boolean
    ingredients: MudRecipeIngredient[]
  }): Promise<MudRecipe> =>
    fetch(`${BASE}/recipes/${recipeId}`, {
      ...opts,
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ...fields,
        outputReadableText: fields.outputReadableText ?? '',
      }),
    }).then(json),

  deleteRecipe: (recipeId: string): Promise<void> =>
    fetch(`${BASE}/recipes/${recipeId}`, { ...opts, method: 'DELETE' }).then(() => {}),

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

  updateZone: (zoneId: string, fields: { name: string; slug: string; description?: string; position: number }): Promise<MudZone> =>
    fetch(`${BASE}/zones/${zoneId}`, {
      ...opts,
      method: 'PATCH',
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

  updateRoom: (roomId: string, fields: { zoneId: string; name: string; slug: string; description?: string; position: number }): Promise<MudRoom> =>
    fetch(`${BASE}/rooms/${roomId}`, {
      ...opts,
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        zoneId: fields.zoneId,
        name: fields.name,
        slug: fields.slug,
        description: fields.description ?? '',
        position: fields.position,
      }),
    }).then(json),

  createExit: (fields: { fromRoomId: string; toRoomId: string; direction: string; exitType?: string; label?: string }): Promise<MudExit> =>
    fetch(`${BASE}/exits`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        fromRoomId: fields.fromRoomId,
        toRoomId: fields.toRoomId,
        direction: fields.direction,
        exitType: fields.exitType ?? 'passage',
        label: fields.label ?? '',
      }),
    }).then(json),

  deleteExit: (exitId: string): Promise<void> =>
    fetch(`${BASE}/exits/${exitId}`, { ...opts, method: 'DELETE' }).then(() => {}),
}
