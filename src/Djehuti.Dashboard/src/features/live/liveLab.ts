import type { AnalyzeResponse, LiveProviderConfig, LiveTurn, LiveWarning } from '../../types'
import { formatNumber, readErrorMessage } from '../../lib/format'

const extractOpenAiResponseText = (payload: unknown) => {
  if (payload && typeof payload === 'object' && 'output_text' in payload) {
    const outputText = (payload as { output_text?: unknown }).output_text
    if (typeof outputText === 'string' && outputText.trim()) {
      return outputText
    }
  }

  const output = (payload as { output?: unknown })?.output
  if (Array.isArray(output)) {
    const texts = output.flatMap((item) => {
      const content = (item as { content?: unknown })?.content
      if (!Array.isArray(content)) {
        return []
      }

      return content
        .map((contentItem) => (contentItem as { text?: unknown })?.text)
        .filter((text): text is string => typeof text === 'string' && text.trim().length > 0)
    })

    if (texts.length > 0) {
      return texts.join('\n')
    }
  }

  throw new Error('Provider response did not contain assistant text.')
}

export const liveTurnsToDatasetJson = (turns: LiveTurn[]) =>
  JSON.stringify(
    {
      source: {
        id: 'live-lab',
        kind: 'client-side-live-chat',
        name: 'Live Lab client-side run',
      },
      constants: {
        distanceMetric: 'cosine',
        captureMode: 'client-side-provider-key',
        contextPolicy: 'vanilla chat history only',
      },
      interactions: turns.map((turn) => ({
        sessionId: 'live-lab-session',
        modelId: turn.modelId,
        sequenceIndex: turn.sequenceIndex,
        prompt: turn.prompt,
        response: turn.response,
      })),
    },
    null,
    2,
  )

export const askLiveProvider = async (
  config: LiveProviderConfig,
  turns: LiveTurn[],
  prompt: string,
  signal?: AbortSignal,
) => {
  if (config.protocol !== 'openai-responses') {
    throw new Error(`Unsupported live provider protocol: ${config.protocol}`)
  }

  const input = [
    ...turns.flatMap((turn) => [
      { role: 'user', content: turn.prompt },
      { role: 'assistant', content: turn.response },
    ]),
    { role: 'user', content: prompt },
  ]

  const body: Record<string, unknown> = { model: config.model, input, store: false }
  if (config.webSearch) {
    body.tools = [{ type: 'web_search_preview' }]
  }

  const response = await fetch(config.endpoint, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${config.apiKey}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
    signal,
  })

  if (!response.ok) {
    const message = await readErrorMessage(response)
    throw new Error(message || `Provider request failed with ${response.status}`)
  }

  return extractOpenAiResponseText(await response.json())
}

export const deriveLiveWarnings = (analysis: AnalyzeResponse) => {
  const warnings: LiveWarning[] = []
  const latestTurn = analysis.turns[analysis.turns.length - 1]

  if (!latestTurn) {
    return warnings
  }

  if ((latestTurn.velocityFromPrevious ?? 0) > 0.8) {
    warnings.push({
      id: `velocity-${latestTurn.sequenceIndex}`,
      sequenceIndex: latestTurn.sequenceIndex,
      severity: 'high',
      label: 'high live velocity',
      evidence: `Velocity from previous turn is ${formatNumber(latestTurn.velocityFromPrevious)}.`,
    })
  }

  if (latestTurn.promptResponseCosine < 0.15) {
    warnings.push({
      id: `alignment-${latestTurn.sequenceIndex}`,
      sequenceIndex: latestTurn.sequenceIndex,
      severity: 'high',
      label: 'low prompt-response alignment',
      evidence: `Prompt-response cosine is ${formatNumber(latestTurn.promptResponseCosine)}.`,
    })
  }

  if (Math.abs(latestTurn.wordCountDelta) > 45) {
    warnings.push({
      id: `delta-${latestTurn.sequenceIndex}`,
      sequenceIndex: latestTurn.sequenceIndex,
      severity: 'medium',
      label: 'large word-count delta',
      evidence: `Prompt has ${latestTurn.promptWordCount} words; response has ${latestTurn.responseWordCount}.`,
    })
  }

  for (const event of analysis.attractorEvents) {
    warnings.push({
      id: `attractor-${event.sequenceIndex}`,
      sequenceIndex: event.sequenceIndex,
      severity: 'high',
      label: 'attractor approach',
      evidence: event.description,
    })
  }

  return warnings
}
