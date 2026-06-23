import type { AnalystResponse, AnalyzeResponse, DataSetCatalogItem } from '../types'
import { readErrorMessage } from '../lib/format'
import { apiBase } from '../lib/apiBase'

export const fetchDataSetCatalog = async () => {
  const response = await fetch(`${apiBase}/api/datasets`)
  if (!response.ok) {
    throw new Error(`Dataset catalog failed with ${response.status}`)
  }

  return (await response.json()) as DataSetCatalogItem[]
}

export const fetchDataSetJson = async (id: string) => {
  const response = await fetch(`${apiBase}/api/datasets/${encodeURIComponent(id)}`)
  if (!response.ok) {
    const message = await response.text()
    throw new Error(message || `Dataset load failed with ${response.status}`)
  }

  return response.text()
}

export const analyzeDatasetJson = async (json: string, signal?: AbortSignal) => {
  const response = await fetch(`${apiBase}/api/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ datasetJson: json }),
    signal,
  })

  if (!response.ok) {
    const message = await readErrorMessage(response)
    throw new Error(message || `Request failed with ${response.status}`)
  }

  return (await response.json()) as AnalyzeResponse
}

export const askAnalystApi = async (
  datasetJson: string,
  question: string,
  signal?: AbortSignal,
) => {
  const response = await fetch(`${apiBase}/api/analyst/ask`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      datasetJson,
      question,
      temperature: 0.1,
      maxOutputTokens: 900,
    }),
    signal,
  })

  if (!response.ok) {
    const message = await readErrorMessage(response)
    throw new Error(message || `Analyst request failed with ${response.status}`)
  }

  return (await response.json()) as AnalystResponse
}
