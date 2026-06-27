const BASE = '/djehuti/api/forum'
const API  = '/djehuti/api'

export interface ForumCategory {
  id: string
  name: string
  description?: string
  position: number
  parentCategoryId?: string
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

export interface ForumTag {
  id: string
  name: string
  slug: string
  description?: string
  createdAt: string
}

export interface ForumPost {
  id: string
  threadId: string
  authorId: string
  content: string
  isAnswer: boolean
  voteCount: number
  isBot?: boolean
  state?: string
  createdAt: string
  updatedAt: string
  deletedAt?: string
}

export interface ForumReport {
  id: string
  reporterId: string
  targetType: string
  targetId: string
  reason: string
  status: string
  resolvedBy?: string
  resolvedAt?: string
  createdAt: string
}

export interface Notification {
  id: string
  userId: string
  type: string
  body: string
  link?: string
  readAt?: string
  createdAt: string
}

export interface Subscription {
  id: string
  userId: string
  targetType: string
  targetId: string
  level: string
  createdAt: string
}

const opts = { credentials: 'include' as RequestCredentials }

export const forumApi = {
  getCategories: (): Promise<ForumCategory[]> =>
    fetch(`${BASE}/categories`, opts).then(r => r.json()),

  getForums: (categoryId: string): Promise<ForumForum[]> =>
    fetch(`${BASE}/categories/${categoryId}/forums`, opts).then(r => r.json()),

  getForumById: (forumId: string): Promise<ForumForum | null> =>
    fetch(`${BASE}/forums/${forumId}`, opts).then(r => r.ok ? r.json() : null),

  getThreads: (forumId: string, page = 1, pageSize = 25): Promise<ForumThread[]> =>
    fetch(`${BASE}/forums/${forumId}/threads?page=${page}&pageSize=${pageSize}`, opts).then(r => r.json()),

  getThread: (threadId: string): Promise<ForumThread> =>
    fetch(`${BASE}/threads/${threadId}`, opts).then(r => r.json()),

  createThread: (forumId: string, title: string, content: string): Promise<ForumThread> =>
    fetch(`${BASE}/forums/${forumId}/threads`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, content }),
    }).then(r => r.json()),

  getPosts: (threadId: string, page = 1, pageSize = 25): Promise<ForumPost[]> =>
    fetch(`${BASE}/threads/${threadId}/posts?page=${page}&pageSize=${pageSize}`, opts).then(r => r.json()),

  createPost: (threadId: string, content: string): Promise<ForumPost> =>
    fetch(`${BASE}/threads/${threadId}/posts`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content }),
    }).then(r => r.json()),

  updatePost: (postId: string, content: string): Promise<ForumPost> =>
    fetch(`${BASE}/posts/${postId}`, {
      ...opts, method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content }),
    }).then(r => r.json()),

  deletePost: (postId: string): Promise<void> =>
    fetch(`${BASE}/posts/${postId}`, { ...opts, method: 'DELETE' }).then(() => {}),

  votePost: (postId: string): Promise<{ voted: boolean }> =>
    fetch(`${BASE}/posts/${postId}/vote`, { ...opts, method: 'POST' }).then(r => r.json()),

  markAnswer: (postId: string): Promise<void> =>
    fetch(`${BASE}/posts/${postId}/answer`, { ...opts, method: 'POST' }).then(() => {}),

  pinThread: (threadId: string, pinned: boolean): Promise<void> =>
    fetch(`${BASE}/threads/${threadId}/pin`, {
      ...opts, method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pinned }),
    }).then(() => {}),

  lockThread: (threadId: string, locked: boolean): Promise<void> =>
    fetch(`${BASE}/threads/${threadId}/lock`, {
      ...opts, method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ locked }),
    }).then(() => {}),

  getTags: (): Promise<ForumTag[]> =>
    fetch(`${API}/forum/tags`, opts).then(r => r.json()),

  createTag: (name: string, slug: string, description?: string): Promise<ForumTag> =>
    fetch(`${API}/forum/tags`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, slug, description: description ?? '' }),
    }).then(r => r.json()),

  deleteTag: (tagId: string): Promise<void> =>
    fetch(`${API}/forum/tags/${tagId}`, { ...opts, method: 'DELETE' }).then(() => {}),

  getThreadTags: (threadId: string): Promise<ForumTag[]> =>
    fetch(`${API}/forum/threads/${threadId}/tags`, opts).then(r => r.json()),

  getReactions: (postId: string): Promise<{ emoji: string; count: number; userReacted: boolean }[]> =>
    fetch(`${API}/forum/posts/${postId}/reactions`, opts).then(r => r.json()),

  toggleReaction: (postId: string, emoji: string): Promise<{ added: boolean; emoji: string }> =>
    fetch(`${API}/forum/posts/${postId}/reactions`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ emoji }),
    }).then(r => r.json()),

  setThreadTags: (threadId: string, tagIds: string[]): Promise<ForumTag[]> =>
    fetch(`${API}/forum/threads/${threadId}/tags`, {
      ...opts, method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tagIds }),
    }).then(r => r.json()),

  reportContent: (targetType: 'post' | 'thread', targetId: string, reason: string): Promise<void> =>
    fetch(`${API}/forum/reports`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ targetType, targetId, reason }),
    }).then(() => {}),

  getAdminReports: (status?: string, page = 1, pageSize = 25): Promise<ForumReport[]> => {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (status) q.set('status', status)
    return fetch(`${API}/admin/forum/reports?${q}`, opts).then(r => r.json())
  },

  resolveReport: (reportId: string, status: 'dismissed' | 'warned' | 'deleted'): Promise<void> =>
    fetch(`${API}/admin/forum/reports/${reportId}`, {
      ...opts, method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status }),
    }).then(() => {}),

  subscribeThread: (threadId: string, level: string): Promise<Subscription> =>
    fetch(`${API}/forum/threads/${threadId}/subscribe`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ level }),
    }).then(r => r.json()),

  getThreadSubscription: (threadId: string): Promise<Subscription | null> =>
    fetch(`${API}/forum/threads/${threadId}/subscribe`, opts).then(r => r.ok ? r.json() : null),

  subscribeCategory: (categoryId: string, level: string): Promise<Subscription> =>
    fetch(`${API}/forum/categories/${categoryId}/subscribe`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ level }),
    }).then(r => r.json()),

  getNotifications: (page = 1, pageSize = 20): Promise<{ items: Notification[]; unreadCount: number }> =>
    fetch(`${API}/notifications?page=${page}&pageSize=${pageSize}`, opts).then(r => r.json()),

  markNotificationRead: (id: string): Promise<void> =>
    fetch(`${API}/notifications/${id}/read`, { ...opts, method: 'PATCH' }).then(() => {}),

  markAllNotificationsRead: (): Promise<void> =>
    fetch(`${API}/notifications/read-all`, { ...opts, method: 'PATCH' }).then(() => {}),

  getPoll: (threadId: string): Promise<PollData | null> =>
    fetch(`${API}/forum/threads/${threadId}/poll`, opts).then(r => r.ok ? r.json() : null),

  createPoll: (threadId: string, data: { question: string; options: string[]; closesAt: string; allowMultiple: boolean }): Promise<{ id: string }> =>
    fetch(`${API}/forum/threads/${threadId}/poll`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }).then(r => r.json()),

  votePoll: (pollId: string, optionIds: string[]): Promise<{ voted: boolean }> =>
    fetch(`${API}/forum/polls/${pollId}/vote`, {
      ...opts, method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ optionIds }),
    }).then(r => r.json()),
}

export interface PollOption {
  id: string
  text: string
  position: number
  voteCount: number
}

export interface PollData {
  id: string
  question: string
  closesAt?: string
  allowMultiple: boolean
  options: PollOption[]
  userVotes: string[]
}
