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
  embeddedChunkCount: number
  embeddingProvider: string
  embeddingReady: boolean
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
}

export const semanticAdminApi = {
  getStats: (): Promise<SemanticGraphStats> =>
    fetch(`${BASE}/stats`, opts).then(json),

  search: (query: string, sourceType?: string, limit = 10): Promise<SemanticChunkHit[]> => {
    const params = new URLSearchParams({ q: query, limit: String(limit) })
    if (sourceType && sourceType.trim()) params.set('sourceType', sourceType.trim())
    return fetch(`${BASE}/search?${params.toString()}`, opts).then(json)
  },

  reindexIndexed: (): Promise<SemanticReindexSummary> =>
    fetch(`${BASE}/reindex/indexed`, { ...opts, method: 'POST' }).then(json),
}
