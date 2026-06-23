const BASE = '/djehuti/api/blog'
const opts = { credentials: 'include' as RequestCredentials }

export interface BlogSection {
  id: string
  name: string
  slug: string
  description?: string
  position: number
  createdAt: string
}

export interface BlogArticle {
  id: string
  sectionId: string
  authorId: string
  title: string
  slug: string
  content: string
  excerpt?: string
  coverUrl?: string
  status: 'draft' | 'submitted' | 'published' | 'rejected'
  publishedAt?: string
  createdAt: string
  updatedAt: string
}

export interface BlogComment {
  id: string
  articleId: string
  authorId: string
  content: string
  createdAt: string
  deletedAt?: string
}

export const blogApi = {
  getSections: (): Promise<BlogSection[]> =>
    fetch(`${BASE}/sections`, opts).then(r => r.json()),

  getArticles: (sectionId?: string, page = 1, pageSize = 20): Promise<BlogArticle[]> => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (sectionId) params.set('sectionId', sectionId)
    return fetch(`${BASE}/articles?${params}`, opts).then(r => r.json())
  },

  getArticle: (slug: string): Promise<BlogArticle> =>
    fetch(`${BASE}/articles/${slug}`, opts).then(r => r.json()),

  getMyArticles: (page = 1, pageSize = 20): Promise<BlogArticle[]> =>
    fetch(`${BASE}/my-articles?page=${page}&pageSize=${pageSize}`, opts).then(r => r.json()),

  createArticle: (sectionId: string, title: string, content: string, excerpt?: string): Promise<BlogArticle> =>
    fetch(`${BASE}/articles`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sectionId, title, content, excerpt: excerpt ?? '' }),
    }).then(r => r.json()),

  updateArticle: (id: string, title: string, content: string, excerpt?: string, coverUrl?: string): Promise<BlogArticle> =>
    fetch(`${BASE}/articles/${id}`, {
      ...opts, method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, content, excerpt: excerpt ?? '', coverUrl: coverUrl ?? '' }),
    }).then(r => r.json()),

  setStatus: (id: string, status: string): Promise<BlogArticle> =>
    fetch(`${BASE}/articles/${id}/status`, {
      ...opts, method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status }),
    }).then(r => r.json()),

  deleteArticle: (id: string): Promise<void> =>
    fetch(`${BASE}/articles/${id}`, { ...opts, method: 'DELETE' }).then(() => {}),

  getComments: (articleId: string): Promise<BlogComment[]> =>
    fetch(`${BASE}/articles/${articleId}/comments`, opts).then(r => r.json()),

  createComment: (articleId: string, content: string): Promise<BlogComment> =>
    fetch(`${BASE}/articles/${articleId}/comments`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content }),
    }).then(r => r.json()),

  deleteComment: (id: string): Promise<void> =>
    fetch(`${BASE}/comments/${id}`, { ...opts, method: 'DELETE' }).then(() => {}),
}
