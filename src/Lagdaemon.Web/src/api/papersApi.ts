const BASE = '/djehuti/api/papers'
const opts = { credentials: 'include' as RequestCredentials }

export interface Paper {
  id: string
  ownerId: string
  title: string
  abstract: string | null
  status: string
  createdAt: string
  updatedAt: string
}

export interface PaperSection {
  id: string
  paperId: string
  title: string
  content: string
  position: number
  createdAt: string
  updatedAt: string
}

export interface PaperCollaborator {
  paperId: string
  userId: string | null
  name: string
  email: string | null
  role: string
  isExternal: boolean
  addedAt: string
}

export interface PublicPaper {
  id: string
  title: string
  abstract: string | null
  authorName: string
  updatedAt: string
}

export interface PublicPaperDetail {
  paper: PublicPaper
  sections: PaperSection[]
}

async function apiFetch(url: string, init?: RequestInit) {
  const res = await fetch(url, { ...opts, ...init })
  if (!res.ok) throw new Error(res.statusText)
  return res.json()
}

async function apiDel(url: string) {
  const res = await fetch(url, { ...opts, method: 'DELETE' })
  if (!res.ok) throw new Error(res.statusText)
}

const json = (body: unknown) => ({
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(body),
})

export const papersApi = {
  publicList: (): Promise<PublicPaper[]> => apiFetch(`${BASE}/public`),
  publicGet: (id: string): Promise<PublicPaperDetail> => apiFetch(`${BASE}/public/${id}`),

  list: (): Promise<Paper[]> => apiFetch(BASE),
  get: (id: string): Promise<Paper> => apiFetch(`${BASE}/${id}`),
  create: (title: string, summary: string): Promise<Paper> => apiFetch(BASE, { method: 'POST', ...json({ title, summary }) }),
  update: (id: string, title: string, summary: string): Promise<Paper> => apiFetch(`${BASE}/${id}`, { method: 'PUT', ...json({ title, summary }) }),
  setStatus: (id: string, status: string): Promise<Paper> => apiFetch(`${BASE}/${id}/status`, { method: 'PATCH', ...json({ status }) }),
  delete: (id: string): Promise<void> => apiDel(`${BASE}/${id}`),

  getSections: (paperId: string): Promise<PaperSection[]> => apiFetch(`${BASE}/${paperId}/sections`),
  createSection: (paperId: string, title: string, position: number): Promise<PaperSection> =>
    apiFetch(`${BASE}/${paperId}/sections`, { method: 'POST', ...json({ title, position }) }),
  updateSection: (sectionId: string, title: string, content: string): Promise<PaperSection> =>
    apiFetch(`${BASE}/sections/${sectionId}`, { method: 'PUT', ...json({ title, content }) }),
  deleteSection: (sectionId: string): Promise<void> => apiDel(`${BASE}/sections/${sectionId}`),

  getCollaborators: (paperId: string): Promise<PaperCollaborator[]> => apiFetch(`${BASE}/${paperId}/collaborators`),
  addCollaborator: (paperId: string, name: string, email: string, role: string, isExternal: boolean): Promise<void> =>
    apiFetch(`${BASE}/${paperId}/collaborators`, { method: 'POST', ...json({ name, email, role, isExternal }) }),
  removeCollaborator: (paperId: string, name: string): Promise<void> =>
    apiDel(`${BASE}/${paperId}/collaborators/${encodeURIComponent(name)}`),
}
