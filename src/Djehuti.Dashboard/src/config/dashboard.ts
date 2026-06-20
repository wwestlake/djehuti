import type { AppMode, AnalyzeView, PhaseRenderMode } from '../types'

export const maxVisibleRows = 250
export const maxChartPoints = 300
export const maxPhasePoints = 600
export const maxTimelinePoints = 360
export const maxFeatureRailHits = 800
export const maxFeatureRows = 220

export const strategyColors: Record<string, number> = {
  seed: 0x155eef,
  natural: 0x087f8c,
  shock: 0xd97706,
  interleaved: 0x7c3aed,
  'interleaved-with-history': 0x7c3aed,
  Stable: 0x087f8c,
  Expansive: 0x7c3aed,
  Corrective: 0xd97706,
  Drifting: 0xb42318,
  Unknown: 0x667085,
}

export const phaseRenderModes: Array<{
  id: PhaseRenderMode
  label: string
}> = [
  { id: 'points', label: 'Points' },
  { id: 'solid', label: 'Solid' },
  { id: 'hybrid', label: 'Hybrid' },
  { id: 'envelope', label: 'Envelope' },
  { id: 'deform', label: 'Deform' },
]

export const visualizationIdeas = [
  {
    name: 'Metric timelines',
    detail: 'Plot cosine, Jaccard, edit similarity, and word delta over integer time.',
  },
  {
    name: 'Prompt-response phase space',
    detail: 'Render time, alignment, and velocity as an interactive trajectory through measurement space.',
  },
  {
    name: 'Strategy bands',
    detail: 'Show contiguous spans of stable, corrective, expansive, or drifting behavior.',
  },
  {
    name: 'State transition graph',
    detail: 'Connect each turn to the next by velocity and annotate large jumps.',
  },
  {
    name: 'Gap detector',
    detail: 'Highlight missing sequence indexes and abrupt metric discontinuities.',
  },
  {
    name: 'Corpus distribution',
    detail: 'Histogram or density view for response length, velocity, and similarity.',
  },
]

export const modeIds = ['analyze', 'live', 'mlmce', 'reports', 'settings'] satisfies AppMode[]
export const analyzeViewIds = ['overview', 'phase', 'timelines', 'features', 'data', 'input'] satisfies AnalyzeView[]
