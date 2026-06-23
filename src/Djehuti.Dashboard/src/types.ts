export type SummaryDto = {
  sourceId: string
  sourceName: string
  sourceKind: string
  sessionId: string
  modelId: string
  turnCount: number
  velocityCount: number
  gapCount: number
  averagePromptResponseCosine: number
  averageWordCountDelta: number
}

export type TurnMetricDto = {
  sequenceIndex: number
  sessionId: string
  prompt: string
  response: string
  promptWordCount: number
  responseWordCount: number
  wordCountDelta: number
  sharedWordCount: number
  promptResponseCosine: number
  jaccardSimilarity: number
  editSimilarity: number
  velocityFromPrevious: number | null
  strategy: string
  contaminationDepth: string
  sourceId: string
}

export type VelocityPointDto = {
  sequenceIndex: number
  sessionId: string
  value: number
  basis: string
}

export type ConstantDto = {
  name: string
  value: string
}

export type AttractorEventDto = {
  turnId: string
  sequenceIndex: number
  description: string
  torsionalResistanceValue: number | null
  torsionalResistanceBasis: string
  torsionalResistanceKind: string
  assumptions: string[]
}

export type AnalyzeResponse = {
  summary: SummaryDto
  turns: TurnMetricDto[]
  velocities: VelocityPointDto[]
  constants: ConstantDto[]
  attractorEvents: AttractorEventDto[]
  warnings: string[]
}

export type DataSetCatalogItem = {
  id: string
  name: string
  description: string
  file: string
  sourceKind: string
  turnCount: number
  declaredTurnCount: number | null
  status: string
}

export type AnalystEvidenceDto = {
  label: string
  value: string
  source: string
}

export type AnalystResponse = {
  answer: string
  evidence: AnalystEvidenceDto[]
  model: string
  metadata: Record<string, string>
}

export type AnalystMessage = {
  id: string
  question: string
  response: AnalystResponse
}

export type FeatureSource = 'prompt' | 'response' | 'transition' | 'metrics' | 'attractor'

export type FeatureSeverity = 'low' | 'medium' | 'high'

export type FeatureHit = {
  id: string
  label: string
  sequenceIndex: number
  source: FeatureSource
  severity: FeatureSeverity
  evidence: string
}

export type LiveProviderProtocol = 'openai-responses'

export type LiveProviderConfig = {
  protocol: LiveProviderProtocol
  apiKey: string
  model: string
  endpoint: string
  webSearch: boolean
}

export type LiveTurn = {
  sequenceIndex: number
  prompt: string
  response: string
  modelId: string
  webSearch?: boolean
}

export type LiveWarning = {
  id: string
  sequenceIndex: number
  severity: FeatureSeverity
  label: string
  evidence: string
}

export type MlmceTurnMode = 'sequential' | 'prompted' | 'broadcast'
export type MlmceSessionKind = 'sequential-dialogue' | 'forked-interferometer-run'

export type MlmceParticipantConfig = {
  id: string
  roleLabel: string
  modelId: string
}

export type MlmceThresholdConfig = {
  stabilityCriterionMargin: number
  leakageBudgetFraction: number
  torsionalAccumulationCeiling: number
  attractorWindow: number
  divergenceThreshold: number
}

export type TimelinePoint = {
  sequenceIndex: number
  value: number
}

export type TimelineSeries = {
  label: string
  color: string
  points: TimelinePoint[]
  formatter?: (value: number) => string
}

export type PhaseRenderMode = 'points' | 'solid' | 'hybrid' | 'envelope' | 'deform'
export type AppMode = 'analyze' | 'live' | 'mlmce' | 'reports' | 'settings' | 'forum' | 'blog'
export type AnalyzeView = 'overview' | 'phase' | 'timelines' | 'features' | 'data' | 'input'


export type TourStep = {
  target: string
  title?: string
  text: string
  side?: 'top' | 'bottom' | 'left' | 'right'
}
