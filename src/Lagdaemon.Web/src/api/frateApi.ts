const BASE = '/djehuti/api/frate'
const opts = { credentials: 'include' as const }
const json = (r: Response) => r.json()

export interface PodSummary {
  name: string
  description?: string | null
  latestVersion: string
  license: string
  exports: string[]
}

export interface PodVersionSummary {
  version: string
  description?: string | null
  sizeBytes: number
  createdAt: string
  yanked: boolean
}

export interface PodDetail {
  name: string
  description?: string | null
  license: string
  versions: PodVersionSummary[]
}

export const frateApi = {
  searchPods: (query?: string): Promise<PodSummary[]> =>
    fetch(`${BASE}/pods${query ? `?q=${encodeURIComponent(query)}` : ''}`, opts).then(json),

  getPod: (name: string): Promise<PodDetail | null> =>
    fetch(`${BASE}/pods/${encodeURIComponent(name)}`, opts).then(r => r.ok ? r.json() : null),
}
