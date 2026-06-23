const BASE = '/api/forum'

export interface ForumCategory {
  id: string
  name: string
  description?: string
  position: number
  createdAt: string
}

export interface ForumForum {
  id: string
  categoryId: string
  name: string
  description?: string
  threadCount: number
  postCount: number
  lastPostAt?: string
  createdAt: string
}

export interface ForumThread {
  id: string
  forumId: string
  authorId: string
  title: string
  isPinned: boolean
  isLocked: boolean
  postCount: number
  viewCount: number
  lastPostAt?: string
  createdAt: string
}

export interface ForumPost {
  id: string
  threadId: string
  authorId: string
  content: string
  isAnswer: boolean
  voteCount: number
  createdAt: string
  updatedAt: string
  deletedAt?: string
}

export const forumApi = {
  getCategories: (): Promise<ForumCategory[]> =>
    fetch(`${BASE}/categories`).then(r => r.json()),

  getForums: (categoryId: string): Promise<ForumForum[]> =>
    fetch(`${BASE}/categories/${categoryId}/forums`).then(r => r.json()),

  getForumById: (forumId: string): Promise<ForumForum | null> =>
    fetch(`${BASE}/forums/${forumId}`).then(r => r.ok ? r.json() : null),

  getThreads: (forumId: string, page = 1, pageSize = 25): Promise<ForumThread[]> =>
    fetch(`${BASE}/forums/${forumId}/threads?page=${page}&pageSize=${pageSize}`).then(r => r.json()),

  getThread: (threadId: string): Promise<ForumThread> =>
    fetch(`${BASE}/threads/${threadId}`).then(r => r.json()),

  createThread: (forumId: string, title: string, content: string): Promise<ForumThread> =>
    fetch(`${BASE}/forums/${forumId}/threads`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, content }),
    }).then(r => r.json()),

  getPosts: (threadId: string, page = 1, pageSize = 25): Promise<ForumPost[]> =>
    fetch(`${BASE}/threads/${threadId}/posts?page=${page}&pageSize=${pageSize}`).then(r => r.json()),

  createPost: (threadId: string, content: string): Promise<ForumPost> =>
    fetch(`${BASE}/threads/${threadId}/posts`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content }),
    }).then(r => r.json()),

  updatePost: (postId: string, content: string): Promise<ForumPost> =>
    fetch(`${BASE}/posts/${postId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content }),
    }).then(r => r.json()),

  deletePost: (postId: string): Promise<void> =>
    fetch(`${BASE}/posts/${postId}`, { method: 'DELETE' }).then(() => {}),

  votePost: (postId: string): Promise<{ voted: boolean }> =>
    fetch(`${BASE}/posts/${postId}/vote`, { method: 'POST' }).then(r => r.json()),

  markAnswer: (postId: string): Promise<void> =>
    fetch(`${BASE}/posts/${postId}/answer`, { method: 'POST' }).then(() => {}),

  pinThread: (threadId: string, pinned: boolean): Promise<void> =>
    fetch(`${BASE}/threads/${threadId}/pin`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pinned }),
    }).then(() => {}),

  lockThread: (threadId: string, locked: boolean): Promise<void> =>
    fetch(`${BASE}/threads/${threadId}/lock`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ locked }),
    }).then(() => {}),
}
