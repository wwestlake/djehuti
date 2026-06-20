export const formatNumber = (value: number | null | undefined, digits = 3) =>
  typeof value === 'number' && Number.isFinite(value) ? value.toFixed(digits) : 'n/a'

export const sampleEvenly = <T,>(items: T[], limit: number) => {
  if (items.length <= limit) {
    return items
  }

  return Array.from({ length: limit }, (_, index) => {
    const sourceIndex = Math.round((index / (limit - 1)) * (items.length - 1))
    return items[sourceIndex]
  })
}

export const readErrorMessage = async (response: Response) => {
  const text = await response.text()
  if (!text) {
    return `Request failed with ${response.status}`
  }

  try {
    const problem = JSON.parse(text) as {
      title?: string
      detail?: string
      errors?: unknown
    }

    if (problem.detail && problem.title) {
      return `${problem.title}: ${problem.detail}`
    }

    if (problem.detail) {
      return problem.detail
    }

    if (problem.title) {
      return problem.title
    }
  } catch {
    // Plain text error bodies are already suitable for display.
  }

  return text
}
