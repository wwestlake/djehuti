const BASE = '/djehuti/api/beta'
const opts = { credentials: 'include' as const }
const json = (r: Response) => r.json()

export interface BetaProduct {
  slug: string
  name: string
  description?: string | null
}

export const betaApi = {
  getOpenProducts: (): Promise<BetaProduct[]> =>
    fetch(`${BASE}/products`, opts).then(json),

  signup: (email: string, productSlug: string): Promise<{ message: string }> =>
    fetch(`${BASE}/signup`, {
      ...opts,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, productSlug }),
    }).then(async r => {
      if (!r.ok) throw new Error((await r.json().catch(() => null))?.title ?? r.statusText)
      return r.json()
    }),
}
