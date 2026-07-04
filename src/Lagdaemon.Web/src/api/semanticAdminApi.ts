const BASE = '/djehuti/api/admin/semantic'
const opts = { credentials: 'include' as RequestCredentials }

const json = (r: Response) => {
  if (!r.ok) throw new Error(`${r.status} ${r.statusText}`)
  return r.json()
}

export interface SemanticGraphStats {
  documentCount: number
  chunkCount: number
  tokenCount: number
  tokenSplitCount: number
  embeddedChunkCount: number
  embeddingProvider: string
  embeddingReady: boolean
}

export interface SemanticTokenDispersionCandidate {
  token: string
  chunkCount: number
  documentCount: number
  sourceTypeCount: number
  neighborCount: number
  dispersionScore: number
  dispersionBand: string
}

export interface SemanticTokenSplitRecord {
  token: string
  scopeKind: string
  scopeValue: string
  variantKey: string
}

export interface SemanticTokenSplitProposalValue {
  scopeValue: string
  chunkCount: number
}

export interface SemanticTokenSplitProposal {
  token: string
  scopeKind: string
  chunkCount: number
  documentCount: number
  sourceTypeCount: number
  dispersionScore: number
  dispersionBand: string
  scopeValueCount: number
  scopeValues: SemanticTokenSplitProposalValue[]
  reason: string
}

export interface SemanticChunkHit {
  sourceType: string
  sourceKey: string
  title: string
  chunkPosition: number
  content: string
  matchedTokenCount: number
  matchedWeight: number
  similarity: number
}

export interface SemanticReindexSummary {
  documentsRequested: number
  documentsIndexed: number
  forumThreadsIndexed: number
  blogArticlesIndexed: number
  mudRoomsIndexed: number
  mudItemsIndexed: number
  mudRecipesIndexed: number
}

export const semanticAdminApi = {
  getStats: (): Promise<SemanticGraphStats> =>
    fetch(`${BASE}/stats`, opts).then(json),

  search: (query: string, sourceType?: string, limit = 10): Promise<SemanticChunkHit[]> => {
    const params = new URLSearchParams({ q: query, limit: String(limit) })
    if (sourceType && sourceType.trim()) params.set('sourceType', sourceType.trim())
    return fetch(`${BASE}/search?${params.toString()}`, opts).then(json)
  },

  getDispersionCandidates: (limit = 12, minChunkCount = 3): Promise<SemanticTokenDispersionCandidate[]> => {
    const params = new URLSearchParams({ limit: String(limit), minChunkCount: String(minChunkCount) })
    return fetch(`${BASE}/dispersion?${params.toString()}`, opts).then(json)
  },

  getTokenSplits: (): Promise<SemanticTokenSplitRecord[]> =>
    fetch(`${BASE}/splits`, opts).then(json),

  getTokenSplitProposals: (limit = 12, minChunkCount = 3): Promise<SemanticTokenSplitProposal[]> => {
    const params = new URLSearchParams({ limit: String(limit), minChunkCount: String(minChunkCount) })
    return fetch(`${BASE}/splits/proposals?${params.toString()}`, opts).then(json)
  },

  applyTokenSplitProposal: (payload: Pick<SemanticTokenSplitProposal, 'token' | 'scopeKind'>): Promise<{ created: number; rebuilt: number }> =>
    fetch(`${BASE}/splits/proposals/apply`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(json),

  materializeSourceTypeSplits: (limit = 12, minChunkCount = 3): Promise<{ created: number; rebuilt: number }> => {
    const params = new URLSearchParams({ limit: String(limit), minChunkCount: String(minChunkCount) })
    return fetch(`${BASE}/splits/materialize/source-types?${params.toString()}`, { ...opts, method: 'POST' }).then(json)
  },

  saveTokenSplit: (payload: SemanticTokenSplitRecord): Promise<{ saved: boolean; rebuilt: number }> =>
    fetch(`${BASE}/splits`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(json),

  deleteTokenSplit: (token: string, scopeKind: string, scopeValue: string): Promise<{ deleted: number; rebuilt: number }> => {
    const params = new URLSearchParams({ token, scopeKind, scopeValue })
    return fetch(`${BASE}/splits?${params.toString()}`, { ...opts, method: 'DELETE' }).then(json)
  },

  reindexIndexed: (): Promise<SemanticReindexSummary> =>
    fetch(`${BASE}/reindex/indexed`, { ...opts, method: 'POST' }).then(json),

  reindexMudRooms: (): Promise<{ indexed: number }> =>
    fetch(`${BASE}/reindex/mud/rooms`, { ...opts, method: 'POST' }).then(json),

  reindexMudItems: (): Promise<{ indexed: number }> =>
    fetch(`${BASE}/reindex/mud/items`, { ...opts, method: 'POST' }).then(json),

  reindexMudRecipes: (): Promise<{ indexed: number }> =>
    fetch(`${BASE}/reindex/mud/recipes`, { ...opts, method: 'POST' }).then(json),
}
