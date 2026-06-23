const BASE = '/api/papers'

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

async function get(url: string) {
  const res = await fetch(url, { credentials: 'include' })
  if (!res.ok) throw new Error(res.statusText)
  return res.json()
}

async function post(url: string, body: unknown) {
  const res = await fetch(url, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(res.statusText)
  return res.json()
}

async function put(url: string, body: unknown) {
  const res = await fetch(url, {
    method: 'PUT',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(res.statusText)
  return res.json()
}

async function patch(url: string, body: unknown) {
  const res = await fetch(url, {
    method: 'PATCH',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(res.statusText)
  return res.json()
}

async function del(url: string) {
  const res = await fetch(url, { method: 'DELETE', credentials: 'include' })
  if (!res.ok) throw new Error(res.statusText)
}

export const papersApi = {
  list: (): Promise<Paper[]> => get(BASE),
  get: (id: string): Promise<Paper> => get(`${BASE}/${id}`),
  create: (title: string, summary: string): Promise<Paper> => post(BASE, { title, summary }),
  update: (id: string, title: string, summary: string): Promise<Paper> => put(`${BASE}/${id}`, { title, summary }),
  setStatus: (id: string, status: string): Promise<Paper> => patch(`${BASE}/${id}/status`, { status }),
  delete: (id: string): Promise<void> => del(`${BASE}/${id}`),

  getSections: (paperId: string): Promise<PaperSection[]> => get(`${BASE}/${paperId}/sections`),
  createSection: (paperId: string, title: string, position: number): Promise<PaperSection> =>
    post(`${BASE}/${paperId}/sections`, { title, position }),
  updateSection: (sectionId: string, title: string, content: string): Promise<PaperSection> =>
    put(`${BASE}/sections/${sectionId}`, { title, content }),
  deleteSection: (sectionId: string): Promise<void> => del(`${BASE}/sections/${sectionId}`),

  getCollaborators: (paperId: string): Promise<PaperCollaborator[]> => get(`${BASE}/${paperId}/collaborators`),
  addCollaborator: (paperId: string, name: string, email: string, role: string, isExternal: boolean): Promise<void> =>
    post(`${BASE}/${paperId}/collaborators`, { name, email, role, isExternal }),
  removeCollaborator: (paperId: string, name: string): Promise<void> =>
    del(`${BASE}/${paperId}/collaborators/${encodeURIComponent(name)}`),
}
