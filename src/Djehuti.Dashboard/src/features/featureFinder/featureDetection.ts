import type { AttractorEventDto, FeatureHit, FeatureSeverity, FeatureSource, TurnMetricDto } from '../../types'
import { formatNumber } from '../../lib/format'

const topicMarkers = [
  { label: 'Descartes', terms: ['descartes', 'cartesian'] },
  { label: 'Gassendi', terms: ['gassendi'] },
  { label: 'pineal gland', terms: ['pineal'] },
  { label: 'vortex theory', terms: ['vortex', 'vortices'] },
  { label: 'dualism', terms: ['dualism', 'dualist'] },
]

const featureSeverityOrder: Record<FeatureSeverity, number> = {
  high: 0,
  medium: 1,
  low: 2,
}

const normalizeFeatureText = (text: string) =>
  text
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()

const evidenceSnippet = (text: string, term?: string) => {
  const compact = text.replace(/\s+/g, ' ').trim()
  if (!compact) {
    return 'No text evidence available.'
  }

  if (!term) {
    return compact.length > 180 ? `${compact.slice(0, 177)}...` : compact
  }

  const lower = compact.toLowerCase()
  const index = lower.indexOf(term.toLowerCase())
  if (index < 0) {
    return compact.length > 180 ? `${compact.slice(0, 177)}...` : compact
  }

  const start = Math.max(index - 70, 0)
  const end = Math.min(index + term.length + 90, compact.length)
  const prefix = start > 0 ? '...' : ''
  const suffix = end < compact.length ? '...' : ''
  return `${prefix}${compact.slice(start, end)}${suffix}`
}

export function detectFeatures(turns: TurnMetricDto[], attractorEvents: AttractorEventDto[]) {
  const hits: FeatureHit[] = []
  const seenPrompts = new Map<string, number>()

  const emit = (
    turn: TurnMetricDto,
    label: string,
    source: FeatureSource,
    severity: FeatureSeverity,
    evidence: string,
  ) => {
    hits.push({
      id: `${turn.sequenceIndex}-${label}-${source}-${hits.length}`,
      label,
      sequenceIndex: turn.sequenceIndex,
      source,
      severity,
      evidence,
    })
  }

  for (const turn of turns) {
    const velocity = turn.velocityFromPrevious ?? 0
    const prompt = turn.prompt ?? ''
    const response = turn.response ?? ''

    if (velocity > 0.65) {
      emit(
        turn,
        'high velocity',
        'transition',
        'high',
        `Velocity from previous turn is ${formatNumber(velocity)}.`,
      )
    }

    if (turn.promptResponseCosine < 0.15) {
      emit(
        turn,
        'low prompt-response alignment',
        'metrics',
        'high',
        `Prompt-response cosine is ${formatNumber(turn.promptResponseCosine)}.`,
      )
    }

    if (Math.abs(turn.wordCountDelta) > 40) {
      emit(
        turn,
        'large word-count delta',
        'metrics',
        'medium',
        `Prompt has ${turn.promptWordCount} words; response has ${turn.responseWordCount}.`,
      )
    }

    if (turn.responseWordCount < 12) {
      emit(turn, 'short response', 'response', 'medium', evidenceSnippet(response))
    }

    const normalizedPrompt = normalizeFeatureText(prompt)
    if (normalizedPrompt.length > 24) {
      const firstSeen = seenPrompts.get(normalizedPrompt)
      if (firstSeen !== undefined) {
        emit(turn, 'repeated prompt', 'prompt', 'medium', `Prompt first appeared at t=${firstSeen}.`)
      } else {
        seenPrompts.set(normalizedPrompt, turn.sequenceIndex)
      }
    }

    const lowerPrompt = prompt.toLowerCase()
    const lowerResponse = response.toLowerCase()
    for (const marker of topicMarkers) {
      const term = marker.terms.find(
        (candidate) => lowerPrompt.includes(candidate) || lowerResponse.includes(candidate),
      )

      if (term) {
        const source = lowerPrompt.includes(term) ? 'prompt' : 'response'
        emit(
          turn,
          `topic: ${marker.label}`,
          source,
          'low',
          evidenceSnippet(source === 'prompt' ? prompt : response, term),
        )
      }
    }
  }

  for (const event of attractorEvents) {
    if (event.sequenceIndex < 0) {
      continue
    }

    const tau =
      event.torsionalResistanceValue === null
        ? 'tau unavailable'
        : `tau ${formatNumber(event.torsionalResistanceValue)}`
    const basis = event.torsionalResistanceBasis ? `; ${event.torsionalResistanceBasis}` : ''
    const kind = event.torsionalResistanceKind ? `; ${event.torsionalResistanceKind}` : ''

    hits.push({
      id: `${event.sequenceIndex}-attractor-${hits.length}`,
      label: 'attractor approach',
      sequenceIndex: event.sequenceIndex,
      source: 'attractor',
      severity: 'high',
      evidence: `${event.description} (${tau}${basis}${kind})`,
    })
  }

  return hits.sort((left, right) => {
    if (left.sequenceIndex !== right.sequenceIndex) {
      return left.sequenceIndex - right.sequenceIndex
    }

    return featureSeverityOrder[left.severity] - featureSeverityOrder[right.severity]
  })
}
