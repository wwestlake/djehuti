const BASE = '/djehuti/api/blog'
const CONFIG_BASE = '/djehuti/api/config'
const opts = { credentials: 'include' as RequestCredentials }

const json = (r: Response) => {
  if (!r.ok) throw new Error(`${r.status} ${r.statusText}`)
  return r.json()
}

// ── Types ─────────────────────────────────────────────────────────────────────

export interface BlogSection {
  id: string
  name: string
  slug: string
  description?: string
  position: number
  createdAt: string
}

export type ArticleStatus =
  | 'draft' | 'submitted' | 'under_review' | 'approved'
  | 'published' | 'rejected' | 'needs_revision'

export interface BlogArticle {
  id: string
  sectionId: string
  authorId: string
  authorName: string
  title: string
  subtitle?: string
  slug: string
  content: string
  bodyJson?: string
  excerpt?: string
  coverUrl?: string
  status: ArticleStatus
  visibility: 'public' | 'unlisted' | 'private'
  featured: boolean
  featuredPosition?: number
  pinned: boolean
  publishedAt?: string
  createdAt: string
  updatedAt: string
  deletedAt?: string
}

export interface BlogTag {
  id: string
  name: string
  slug: string
  description?: string
}

export interface BlogAuthor {
  userId: string
  bio?: string
  displayName?: string
  avatarUrl?: string
  socialLinks: string
  trusted: boolean
  createdAt: string
  updatedAt: string
}

export interface BlogUpload {
  id: string
  articleId?: string
  uploaderUserId: string
  originalFilename: string
  mimeType: string
  format: string
  storageKey: string
  sizeBytes?: number
  conversionStatus: 'pending' | 'processing' | 'done' | 'failed'
  conversionOption: 'as-is' | 'convert' | 'reformat'
  convertedHtml?: string
  errorMessage?: string
  createdAt: string
  updatedAt: string
}

export interface BlogModerationEntry {
  id: string
  articleId: string
  moderatorUserId?: string
  action: string
  note?: string
  createdAt: string
}

export interface BlogComment {
  id: string
  articleId: string
  authorId: string
  content: string
  createdAt: string
  deletedAt?: string
}

export interface SiteConfigEntry {
  scope: string
  key: string
  value: string
  updatedAt: string
}

// ── Sections ──────────────────────────────────────────────────────────────────

export const blogApi = {
  getSections: (): Promise<BlogSection[]> =>
    fetch(`${BASE}/sections`, opts).then(json),

  createSection: (name: string, description?: string): Promise<BlogSection> =>
    fetch(`${BASE}/sections`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, description: description ?? '' }),
    }).then(json),

  // ── Tags ───────────────────────────────────────────────────────────────────

  getTags: (): Promise<BlogTag[]> =>
    fetch(`${BASE}/tags`, opts).then(json),

  createTag: (name: string, description?: string): Promise<BlogTag> =>
    fetch(`${BASE}/tags`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, description: description ?? '' }),
    }).then(json),

  updateTag: (id: string, name: string, description?: string): Promise<BlogTag> =>
    fetch(`${BASE}/tags/${id}`, {
      ...opts, method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, description: description ?? '' }),
    }).then(json),

  deleteTag: (id: string): Promise<void> =>
    fetch(`${BASE}/tags/${id}`, { ...opts, method: 'DELETE' }).then(() => {}),

  getArticleTags: (articleId: string): Promise<BlogTag[]> =>
    fetch(`${BASE}/articles/${articleId}/tags`, opts).then(json),

  setArticleTags: (articleId: string, tagIds: string[]): Promise<void> =>
    fetch(`${BASE}/articles/${articleId}/tags`, {
      ...opts, method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tagIds }),
    }).then(() => {}),

  // ── Articles ───────────────────────────────────────────────────────────────

  getArticles: (opts2?: { sectionId?: string; search?: string; tag?: string; page?: number; pageSize?: number }): Promise<BlogArticle[]> => {
    const p = new URLSearchParams({
      page: String(opts2?.page ?? 1),
      pageSize: String(opts2?.pageSize ?? 20),
    })
    if (opts2?.sectionId) p.set('sectionId', opts2.sectionId)
    if (opts2?.search)    p.set('search', opts2.search)
    if (opts2?.tag)       p.set('tag', opts2.tag)
    return fetch(`${BASE}/articles?${p}`, opts).then(json)
  },

  getArticle: (slug: string): Promise<BlogArticle> =>
    fetch(`${BASE}/articles/${slug}`, opts).then(json),

  getRandomArticle: (): Promise<BlogArticle | null> =>
    fetch(`${BASE}/articles/random`, opts).then(r => r.ok ? r.json() : null),

  getMyArticles: (page = 1, pageSize = 20): Promise<BlogArticle[]> =>
    fetch(`${BASE}/my-articles?page=${page}&pageSize=${pageSize}`, opts).then(json),

  createArticle: (fields: {
    sectionId: string; title: string; subtitle?: string
    content: string; bodyJson?: string; excerpt?: string; visibility?: string
  }): Promise<BlogArticle> =>
    fetch(`${BASE}/articles`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        sectionId:  fields.sectionId,
        title:      fields.title,
        subtitle:   fields.subtitle   ?? '',
        content:    fields.content,
        bodyJson:   fields.bodyJson   ?? '',
        excerpt:    fields.excerpt    ?? '',
        visibility: fields.visibility ?? 'public',
      }),
    }).then(json),

  updateArticle: (id: string, fields: {
    title: string; subtitle?: string; content: string; bodyJson?: string
    excerpt?: string; coverUrl?: string; visibility?: string
  }): Promise<BlogArticle> =>
    fetch(`${BASE}/articles/${id}`, {
      ...opts, method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        title:      fields.title,
        subtitle:   fields.subtitle   ?? '',
        content:    fields.content,
        bodyJson:   fields.bodyJson   ?? '',
        excerpt:    fields.excerpt    ?? '',
        coverUrl:   fields.coverUrl   ?? '',
        visibility: fields.visibility ?? 'public',
      }),
    }).then(json),

  setStatus: (id: string, status: string, note?: string): Promise<BlogArticle> =>
    fetch(`${BASE}/articles/${id}/status`, {
      ...opts, method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status, note: note ?? '' }),
    }).then(json),

  setFeatured: (id: string, featured: boolean, position?: number): Promise<BlogArticle> =>
    fetch(`${BASE}/articles/${id}/feature`, {
      ...opts, method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ featured, position: position ?? 0 }),
    }).then(json),

  setPinned: (id: string, pinned: boolean): Promise<BlogArticle> =>
    fetch(`${BASE}/articles/${id}/pin`, {
      ...opts, method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pinned }),
    }).then(json),

  deleteArticle: (id: string): Promise<void> =>
    fetch(`${BASE}/articles/${id}`, { ...opts, method: 'DELETE' }).then(() => {}),

  getModerationLog: (articleId: string): Promise<BlogModerationEntry[]> =>
    fetch(`${BASE}/articles/${articleId}/moderation-log`, opts).then(json),

  // ── Uploads ────────────────────────────────────────────────────────────────

  registerUpload: (fields: {
    originalFilename: string; mimeType: string; format: string
    storageKey: string; sizeBytes: number; conversionOption: string
  }): Promise<BlogUpload> =>
    fetch(`${BASE}/uploads`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(fields),
    }).then(json),

  getUpload: (id: string): Promise<BlogUpload> =>
    fetch(`${BASE}/uploads/${id}`, opts).then(json),

  attachUpload: (uploadId: string, articleId: string): Promise<BlogUpload> =>
    fetch(`${BASE}/uploads/${uploadId}/attach`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ articleId }),
    }).then(json),

  // ── Authors ────────────────────────────────────────────────────────────────

  getAuthors: (): Promise<BlogAuthor[]> =>
    fetch(`${BASE}/authors`, opts).then(json),

  getAuthor: (userId: string): Promise<BlogAuthor> =>
    fetch(`${BASE}/authors/${userId}`, opts).then(json),

  upsertAuthor: (userId: string, fields: {
    bio?: string; displayName?: string; avatarUrl?: string; socialLinks?: string; trusted?: boolean
  }): Promise<BlogAuthor> =>
    fetch(`${BASE}/authors/${userId}`, {
      ...opts, method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        bio: fields.bio ?? '', displayName: fields.displayName ?? '',
        avatarUrl: fields.avatarUrl ?? '', socialLinks: fields.socialLinks ?? '[]',
        trusted: fields.trusted ?? false,
      }),
    }).then(json),

  // ── Comments ───────────────────────────────────────────────────────────────

  getComments: (articleId: string): Promise<BlogComment[]> =>
    fetch(`${BASE}/articles/${articleId}/comments`, opts).then(json),

  createComment: (articleId: string, content: string): Promise<BlogComment> =>
    fetch(`${BASE}/articles/${articleId}/comments`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content }),
    }).then(json),

  deleteComment: (id: string): Promise<void> =>
    fetch(`${BASE}/comments/${id}`, { ...opts, method: 'DELETE' }).then(() => {}),

  // ── Config ─────────────────────────────────────────────────────────────────

  getConfig: (scope?: string): Promise<SiteConfigEntry[]> => {
    const p = scope ? `?scope=${scope}` : ''
    return fetch(`${CONFIG_BASE}${p}`, opts).then(json)
  },

  setConfig: (scope: string, key: string, value: string): Promise<SiteConfigEntry> =>
    fetch(`${CONFIG_BASE}/${scope}/${key}`, {
      ...opts, method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ value }),
    }).then(json),
}
