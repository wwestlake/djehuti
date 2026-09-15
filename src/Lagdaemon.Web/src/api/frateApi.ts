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

export interface PagedPods {
  total: number
  pods: PodSummary[]
}

export const frateApi = {
  searchPods: (query?: string): Promise<PodSummary[]> =>
    fetch(`${BASE}/pods${query ? `?q=${encodeURIComponent(query)}` : ''}`, opts).then(json),

  getPod: (name: string): Promise<PodDetail | null> =>
    fetch(`${BASE}/pods/${encodeURIComponent(name)}`, opts).then(r => r.ok ? r.json() : null),

  browsePods: (query: string, license: string, page: number, pageSize = 10): Promise<PagedPods> => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (query) params.set('q', query)
    if (license) params.set('license', license)
    return fetch(`${BASE}/browse?${params}`, opts).then(json)
  },

  getFacets: (): Promise<{ licenses: string[] }> =>
    fetch(`${BASE}/facets`, opts).then(json),
}
