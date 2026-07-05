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

export interface SemanticAutomationStatus {
  syncEnabled: boolean
  syncIntervalSeconds: number
  autoSplitEnabled: boolean
  autoSplitIntervalSeconds: number
  autoSplitLimit: number
  autoSplitMinChunkCount: number
  autoSplitScopeKind: string | null
  graphBackfillLimit: number
  consecutiveDbFailures: number
  lastSyncAt: string | null
  lastSplitAt: string | null
  lastGraphBackfillCount: number
  lastAutoSplitCreatedCount: number
  lastAutoSplitProposalCount: number
}

export interface SemanticAutomationRunResult {
  forumThreadsRequested: number
  forumThreadsIndexed: number
  blogArticlesRequested: number
  blogArticlesIndexed: number
  mudRoomsRequested: number
  mudRoomsIndexed: number
  mudItemsRequested: number
  mudItemsIndexed: number
  mudRecipesRequested: number
  mudRecipesIndexed: number
  graphBackfilled: number
  autoSplitCreatedCount: number
  autoSplitProposalCount: number
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
  driftPressure: number
  adjustedMinChunkCount: number
  mediumBandEnabled: boolean
  reason: string
}

export interface SemanticDriftStatus {
  recentTurnCount: number
  driftSampleCount: number
  meanDrift: number
  highDriftCount: number
  highDriftRatio: number
  pressureMultiplier: number
  baseMinChunkCount: number
  adjustedMinChunkCount: number
  mediumBandEnabled: boolean
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
  rankingScore: number
  coOccurrenceWeightMultiplier: number
}

export interface SemanticCurvatureStatus {
  sampleCount: number
  curvature: number
  weightMultiplier: number
}

export interface SemanticRecoveryStatus {
  triggered: boolean
  reason: string
  similarityFloor: number
  candidateLimit: number
  resultLimit: number
  triggerScore: number
}

export interface SemanticQuerySessionSummary {
  id: string
  adminUserId: string
  turnCount: number
  lastQueryText: string | null
  lastSourceTypeFilter: string | null
  createdAt: string
  updatedAt: string
}

export interface SemanticQueryTurnRecord {
  id: string
  turnIndex: number
  queryText: string
  sourceTypeFilter: string | null
  tokenCount: number
  hitCount: number
  sourceTypeDiversity: number
  matchedTokenTotal: number
  matchedWeightTotal: number
  topSimilarity: number
  meanSimilarity: number
  driftFromPrevious: number | null
  createdAt: string
}

export interface SemanticSearchResponse {
  session: SemanticQuerySessionSummary | null
  currentTurn: SemanticQueryTurnRecord
  recentTurns: SemanticQueryTurnRecord[]
  curvature: SemanticCurvatureStatus
  recovery: SemanticRecoveryStatus
  hits: SemanticChunkHit[]
  recorded: boolean
}

export interface SemanticSearchComparison {
  baselineHits: SemanticChunkHit[]
  trajectoryHits: SemanticChunkHit[]
  curvature: SemanticCurvatureStatus
  recovery: SemanticRecoveryStatus
  overlapCount: number
  baselineOnlyCount: number
  trajectoryOnlyCount: number
  baselineOnlyHits: SemanticChunkHit[]
  trajectoryOnlyHits: SemanticChunkHit[]
}

export interface SemanticSearchComparisonSummary {
  queryText: string
  sourceTypeFilter: string | null
  overlapCount: number
  baselineOnlyCount: number
  trajectoryOnlyCount: number
  curvature: number
  recoveryTriggered: boolean
  recoveryReason: string
}

export interface SemanticSessionSearchEvaluation {
  sessionId: string
  turnCount: number
  comparedTurnCount: number
  meanOverlapCount: number
  meanBaselineOnlyCount: number
  meanTrajectoryOnlyCount: number
  recoveryTriggerCount: number
  meanCurvature: number
  turns: SemanticSearchComparisonSummary[]
}

export interface SemanticQuerySessionDetail {
  session: SemanticQuerySessionSummary
  turns: SemanticQueryTurnRecord[]
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

export interface SemanticSplitApplyResult {
  created: number
  rebuilt: number
  proposalsApplied?: number
}

export interface SemanticAdminActionRecord {
  id: string
  adminUserId: string
  adminDisplayName: string
  action: string
  token: string | null
  scopeKind: string | null
  scopeValue: string | null
  variantKey: string | null
  createdCount: number
  proposalCount: number
  detailsJson: string
  createdAt: string
}

export const semanticAdminApi = {
  getStats: (): Promise<SemanticGraphStats> =>
    fetch(`${BASE}/stats`, opts).then(json),

  getAutomationStatus: (): Promise<SemanticAutomationStatus> =>
    fetch(`${BASE}/automation/status`, opts).then(json),

  runAutomationPass: (): Promise<SemanticAutomationRunResult> =>
    fetch(`${BASE}/automation/run`, { ...opts, method: 'POST' }).then(json),

  search: (query: string, sourceType?: string, limit = 10, sessionId?: string, record = true): Promise<SemanticSearchResponse> => {
    const params = new URLSearchParams({ q: query, limit: String(limit) })
    if (sourceType && sourceType.trim()) params.set('sourceType', sourceType.trim())
    if (sessionId && sessionId.trim()) params.set('sessionId', sessionId.trim())
    params.set('record', String(record))
    return fetch(`${BASE}/search?${params.toString()}`, opts).then(json)
  },

  getDispersionCandidates: (limit = 12, minChunkCount = 3): Promise<SemanticTokenDispersionCandidate[]> => {
    const params = new URLSearchParams({ limit: String(limit), minChunkCount: String(minChunkCount) })
    return fetch(`${BASE}/dispersion?${params.toString()}`, opts).then(json)
  },

  getDriftStatus: (baseMinChunkCount = 3): Promise<SemanticDriftStatus> => {
    const params = new URLSearchParams({ baseMinChunkCount: String(baseMinChunkCount) })
    return fetch(`${BASE}/drift-status?${params.toString()}`, opts).then(json)
  },

  getTokenSplits: (): Promise<SemanticTokenSplitRecord[]> =>
    fetch(`${BASE}/splits`, opts).then(json),

  getSemanticAdminHistory: (limit = 25): Promise<SemanticAdminActionRecord[]> => {
    const params = new URLSearchParams({ limit: String(limit) })
    return fetch(`${BASE}/splits/history?${params.toString()}`, opts).then(json)
  },

  getSearchSessions: (limit = 10): Promise<SemanticQuerySessionSummary[]> => {
    const params = new URLSearchParams({ limit: String(limit) })
    return fetch(`${BASE}/search/sessions?${params.toString()}`, opts).then(json)
  },

  getSearchSessionDetail: (sessionId: string, turnLimit = 8): Promise<SemanticQuerySessionDetail> => {
    const params = new URLSearchParams({ turnLimit: String(turnLimit) })
    return fetch(`${BASE}/search/sessions/${sessionId}?${params.toString()}`, opts).then(json)
  },

  evaluateSearchSession: (sessionId: string, limit = 10, turnLimit = 8): Promise<SemanticSessionSearchEvaluation> => {
    const params = new URLSearchParams({ limit: String(limit), turnLimit: String(turnLimit) })
    return fetch(`${BASE}/search/sessions/${sessionId}/evaluate?${params.toString()}`, opts).then(json)
  },

  compareSearchModes: (query: string, sourceType?: string, limit = 10, sessionId?: string): Promise<SemanticSearchComparison> => {
    const params = new URLSearchParams({ q: query, limit: String(limit) })
    if (sourceType && sourceType.trim()) params.set('sourceType', sourceType.trim())
    if (sessionId && sessionId.trim()) params.set('sessionId', sessionId.trim())
    return fetch(`${BASE}/search/compare?${params.toString()}`, opts).then(json)
  },

  getTokenSplitProposals: (limit = 12, minChunkCount = 3, scopeKind?: string): Promise<SemanticTokenSplitProposal[]> => {
    const params = new URLSearchParams({ limit: String(limit), minChunkCount: String(minChunkCount) })
    if (scopeKind && scopeKind.trim()) params.set('scopeKind', scopeKind.trim())
    return fetch(`${BASE}/splits/proposals?${params.toString()}`, opts).then(json)
  },

  applyTokenSplitProposal: (payload: Pick<SemanticTokenSplitProposal, 'token' | 'scopeKind'>): Promise<SemanticSplitApplyResult> =>
    fetch(`${BASE}/splits/proposals/apply`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(json),

  applyAllTokenSplitProposals: (limit = 12, minChunkCount = 3, scopeKind?: string): Promise<SemanticSplitApplyResult> => {
    const params = new URLSearchParams({ limit: String(limit), minChunkCount: String(minChunkCount) })
    if (scopeKind && scopeKind.trim()) params.set('scopeKind', scopeKind.trim())
    return fetch(`${BASE}/splits/proposals/apply-all?${params.toString()}`, { ...opts, method: 'POST' }).then(json)
  },

  materializeSourceTypeSplits: (limit = 12, minChunkCount = 3): Promise<SemanticSplitApplyResult> => {
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
