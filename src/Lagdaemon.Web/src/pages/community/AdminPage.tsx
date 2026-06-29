import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { blogApi } from '../../api/blogApi'
import type { BlogArticle, BlogTag, BlogAuthor, SiteConfigEntry } from '../../api/blogApi'
import { forumApi } from '../../api/forumApi'
import type { ForumTag, ForumReport } from '../../api/forumApi'

const BASE = '/djehuti'

interface AdminUser {
  id: string; email: string; role: string; status: string; displayName: string | null
  createdAt: string; lastLoginAt: string | null; emailVerified: boolean
}
interface ContextRole {
  id: string; userId: string; module: string; role: string; scopeId: string | null; grantedAt: string
}
interface Announcement {
  id: string; title: string; body: string; priority: number
  authorId: string | null; publishedAt: string | null; expiresAt: string | null
  createdAt: string; updatedAt: string
}

type Tab = 'users' | 'blog-queue' | 'blog-all' | 'blog-authors' | 'tags' | 'forum-tags' | 'forum-reports' | 'config' | 'roles' | 'announcements' | 'personas' | 'heartbeat' | 'metrics' | 'api-keys'

interface MetricsCounts { users: number; posts: number; threads: number; articles: number; votesGiven: number; reactions: number; achievements: number }
interface ForumActivityRow { forumId: string; forumName: string; postsAll: number; postsHuman: number; postsAi: number; threadsAll: number; threadsHuman: number; threadsAi: number }
interface DailyActivityRow { date: string; postsHuman: number; postsAi: number }
interface TopUserRow { userId: string; displayName: string; isBot: boolean; posts: number; threads: number; votesReceived: number; achievements: number; loginStreak: number; daysActive: number }
interface SiteMetrics {
  totals: { all: MetricsCounts; human: MetricsCounts; ai: MetricsCounts }
  forumActivity: ForumActivityRow[]
  dailyActivity: DailyActivityRow[]
  topHumans: TopUserRow[]
  topBots: TopUserRow[]
}
interface UserDrilldown {
  userId: string; displayName: string; isBot: boolean; email: string
  postCount: number; threadCount: number; voteReceived: number; voteGiven: number
  reactionCount: number; answerCount: number; loginStreak: number; daysActive: number; lastActiveAt: string
  achievements: { slug: string; name: string; icon: string; tier: string; awardedAt: string }[]
}

interface ApiKey {
  id: string
  name: string
  keyPrefix: string
  createdAt: string
  lastUsedAt: string | null
  active: boolean
}

interface AiPersona {
  id: string; name: string; slug: string; avatarUrl: string | null; systemPrompt: string
  model: string; triggerMode: string; active: boolean; createdAt: string
}
interface HeartbeatJob {
  id: string; actionType: string; payload: string; status: string
  retryCount: number; createdAt: string; completedAt: string | null; error: string | null
}
interface HeartbeatConfig { [key: string]: string }

const PAGE_SIZE = 25

async function apiFetch(url: string, opts?: RequestInit) {
  const res = await fetch(url, { credentials: 'include', ...opts })
  if (!res.ok) throw new Error(res.statusText)
  return res.json()
}

export default function AdminPage() {
  const { user } = useAuth()
  const [tab, setTab] = useState<Tab>('users')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Users list state
  const [users, setUsers] = useState<AdminUser[]>([])
  const [usersTotal, setUsersTotal] = useState(0)
  const [usersPage, setUsersPage] = useState(1)
  const [userSearch, setUserSearch] = useState('')
  const [userSearchInput, setUserSearchInput] = useState('')
  const [userFilterRole, setUserFilterRole] = useState('')
  const [userFilterStatus, setUserFilterStatus] = useState('')

  // Edit modal
  const [userModal, setUserModal] = useState<AdminUser | null>(null)
  const [editName, setEditName] = useState('')
  const [editRole, setEditRole] = useState('')
  const [editStatus, setEditStatus] = useState('')
  const [modalSaving, setModalSaving] = useState(false)

  // Invite modal
  const [inviteOpen, setInviteOpen] = useState(false)
  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteRole, setInviteRole] = useState('user')
  const [inviting, setInviting] = useState(false)

  // Blog queue
  const [queue, setQueue] = useState<BlogArticle[]>([])
  const [reviewingId, setReviewingId] = useState<string | null>(null)
  const [modNote, setModNote] = useState('')

  // All articles (admin view)
  const [allArticles, setAllArticles] = useState<BlogArticle[]>([])

  // Blog authors
  const [authors, setAuthors] = useState<BlogAuthor[]>([])

  // Tags
  const [tags, setTags] = useState<BlogTag[]>([])
  const [newTagName, setNewTagName] = useState('')
  const [newTagDesc, setNewTagDesc] = useState('')

  const [forumTags, setForumTags] = useState<ForumTag[]>([])
  const [newForumTagName, setNewForumTagName] = useState('')
  const [newForumTagDesc, setNewForumTagDesc] = useState('')

  const [forumReports, setForumReports] = useState<ForumReport[]>([])

  // Config
  const [config, setConfig] = useState<SiteConfigEntry[]>([])
  const [editingConfig, setEditingConfig] = useState<Record<string, string>>({})

  // Roles
  const [roles, setRoles] = useState<ContextRole[]>([])
  const [grantForm, setGrantForm] = useState({ userId: '', module: 'forum', role: 'moderator', scopeId: '' })
  const [granting, setGranting] = useState(false)

  // Announcements
  const [announcements, setAnnouncements] = useState<Announcement[]>([])
  const [annForm, setAnnForm] = useState({ title: '', body: '', priority: 0, expiresAt: '' })
  const [editingAnn, setEditingAnn] = useState<Announcement | null>(null)
  const [annSaving, setAnnSaving] = useState(false)

  // Personas
  const [personas, setPersonas] = useState<AiPersona[]>([])
  const [personaForm, setPersonaForm] = useState({ name: '', slug: '', systemPrompt: '', model: 'claude-sonnet-4-6', triggerMode: 'mention', avatarUrl: '', forumIds: '' })
  const [editingPersona, setEditingPersona] = useState<AiPersona | null>(null)
  const [personaSaving, setPersonaSaving] = useState(false)

  // Heartbeat
  const [hbJobs, setHbJobs] = useState<HeartbeatJob[]>([])
  const [_hbConfig, setHbConfig] = useState<HeartbeatConfig>({})
  const [hbConfigEdit, setHbConfigEdit] = useState<HeartbeatConfig>({})
  const [hbSaving, setHbSaving] = useState(false)
  const [hbTriggering, setHbTriggering] = useState(false)

  // Metrics
  const [metrics, setMetrics] = useState<SiteMetrics | null>(null)
  const [metricsUser, setMetricsUser] = useState<UserDrilldown | null>(null)
  const [metricsUserLoading, setMetricsUserLoading] = useState(false)
  const [recomputingAchievements, setRecomputingAchievements] = useState(false)
  const [recomputeResult, setRecomputeResult] = useState<string | null>(null)
  const [collapsedSections, setCollapsedSections] = useState<Record<string, boolean>>({})
  const [timeseriesMetric, setTimeseriesMetric] = useState<string | null>(null)
  const [timeseriesData, setTimeseriesData] = useState<{ date: string; count: number }[]>([])
  const [timeseriesLoading, setTimeseriesLoading] = useState(false)
  const [anonMetrics, setAnonMetrics] = useState<{
    uniqueVisitors30d: number
    uniqueVisitorsAllTime: number
    conversions30d: number
    conversionRatePct: number
    topThreads: { threadId: string; title: string; viewCount: number }[]
    referrers: { referrer: string; visits: number }[]
    dailyVisitors: { date: string; count: number }[]
    countries: { country: string; visitors: number }[]
    topPages: { path: string; views: number; uniqueVisitors: number }[]
    recentVisitors: { ipAddress: string; country: string; region: string; city: string; domain: string; referrer: string; path: string; viewedAt: string }[]
  } | null>(null)
  const [anonRefreshing, setAnonRefreshing] = useState(false)
  const [anonRefreshMsg, setAnonRefreshMsg] = useState<string | null>(null)
  const [menuOpen, setMenuOpen] = useState(false)
  const [apiKeys, setApiKeys] = useState<ApiKey[]>([])
  const [newKeyName, setNewKeyName] = useState('')
  const [newKeyPlaintext, setNewKeyPlaintext] = useState<string | null>(null)
  const [keyCreating, setKeyCreating] = useState(false)

  const loadUsers = async (p = usersPage, s = userSearch, r = userFilterRole, st = userFilterStatus) => {
    setLoading(true); setError(null)
    try {
      const params = new URLSearchParams({ page: String(p), pageSize: String(PAGE_SIZE) })
      if (s) params.set('search', s)
      if (r) params.set('role', r)
      if (st) params.set('status', st)
      const res = await apiFetch(`${BASE}/api/admin/users?${params}`)
      setUsers(Array.isArray(res) ? res : (res.data ?? []))
      setUsersTotal(res.total ?? 0)
    } catch { setError('Failed to load users') }
    finally { setLoading(false) }
  }

  useEffect(() => {
    if (!user || user.role !== 'admin') return
    if (tab === 'users') { loadUsers(1, '', '', ''); setUsersPage(1); setUserSearch(''); setUserSearchInput(''); setUserFilterRole(''); setUserFilterStatus('') }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab, user])

  useEffect(() => {
    if (!user || user.role !== 'admin') return
    if (tab === 'users') return
    setLoading(true); setError(null)
    const loaders: Record<Tab, () => Promise<void>> = {
      users: async () => { /* handled above */ },
      'blog-queue': () => apiFetch(`${BASE}/api/admin/blog/queue`).then(setQueue),
      'blog-all': () => apiFetch(`${BASE}/api/admin/blog/articles`).then(setAllArticles),
      'blog-authors': () => blogApi.getAuthors().then(setAuthors),
      tags: () => blogApi.getTags().then(setTags),
      'forum-tags': () => forumApi.getTags().then(setForumTags),
      'forum-reports': () => forumApi.getAdminReports('open').then(setForumReports),
      config: () => blogApi.getConfig().then(entries => {
        setConfig(entries)
        const map: Record<string, string> = {}
        entries.forEach(e => { map[`${e.scope}:${e.key}`] = e.value.replace(/^"|"$/g, '') })
        setEditingConfig(map)
      }),
      roles: () => apiFetch(`${BASE}/api/admin/context-roles`).then(setRoles),
      announcements: () => apiFetch(`${BASE}/api/admin/announcements`).then(setAnnouncements),
      personas: () => apiFetch(`${BASE}/api/admin/personas`).then((rows: { persona: AiPersona }[]) => setPersonas(rows.map(r => r.persona))),
      heartbeat: async () => {
        const [jobs, cfg] = await Promise.all([
          apiFetch(`${BASE}/api/admin/heartbeat/jobs?limit=50`),
          apiFetch(`${BASE}/api/admin/heartbeat/config`),
        ])
        setHbJobs(jobs)
        setHbConfig(cfg)
        setHbConfigEdit({ ...cfg })
      },
      metrics: () => Promise.all([
        apiFetch(`${BASE}/api/admin/metrics`).then(setMetrics),
        apiFetch(`${BASE}/api/admin/metrics/anonymous`).then(setAnonMetrics).catch(() => {}),
      ]).then(() => {}),
      'api-keys': () => apiFetch(`${BASE}/api/admin/api-keys`).then(setApiKeys),
    }
    loaders[tab]().catch(() => setError('Failed to load data')).finally(() => setLoading(false))
  }, [tab, user])

  // ── Moderation ──────────────────────────────────────────────────────────────

  const doModAction = async (id: string, status: string) => {
    try {
      await blogApi.setStatus(id, status, modNote || undefined)
      setQueue(prev => prev.filter(a => a.id !== id))
      setReviewingId(null); setModNote('')
    } catch { setError('Failed to update article.') }
  }

  // ── Authors ─────────────────────────────────────────────────────────────────

  const toggleTrusted = async (a: BlogAuthor) => {
    try {
      const updated = await blogApi.upsertAuthor(a.userId, { ...a, trusted: !a.trusted })
      setAuthors(prev => prev.map(x => x.userId === a.userId ? updated : x))
    } catch { setError('Failed to update author.') }
  }

  // ── Tags ────────────────────────────────────────────────────────────────────

  const createTag = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newTagName.trim()) return
    try {
      const t = await blogApi.createTag(newTagName.trim(), newTagDesc.trim() || undefined)
      if (t) setTags(prev => [...prev, t])
      setNewTagName(''); setNewTagDesc('')
    } catch { setError('Failed to create tag.') }
  }

  const deleteTag = async (id: string) => {
    if (!confirm('Delete tag? This removes it from all articles.')) return
    try { await blogApi.deleteTag(id); setTags(prev => prev.filter(t => t.id !== id)) }
    catch { setError('Failed to delete tag.') }
  }

  const createForumTag = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newForumTagName.trim()) return
    const slug = newForumTagName.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')
    try {
      const t = await forumApi.createTag(newForumTagName.trim(), slug, newForumTagDesc.trim() || undefined)
      setForumTags(prev => [...prev, t])
      setNewForumTagName(''); setNewForumTagDesc('')
    } catch { setError('Failed to create forum tag.') }
  }

  const deleteForumTag = async (id: string) => {
    if (!confirm('Delete forum tag? This removes it from all threads.')) return
    try { await forumApi.deleteTag(id); setForumTags(prev => prev.filter(t => t.id !== id)) }
    catch { setError('Failed to delete forum tag.') }
  }

  // ── Config ──────────────────────────────────────────────────────────────────

  const saveConfig = async (scope: string, key: string) => {
    const val = editingConfig[`${scope}:${key}`] ?? ''
    try {
      // Wrap string values in quotes for JSON storage
      const jsonVal = `"${val}"`
      await blogApi.setConfig(scope, key, jsonVal)
    } catch { setError('Failed to save config.') }
  }

  // ── Roles ───────────────────────────────────────────────────────────────────

  const grantRole = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!grantForm.userId.trim()) return
    setGranting(true)
    try {
      await apiFetch(`${BASE}/api/roles/grant`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId: grantForm.userId, module: grantForm.module, role: grantForm.role, scopeId: grantForm.scopeId || null }),
      })
      setRoles(await apiFetch(`${BASE}/api/admin/context-roles`))
      setGrantForm(f => ({ ...f, userId: '', scopeId: '' }))
    } catch { setError('Failed to grant role.') } finally { setGranting(false) }
  }

  const revokeRole = async (id: string) => {
    try { await apiFetch(`${BASE}/api/admin/context-roles/${id}`, { method: 'DELETE' }); setRoles(prev => prev.filter(r => r.id !== id)) }
    catch { setError('Failed to revoke role.') }
  }

  const openUserModal = (u: AdminUser) => {
    setUserModal(u); setEditName(u.displayName ?? ''); setEditRole(u.role); setEditStatus(u.status)
  }

  const saveUserModal = async () => {
    if (!userModal) return
    setModalSaving(true)
    try {
      await fetch(`${BASE}/api/admin/users/${userModal.id}/display-name`, {
        method: 'PATCH', credentials: 'include', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: editName }),
      })
      await fetch(`${BASE}/api/admin/users/${userModal.id}/role`, {
        method: 'PATCH', credentials: 'include', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ role: editRole }),
      })
      if (editStatus !== userModal.status) {
        if (editStatus === 'restricted' || userModal.status === 'restricted') {
          await fetch(`${BASE}/api/admin/users/${userModal.id}/restrict`, {
            method: 'PATCH', credentials: 'include', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ restrict: editStatus === 'restricted' }),
          })
        } else {
          await fetch(`${BASE}/api/admin/users/${userModal.id}/suspend`, {
            method: 'PATCH', credentials: 'include', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ suspend: editStatus === 'suspended' }),
          })
        }
      }
      setUsers(prev => prev.map(u => u.id === userModal.id
        ? { ...u, displayName: editName || null, role: editRole, status: editStatus } : u))
      setUserModal(null)
    } catch { setError('Failed to save changes.') }
    finally { setModalSaving(false) }
  }

  const verifyUserEmail = async (id: string) => {
    try {
      await fetch(`${BASE}/api/admin/users/${id}/verify`, { method: 'PATCH', credentials: 'include' })
      setUsers(prev => prev.map(u => u.id === id ? { ...u, emailVerified: true } : u))
      setUserModal(prev => prev?.id === id ? { ...prev, emailVerified: true } : prev)
    } catch { setError('Failed to verify email.') }
  }

  const sendPasswordReset = async (id: string) => {
    try {
      await fetch(`${BASE}/api/admin/users/${id}/reset-password`, { method: 'POST', credentials: 'include' })
      alert('Password reset email sent.')
    } catch { setError('Failed to send reset email.') }
  }

  const deleteUser = async (id: string, email: string) => {
    if (!confirm(`Permanently delete ${email}? This cannot be undone.`)) return
    try {
      await fetch(`${BASE}/api/admin/users/${id}`, { method: 'DELETE', credentials: 'include' })
      setUsers(prev => prev.filter(u => u.id !== id))
      setUsersTotal(t => t - 1)
      setUserModal(null)
    } catch { setError('Failed to delete user.') }
  }

  const sendInvite = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!inviteEmail.trim()) return
    setInviting(true)
    try {
      await apiFetch(`${BASE}/api/admin/users/invite`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: inviteEmail.trim(), role: inviteRole }),
      })
      alert(`Invite sent to ${inviteEmail}`)
      setInviteEmail(''); setInviteOpen(false)
      loadUsers(1, userSearch, userFilterRole, userFilterStatus)
    } catch { setError('Failed to send invite.') }
    finally { setInviting(false) }
  }

  // ── Announcements ────────────────────────────────────────────────────────────

  const saveAnn = async (e: React.FormEvent) => {
    e.preventDefault()
    setAnnSaving(true)
    try {
      const payload = {
        title: annForm.title.trim(),
        body: annForm.body.trim(),
        priority: annForm.priority,
        expiresAt: annForm.expiresAt || null,
      }
      if (editingAnn) {
        const updated = await apiFetch(`${BASE}/api/admin/announcements/${editingAnn.id}`, {
          method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload),
        })
        setAnnouncements(prev => prev.map(a => a.id === editingAnn.id ? updated : a))
      } else {
        const created = await apiFetch(`${BASE}/api/admin/announcements`, {
          method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload),
        })
        setAnnouncements(prev => [created, ...prev])
      }
      setAnnForm({ title: '', body: '', priority: 0, expiresAt: '' })
      setEditingAnn(null)
    } catch { setError('Failed to save announcement.') } finally { setAnnSaving(false) }
  }

  const publishAnn = async (id: string) => {
    try {
      const updated = await apiFetch(`${BASE}/api/admin/announcements/${id}/publish`, { method: 'POST' })
      setAnnouncements(prev => prev.map(a => a.id === id ? updated : a))
    } catch { setError('Failed to publish.') }
  }

  const unpublishAnn = async (id: string) => {
    try {
      const updated = await apiFetch(`${BASE}/api/admin/announcements/${id}/unpublish`, { method: 'POST' })
      setAnnouncements(prev => prev.map(a => a.id === id ? updated : a))
    } catch { setError('Failed to unpublish.') }
  }

  const deleteAnn = async (id: string) => {
    if (!confirm('Delete this announcement?')) return
    try {
      await apiFetch(`${BASE}/api/admin/announcements/${id}`, { method: 'DELETE' })
      setAnnouncements(prev => prev.filter(a => a.id !== id))
    } catch { setError('Failed to delete.') }
  }

  const startEditAnn = (a: Announcement) => {
    setEditingAnn(a)
    setAnnForm({
      title: a.title, body: a.body, priority: a.priority,
      expiresAt: a.expiresAt ? a.expiresAt.slice(0, 10) : '',
    })
  }

  if (!user || user.role !== 'admin') return <p className="forum-login-prompt">Admin access required.</p>

  const generateApiKey = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newKeyName.trim()) return
    setKeyCreating(true)
    try {
      const params = new URLSearchParams({ name: newKeyName.trim() })
      const res = await apiFetch(`${BASE}/api/admin/api-keys?${params}`, { method: 'POST' })
      setNewKeyPlaintext(res.key)
      setApiKeys(prev => [res.record, ...prev])
      setNewKeyName('')
    } catch { setError('Failed to generate key') }
    finally { setKeyCreating(false) }
  }

  const revokeApiKey = async (id: string) => {
    if (!confirm('Revoke this API key? It will stop working immediately.')) return
    try {
      await apiFetch(`${BASE}/api/admin/api-keys/${id}`, { method: 'DELETE' })
      setApiKeys(prev => prev.map(k => k.id === id ? { ...k, active: false } : k))
    } catch { setError('Failed to revoke key') }
  }

  const TAB_LABELS: Record<Tab, string> = {
    users: 'Users', 'blog-queue': 'Review Queue', 'blog-all': 'All Articles', 'blog-authors': 'Authors', tags: 'Blog Tags', 'forum-tags': 'Forum Tags', 'forum-reports': 'Reports', config: 'Config', roles: 'Roles', announcements: 'Announcements', personas: 'AI Personas', heartbeat: 'Heartbeat', metrics: 'Metrics', 'api-keys': 'API Keys',
  }

  const toggleSection = (key: string) =>
    setCollapsedSections(prev => ({ ...prev, [key]: !prev[key] }))

  const openTimeseries = async (metric: string, label: string) => {
    setTimeseriesMetric(label)
    setTimeseriesLoading(true)
    setTimeseriesData([])
    try {
      const rows: { date: string; postsHuman: number }[] = await apiFetch(`${BASE}/api/admin/metrics/timeseries/${metric}`)
      setTimeseriesData(rows.map(r => ({ date: r.date, count: r.postsHuman })))
    } catch { setTimeseriesData([]) }
    finally { setTimeseriesLoading(false) }
  }

  const recomputeAchievements = async () => {
    setRecomputingAchievements(true)
    setRecomputeResult(null)
    try {
      await apiFetch(`${BASE}/api/admin/achievements/recompute`, { method: 'POST' })
      setRecomputeResult('Recompute complete. Refresh Metrics to see updated badge counts.')
      const updated = await apiFetch(`${BASE}/api/admin/metrics`)
      setMetrics(updated)
    } catch { setRecomputeResult('Recompute failed — check server logs.') }
    finally { setRecomputingAchievements(false) }
  }

  const refreshAnonFromLogs = async () => {
    setAnonRefreshing(true)
    setAnonRefreshMsg(null)
    try {
      await apiFetch(`${BASE}/api/admin/metrics/anonymous/refresh-logs`, { method: 'POST' })
      setAnonRefreshMsg('Log scan running — refreshing metrics in 15 seconds…')
      setTimeout(async () => {
        try {
          const data = await apiFetch(`${BASE}/api/admin/metrics/anonymous`)
          setAnonMetrics(data)
          setAnonRefreshMsg('Done.')
        } catch { setAnonRefreshMsg('Scan complete — refresh the page to see results.') }
        finally { setAnonRefreshing(false) }
      }, 15000)
    } catch {
      setAnonRefreshMsg('Failed to start log scan.')
      setAnonRefreshing(false)
    }
  }

  const loadMetricsUser = async (userId: string) => {
    setMetricsUserLoading(true)
    try { setMetricsUser(await apiFetch(`${BASE}/api/admin/metrics/user/${userId}`)) }
    catch { setMetricsUser(null) }
    finally { setMetricsUserLoading(false) }
  }

  const savePersona = async (e: React.FormEvent) => {
    e.preventDefault()
    setPersonaSaving(true)
    try {
      const forumIds = personaForm.forumIds.split(',').map(s => s.trim()).filter(Boolean)
      if (editingPersona) {
        await apiFetch(`${BASE}/api/admin/personas/${editingPersona.id}`, {
          method: 'PUT', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ ...personaForm, active: true, forumIds }),
        })
      } else {
        await apiFetch(`${BASE}/api/admin/personas`, {
          method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ ...personaForm, forumIds }),
        })
      }
      const rows: { persona: AiPersona }[] = await apiFetch(`${BASE}/api/admin/personas`)
      setPersonas(rows.map(r => r.persona))
      setEditingPersona(null)
      setPersonaForm({ name: '', slug: '', systemPrompt: '', model: 'claude-sonnet-4-6', triggerMode: 'mention', avatarUrl: '', forumIds: '' })
    } catch { setError('Failed to save persona') }
    finally { setPersonaSaving(false) }
  }

  const deletePersona = async (id: string) => {
    if (!confirm('Delete this persona?')) return
    await apiFetch(`${BASE}/api/admin/personas/${id}`, { method: 'DELETE' })
    setPersonas(prev => prev.filter(p => p.id !== id))
  }

  const saveHbConfig = async () => {
    setHbSaving(true)
    try {
      const updated = await apiFetch(`${BASE}/api/admin/heartbeat/config`, {
        method: 'PATCH', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(hbConfigEdit),
      })
      setHbConfig(updated)
    } catch { setError('Failed to save config') }
    finally { setHbSaving(false) }
  }

  const triggerHeartbeat = async () => {
    setHbTriggering(true)
    try {
      await apiFetch(`${BASE}/api/admin/heartbeat/trigger`, { method: 'POST' })
      const jobs = await apiFetch(`${BASE}/api/admin/heartbeat/jobs?limit=50`)
      setHbJobs(jobs)
    } catch { setError('Failed to trigger heartbeat') }
    finally { setHbTriggering(false) }
  }

  return (
    <div className="community-page admin-page">
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 8 }}>
        <h2 className="community-page-title" style={{ margin: 0 }}>Admin Console</h2>
        <span style={{ color: 'var(--text-muted)', fontSize: '0.9rem', flex: 1 }}>{TAB_LABELS[tab]}</span>
        <button className="nav-hamburger" onClick={() => setMenuOpen(o => !o)} aria-label="Menu">
          <span /><span /><span />
        </button>
      </div>

      {menuOpen && <div className="nav-drawer-overlay" onClick={() => setMenuOpen(false)} />}
      <div className={`nav-drawer${menuOpen ? ' open' : ''}`}>
        <button className="nav-drawer-close" onClick={() => setMenuOpen(false)}>✕</button>
        {(Object.keys(TAB_LABELS) as Tab[]).map(t => (
          <button
            key={t}
            className={`nav-community-link${tab === t ? ' active' : ''}`}
            onClick={() => { setTab(t); setMenuOpen(false) }}
          >
            {TAB_LABELS[t]}
            {t === 'blog-queue' && queue.length > 0 && <span className="admin-badge" style={{ marginLeft: 6 }}>{queue.length}</span>}
            {t === 'forum-reports' && forumReports.length > 0 && <span className="admin-badge" style={{ marginLeft: 6 }}>{forumReports.length}</span>}
          </button>
        ))}
      </div>

      {error && <p className="auth-error">{error} <button onClick={() => setError(null)}>✕</button></p>}
      {loading && <div className="forum-loading">Loading…</div>}

      {/* ── Users ── */}
      {tab === 'users' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
          <div className="admin-toolbar">
            <div className="admin-search-row">
              <input
                className="admin-search-input"
                placeholder="Search email or display name…"
                value={userSearchInput}
                onChange={e => setUserSearchInput(e.target.value)}
                onKeyDown={e => {
                  if (e.key === 'Enter') {
                    setUserSearch(userSearchInput); setUsersPage(1)
                    loadUsers(1, userSearchInput, userFilterRole, userFilterStatus)
                  }
                }}
              />
              <button className="admin-action-btn" onClick={() => {
                setUserSearch(userSearchInput); setUsersPage(1)
                loadUsers(1, userSearchInput, userFilterRole, userFilterStatus)
              }}>Search</button>
            </div>
            <div className="admin-filter-row">
              <select className="admin-role-select" value={userFilterRole} onChange={e => {
                setUserFilterRole(e.target.value); setUsersPage(1)
                loadUsers(1, userSearch, e.target.value, userFilterStatus)
              }}>
                <option value="">All roles</option>
                <option value="user">user</option>
                <option value="admin">admin</option>
                <option value="moderator">moderator</option>
                <option value="author">author</option>
              </select>
              <select className="admin-role-select" value={userFilterStatus} onChange={e => {
                setUserFilterStatus(e.target.value); setUsersPage(1)
                loadUsers(1, userSearch, userFilterRole, e.target.value)
              }}>
                <option value="">All statuses</option>
                <option value="active">active</option>
                <option value="pending">pending</option>
                <option value="suspended">suspended</option>
                <option value="restricted">restricted</option>
              </select>
              <button className="btn-primary" style={{ marginLeft: 'auto' }} onClick={() => setInviteOpen(true)}>+ Invite User</button>
            </div>
          </div>

          {!loading && (
            <>
              <div className="admin-table-wrap">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Email / Display Name</th>
                      <th>Role</th>
                      <th>Status</th>
                      <th>Verified</th>
                      <th>Joined</th>
                    </tr>
                  </thead>
                  <tbody>
                    {users.map(u => (
                      <tr key={u.id} style={{ opacity: u.status === 'suspended' ? 0.6 : 1 }}>
                        <td>
                          <button className="admin-email-btn" onClick={() => openUserModal(u)}>{u.email}</button>
                          {u.displayName && <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)' }}>{u.displayName}</div>}
                        </td>
                        <td>{u.role}</td>
                        <td>
                          <span style={{ color: u.status === 'suspended' ? '#f85149' : u.status === 'active' ? '#3fb950' : '#8b949e' }}>
                            {u.status}
                          </span>
                        </td>
                        <td>{u.emailVerified ? '✓' : '✗'}</td>
                        <td>{new Date(u.createdAt).toLocaleDateString()}</td>
                      </tr>
                    ))}
                    {users.length === 0 && (
                      <tr><td colSpan={5} style={{ textAlign: 'center', color: 'var(--text-muted)', padding: 24 }}>No users found.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>

              {usersTotal > PAGE_SIZE && (
                <div className="admin-pagination">
                  <button className="admin-action-btn" disabled={usersPage <= 1} onClick={() => {
                    const p = usersPage - 1; setUsersPage(p); loadUsers(p)
                  }}>← Prev</button>
                  <span>Page {usersPage} of {Math.ceil(usersTotal / PAGE_SIZE)} ({usersTotal} users)</span>
                  <button className="admin-action-btn" disabled={usersPage >= Math.ceil(usersTotal / PAGE_SIZE)} onClick={() => {
                    const p = usersPage + 1; setUsersPage(p); loadUsers(p)
                  }}>Next →</button>
                </div>
              )}
            </>
          )}
        </div>
      )}

      {/* ── Blog review queue ── */}
      {tab === 'blog-queue' && !loading && (
        <div>
          {queue.length === 0 ? <p className="forum-empty">No articles pending review.</p> : (
            <div className="mod-queue">
              {queue.map(a => (
                <div key={a.id} className={`mod-queue-item${reviewingId === a.id ? ' expanded' : ''}`}>
                  <div className="mod-queue-header" onClick={() => setReviewingId(reviewingId === a.id ? null : a.id)}>
                    <div>
                      <div className="mod-queue-title">{a.title}</div>
                      <div className="mod-queue-meta">
                        <span className={`blog-status-badge ${a.status}`}>{a.status}</span>
                        <span>{new Date(a.createdAt).toLocaleDateString()}</span>
                        {a.subtitle && <span>{a.subtitle}</span>}
                      </div>
                    </div>
                    <span className="mod-queue-chevron">{reviewingId === a.id ? '▲' : '▼'}</span>
                  </div>

                  {reviewingId === a.id && (
                    <div className="mod-queue-body">
                      <div className="blog-article-content mod-article-preview"
                        dangerouslySetInnerHTML={{ __html: a.content }} />
                      <div className="mod-actions">
                        <textarea className="mod-note-input" rows={2} value={modNote}
                          onChange={e => setModNote(e.target.value)}
                          placeholder="Optional note to author (required for rejection / revision request)…" />
                        <div className="mod-action-buttons">
                          <button className="tiptap-action-btn primary" onClick={() => doModAction(a.id, 'approved')}>Approve</button>
                          <button className="tiptap-action-btn primary" onClick={() => doModAction(a.id, 'published')}>Approve &amp; Publish</button>
                          <button className="tiptap-action-btn secondary" onClick={() => doModAction(a.id, 'needs_revision')}>Request Revision</button>
                          <button className="tiptap-action-btn secondary" onClick={() => doModAction(a.id, 'rejected')}>Reject</button>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ── All Articles (admin) ── */}
      {tab === 'blog-all' && !loading && (
        <div className="admin-table-wrap">
          {allArticles.length === 0 ? <p className="forum-empty">No articles in database.</p> : (
            <table className="admin-table">
              <thead><tr><th>Title</th><th>Status</th><th>Visibility</th><th>Author ID</th><th>Created</th><th>Published</th></tr></thead>
              <tbody>
                {allArticles.map(a => (
                  <tr key={a.id}>
                    <td>{a.title}</td>
                    <td><span className={`blog-status-badge ${a.status}`}>{a.status}</span></td>
                    <td>{a.visibility}</td>
                    <td className="admin-id-cell">{a.authorId.slice(0, 8)}…</td>
                    <td>{new Date(a.createdAt).toLocaleDateString()}</td>
                    <td>{a.publishedAt ? new Date(a.publishedAt).toLocaleDateString() : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* ── Blog authors ── */}
      {tab === 'blog-authors' && !loading && (
        <div className="admin-table-wrap">
          {authors.length === 0 ? <p className="forum-empty">No authors yet.</p> : (
            <table className="admin-table">
              <thead><tr><th>User ID</th><th>Display Name</th><th>Trusted</th><th>Joined</th></tr></thead>
              <tbody>
                {authors.map(a => (
                  <tr key={a.userId}>
                    <td className="admin-id-cell">{a.userId.slice(0, 8)}…</td>
                    <td>{a.displayName ?? '—'}</td>
                    <td>
                      <button className={`admin-trust-btn${a.trusted ? ' trusted' : ''}`}
                        onClick={() => toggleTrusted(a)}>
                        {a.trusted ? '✓ Trusted' : 'Set Trusted'}
                      </button>
                    </td>
                    <td>{new Date(a.createdAt).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* ── Tags ── */}
      {tab === 'tags' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <form className="admin-grant-form" onSubmit={createTag}>
            <h4 style={{ margin: 0 }}>Create Tag</h4>
            <div className="admin-grant-fields">
              <input placeholder="Tag name" value={newTagName} onChange={e => setNewTagName(e.target.value)} className="papers-new-input" />
              <input placeholder="Description (optional)" value={newTagDesc} onChange={e => setNewTagDesc(e.target.value)} className="papers-new-input" style={{ flex: 2 }} />
              <button type="submit" className="tiptap-action-btn primary" disabled={!newTagName.trim()}>Create</button>
            </div>
          </form>
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead><tr><th>Name</th><th>Slug</th><th>Description</th><th></th></tr></thead>
              <tbody>
                {tags.map(t => (
                  <tr key={t.id}>
                    <td>{t.name}</td>
                    <td className="admin-id-cell">{t.slug}</td>
                    <td>{t.description ?? '—'}</td>
                    <td><button className="post-action post-action-delete" onClick={() => deleteTag(t.id)}>Delete</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Forum Tags ── */}
      {tab === 'forum-tags' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <form className="admin-grant-form" onSubmit={createForumTag}>
            <h4 style={{ margin: 0 }}>Create Forum Tag</h4>
            <div className="admin-grant-fields">
              <input placeholder="Tag name" value={newForumTagName} onChange={e => setNewForumTagName(e.target.value)} className="papers-new-input" />
              <input placeholder="Description (optional)" value={newForumTagDesc} onChange={e => setNewForumTagDesc(e.target.value)} className="papers-new-input" style={{ flex: 2 }} />
              <button type="submit" className="tiptap-action-btn primary" disabled={!newForumTagName.trim()}>Create</button>
            </div>
          </form>
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead><tr><th>Name</th><th>Slug</th><th>Description</th><th></th></tr></thead>
              <tbody>
                {forumTags.map(t => (
                  <tr key={t.id}>
                    <td>{t.name}</td>
                    <td className="admin-id-cell">{t.slug}</td>
                    <td>{t.description ?? '—'}</td>
                    <td><button className="post-action post-action-delete" onClick={() => deleteForumTag(t.id)}>Delete</button></td>
                  </tr>
                ))}
                {forumTags.length === 0 && <tr><td colSpan={4} style={{ color: 'var(--text-muted)', textAlign: 'center' }}>No forum tags yet.</td></tr>}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Forum Reports ── */}
      {tab === 'forum-reports' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Type</th>
                  <th>Reason</th>
                  <th>Target ID</th>
                  <th>Reported</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {forumReports.map(r => (
                  <tr key={r.id}>
                    <td><span className="admin-badge">{r.targetType}</span></td>
                    <td>{r.reason}</td>
                    <td className="admin-id-cell">{r.targetId}</td>
                    <td>{new Date(r.createdAt).toLocaleDateString()}</td>
                    <td>
                      <div style={{ display: 'flex', gap: 6 }}>
                        <button className="tiptap-action-btn secondary small" onClick={async () => {
                          await forumApi.resolveReport(r.id, 'dismissed')
                          setForumReports(prev => prev.filter(x => x.id !== r.id))
                        }}>Dismiss</button>
                        <button className="tiptap-action-btn secondary small" onClick={async () => {
                          await forumApi.resolveReport(r.id, 'warned')
                          setForumReports(prev => prev.filter(x => x.id !== r.id))
                        }}>Warn</button>
                        <button className="post-action post-action-delete" onClick={async () => {
                          await forumApi.resolveReport(r.id, 'deleted')
                          setForumReports(prev => prev.filter(x => x.id !== r.id))
                        }}>Delete</button>
                      </div>
                    </td>
                  </tr>
                ))}
                {forumReports.length === 0 && (
                  <tr><td colSpan={5} style={{ color: 'var(--text-muted)', textAlign: 'center' }}>No open reports.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Config ── */}
      {tab === 'config' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
          {['global', 'blog'].map(scope => (
            <div key={scope}>
              <h4 style={{ margin: '0 0 10px', textTransform: 'capitalize' }}>{scope} settings</h4>
              <div className="config-table">
                {config.filter(c => c.scope === scope).map(c => {
                  const k = `${c.scope}:${c.key}`
                  return (
                    <div key={k} className="config-row">
                      <div className="config-key">{c.key}</div>
                      <input className="config-value-input" value={editingConfig[k] ?? ''}
                        onChange={e => setEditingConfig(prev => ({ ...prev, [k]: e.target.value }))} />
                      <button className="tiptap-action-btn secondary small"
                        onClick={() => saveConfig(c.scope, c.key)}>Save</button>
                    </div>
                  )
                })}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* ── Announcements ── */}
      {tab === 'announcements' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
          <form className="admin-grant-form" onSubmit={saveAnn}>
            <h4 style={{ margin: 0 }}>{editingAnn ? 'Edit Announcement' : 'New Announcement'}</h4>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <input className="papers-new-input" placeholder="Title" value={annForm.title}
                onChange={e => setAnnForm(f => ({ ...f, title: e.target.value }))} required />
              <textarea className="mod-note-input" rows={6} placeholder="Body (HTML allowed)"
                value={annForm.body} onChange={e => setAnnForm(f => ({ ...f, body: e.target.value }))} required />
              <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 14 }}>
                  Priority
                  <select className="admin-role-select" value={annForm.priority}
                    onChange={e => setAnnForm(f => ({ ...f, priority: Number(e.target.value) }))}>
                    <option value={0}>Normal</option>
                    <option value={1}>High</option>
                    <option value={2}>Critical</option>
                  </select>
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 14 }}>
                  Expires
                  <input type="date" className="papers-new-input" value={annForm.expiresAt}
                    onChange={e => setAnnForm(f => ({ ...f, expiresAt: e.target.value }))} />
                </label>
                <button type="submit" className="tiptap-action-btn primary" disabled={annSaving || !annForm.title.trim()}>
                  {annSaving ? 'Saving…' : editingAnn ? 'Save Changes' : 'Create'}
                </button>
                {editingAnn && (
                  <button type="button" className="tiptap-action-btn secondary"
                    onClick={() => { setEditingAnn(null); setAnnForm({ title: '', body: '', priority: 0, expiresAt: '' }) }}>
                    Cancel
                  </button>
                )}
              </div>
            </div>
          </form>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {announcements.length === 0 && <p className="forum-empty">No announcements yet.</p>}
            {announcements.map(a => (
              <div key={a.id} className="ann-admin-card">
                <div className="ann-admin-card-header">
                  <div>
                    <strong className="ann-admin-title">{a.title}</strong>
                    <div className="ann-admin-meta">
                      <span className={`blog-status-badge ${a.publishedAt ? 'published' : 'draft'}`}>
                        {a.publishedAt ? 'Published' : 'Draft'}
                      </span>
                      {a.priority > 0 && <span className="ann-priority-badge">Priority {a.priority}</span>}
                      <span style={{ fontSize: 12, color: '#888' }}>{new Date(a.createdAt).toLocaleDateString()}</span>
                      {a.expiresAt && <span style={{ fontSize: 12, color: '#888' }}>Expires {new Date(a.expiresAt).toLocaleDateString()}</span>}
                    </div>
                  </div>
                  <div className="ann-admin-actions">
                    {a.publishedAt
                      ? <button className="tiptap-action-btn secondary" onClick={() => unpublishAnn(a.id)}>Unpublish</button>
                      : <button className="tiptap-action-btn primary" onClick={() => publishAnn(a.id)}>Publish &amp; Email</button>
                    }
                    <button className="tiptap-action-btn secondary" onClick={() => startEditAnn(a)}>Edit</button>
                    <button className="post-action post-action-delete" onClick={() => deleteAnn(a.id)}>Delete</button>
                  </div>
                </div>
                <div className="ann-admin-body" dangerouslySetInnerHTML={{ __html: a.body }} />
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ── Roles ── */}
      {tab === 'roles' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <form className="admin-grant-form" onSubmit={grantRole}>
            <h4 style={{ margin: 0 }}>Grant Context Role</h4>
            <div className="admin-grant-fields">
              <input placeholder="User ID" value={grantForm.userId}
                onChange={e => setGrantForm(f => ({ ...f, userId: e.target.value }))} className="papers-new-input" />
              <select className="admin-role-select" value={grantForm.module}
                onChange={e => setGrantForm(f => ({ ...f, module: e.target.value }))}>
                <option value="forum">forum</option>
                <option value="blog">blog</option>
                <option value="papers">papers</option>
              </select>
              <select className="admin-role-select" value={grantForm.role}
                onChange={e => setGrantForm(f => ({ ...f, role: e.target.value }))}>
                <option value="moderator">moderator</option>
                <option value="author">author</option>
                <option value="editor">editor</option>
                <option value="contributor">contributor</option>
              </select>
              <input placeholder="Scope ID (optional)" value={grantForm.scopeId}
                onChange={e => setGrantForm(f => ({ ...f, scopeId: e.target.value }))} className="papers-new-input" />
              <button type="submit" className="tiptap-action-btn primary" disabled={granting || !grantForm.userId.trim()}>
                {granting ? 'Granting…' : 'Grant'}
              </button>
            </div>
          </form>
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead><tr><th>User ID</th><th>Module</th><th>Role</th><th>Scope</th><th>Granted</th><th></th></tr></thead>
              <tbody>
                {roles.map(r => (
                  <tr key={r.id}>
                    <td className="admin-id-cell">{r.userId.slice(0, 8)}…</td>
                    <td>{r.module}</td><td>{r.role}</td>
                    <td>{r.scopeId ? r.scopeId.slice(0, 8) + '…' : '—'}</td>
                    <td>{new Date(r.grantedAt).toLocaleDateString()}</td>
                    <td><button className="post-action post-action-delete" onClick={() => revokeRole(r.id)}>Revoke</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
      {/* ── AI Personas ── */}
      {tab === 'personas' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
          <form onSubmit={savePersona} style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <h4 style={{ margin: 0 }}>{editingPersona ? 'Edit Persona' : 'Create Persona'}</h4>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
              <input className="papers-new-input" placeholder="Display name" value={personaForm.name} onChange={e => setPersonaForm(f => ({ ...f, name: e.target.value }))} required />
              <input className="papers-new-input" placeholder="slug (e.g. dr-ada)" value={personaForm.slug} onChange={e => setPersonaForm(f => ({ ...f, slug: e.target.value }))} required disabled={!!editingPersona} />
            </div>
            <textarea className="papers-new-input" placeholder="System prompt (persona instructions)" rows={5} value={personaForm.systemPrompt} onChange={e => setPersonaForm(f => ({ ...f, systemPrompt: e.target.value }))} required style={{ resize: 'vertical', fontFamily: 'monospace', fontSize: '0.82rem' }} />
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
              <select className="subscribe-select" value={personaForm.model} onChange={e => setPersonaForm(f => ({ ...f, model: e.target.value }))}>
                <option value="claude-sonnet-4-6">claude-sonnet-4-6</option>
                <option value="claude-haiku-4-5-20251001">claude-haiku-4-5 (fast)</option>
                <option value="claude-opus-4-8">claude-opus-4-8 (powerful)</option>
              </select>
              <select className="subscribe-select" value={personaForm.triggerMode} onChange={e => setPersonaForm(f => ({ ...f, triggerMode: e.target.value }))}>
                <option value="mention">On @mention only</option>
                <option value="always">Always (scheduled)</option>
                <option value="new_thread">New threads only</option>
              </select>
              <input className="papers-new-input" placeholder="Avatar URL (optional)" value={personaForm.avatarUrl} onChange={e => setPersonaForm(f => ({ ...f, avatarUrl: e.target.value }))} />
            </div>
            <input className="papers-new-input" placeholder="Forum IDs (comma-separated UUIDs)" value={personaForm.forumIds} onChange={e => setPersonaForm(f => ({ ...f, forumIds: e.target.value }))} />
            <div style={{ display: 'flex', gap: 8 }}>
              <button type="submit" className="tiptap-action-btn primary" disabled={personaSaving}>{personaSaving ? 'Saving…' : editingPersona ? 'Update' : 'Create'}</button>
              {editingPersona && <button type="button" className="tiptap-action-btn" onClick={() => { setEditingPersona(null); setPersonaForm({ name: '', slug: '', systemPrompt: '', model: 'claude-sonnet-4-6', triggerMode: 'mention', avatarUrl: '', forumIds: '' }) }}>Cancel</button>}
            </div>
          </form>
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead><tr><th>Name</th><th>Model</th><th>Trigger</th><th>Active</th><th>Created</th><th></th></tr></thead>
              <tbody>
                {personas.map(p => (
                  <tr key={p.id}>
                    <td><strong>{p.name}</strong><br /><span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>@{p.slug}</span></td>
                    <td style={{ fontSize: '0.8rem' }}>{p.model}</td>
                    <td>{p.triggerMode}</td>
                    <td>{p.active ? '✓' : '—'}</td>
                    <td>{new Date(p.createdAt).toLocaleDateString()}</td>
                    <td style={{ display: 'flex', gap: 6 }}>
                      <button className="post-action" onClick={() => { setEditingPersona(p); setPersonaForm({ name: p.name, slug: p.slug, systemPrompt: p.systemPrompt, model: p.model, triggerMode: p.triggerMode, avatarUrl: p.avatarUrl ?? '', forumIds: '' }) }}>Edit</button>
                      <button className="post-action post-action-delete" onClick={() => deletePersona(p.id)}>Delete</button>
                    </td>
                  </tr>
                ))}
                {personas.length === 0 && <tr><td colSpan={6} style={{ textAlign: 'center', color: 'var(--text-muted)' }}>No personas yet.</td></tr>}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Heartbeat ── */}
      {tab === 'heartbeat' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <h4 style={{ margin: 0 }}>Configuration</h4>
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="tiptap-action-btn" onClick={triggerHeartbeat} disabled={hbTriggering}>{hbTriggering ? 'Triggering…' : 'Manual Trigger'}</button>
                <button className="tiptap-action-btn primary" onClick={saveHbConfig} disabled={hbSaving}>{hbSaving ? 'Saving…' : 'Save Config'}</button>
              </div>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: 10 }}>
              {Object.entries(hbConfigEdit).map(([k, v]) => (
                <label key={k} style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: '0.85rem' }}>
                  <span style={{ color: 'var(--text-muted)' }}>{k}</span>
                  <input className="papers-new-input" value={v} onChange={e => setHbConfigEdit(c => ({ ...c, [k]: e.target.value }))} />
                </label>
              ))}
            </div>
          </div>
          <div>
            <h4 style={{ margin: '0 0 10px' }}>Recent Jobs</h4>
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead><tr><th>Action</th><th>Status</th><th>Retries</th><th>Created</th><th>Completed</th><th>Error</th></tr></thead>
                <tbody>
                  {hbJobs.map(j => (
                    <tr key={j.id}>
                      <td style={{ fontSize: '0.82rem' }}>{j.actionType}</td>
                      <td><span style={{ color: j.status === 'Completed' ? '#6ee7a0' : j.status === 'Failed' ? 'var(--danger)' : 'var(--text-muted)' }}>{j.status}</span></td>
                      <td>{j.retryCount}</td>
                      <td style={{ fontSize: '0.78rem' }}>{new Date(j.createdAt).toLocaleString()}</td>
                      <td style={{ fontSize: '0.78rem' }}>{j.completedAt ? new Date(j.completedAt).toLocaleString() : '—'}</td>
                      <td style={{ fontSize: '0.78rem', color: 'var(--danger)', maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{j.error ?? ''}</td>
                    </tr>
                  ))}
                  {hbJobs.length === 0 && <tr><td colSpan={6} style={{ textAlign: 'center', color: 'var(--text-muted)' }}>No jobs yet.</td></tr>}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* ── Metrics ── */}
      {tab === 'metrics' && !loading && metrics && (() => {
        const { totals, forumActivity, dailyActivity, topHumans, topBots } = metrics
        const maxPosts = Math.max(...forumActivity.map(f => f.postsAll), 1)
        const maxDay   = Math.max(...dailyActivity.map(d => d.postsHuman + d.postsAi), 1)
        const TIER_COLOR: Record<string, string> = { bronze: '#cd7f32', silver: '#a0a0a0', gold: '#ffd700', platinum: '#00ccdd', legendary: '#c084fc', 'djehuti-svg': '#4fc3f7' }

        const SectionHeader = ({ id, title }: { id: string; title: string }) => (
          <button className="metrics-section-toggle" onClick={() => toggleSection(id)}>
            <h4 className="metrics-section-title" style={{ margin: 0 }}>{title}</h4>
            <span className="metrics-section-chevron">{collapsedSections[id] ? '▶' : '▼'}</span>
          </button>
        )

        const StatCard = ({ label, metric, all, human, ai }: { label: string; metric: string; all: number; human: number; ai: number }) => (
          <button className="metrics-stat-card metrics-stat-card-btn" onClick={() => openTimeseries(metric, label)}>
            <div className="metrics-stat-label">{label}</div>
            <div className="metrics-stat-all">{all.toLocaleString()}</div>
            <div className="metrics-stat-split">
              {human > 0 && <span className="metrics-human">{human.toLocaleString()} human</span>}
              {ai > 0 && <span className="metrics-ai">{ai.toLocaleString()} AI</span>}
              {human === 0 && ai === 0 && <span style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>click for trend</span>}
            </div>
          </button>
        )

        return (
          <div className="metrics-root">
            {/* Actions */}
            <section className="metrics-section" style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <button className="tiptap-action-btn primary" onClick={recomputeAchievements} disabled={recomputingAchievements}>
                {recomputingAchievements ? 'Recomputing…' : 'Recompute Achievements'}
              </button>
              {recomputeResult && <span style={{ fontSize: '0.85rem', color: recomputeResult.includes('failed') ? 'var(--danger)' : '#6ee7a0' }}>{recomputeResult}</span>}
            </section>

            {/* Summary cards */}
            <section className="metrics-section">
              <SectionHeader id="totals" title="Site Totals" />
              {!collapsedSections['totals'] && (
                <div className="metrics-cards-row" style={{ marginTop: 12 }}>
                  <StatCard label="Members"  metric="members"  all={totals.all.users}         human={totals.human.users}    ai={totals.ai.users} />
                  <StatCard label="Posts"    metric="posts"    all={totals.all.posts}          human={totals.human.posts}    ai={totals.ai.posts} />
                  <StatCard label="Threads"  metric="threads"  all={totals.all.threads}        human={totals.human.threads}  ai={totals.ai.threads} />
                  <StatCard label="Articles" metric="articles" all={totals.all.articles}       human={0} ai={0} />
                  <StatCard label="Upvotes"  metric="upvotes"  all={totals.all.votesGiven}     human={0} ai={0} />
                  <StatCard label="Badges"   metric="badges"   all={totals.all.achievements}   human={0} ai={0} />
                </div>
              )}
            </section>

            {/* Forum breakdown */}
            <section className="metrics-section">
              <SectionHeader id="forums" title="Posts by Forum" />
              {!collapsedSections['forums'] && (
                <div className="metrics-forum-bars" style={{ marginTop: 12 }}>
                  {forumActivity.map(f => (
                    <div key={f.forumId} className="metrics-forum-row">
                      <div className="metrics-forum-name" title={f.forumName}>{f.forumName}</div>
                      <div className="metrics-bar-track">
                        <div className="metrics-bar-human" style={{ width: `${(f.postsHuman / maxPosts) * 100}%` }} title={`${f.postsHuman} human`} />
                        <div className="metrics-bar-ai"    style={{ width: `${(f.postsAi    / maxPosts) * 100}%` }} title={`${f.postsAi} AI`} />
                      </div>
                      <div className="metrics-forum-counts">
                        <span className="metrics-human">{f.postsHuman}</span>
                        <span style={{ color: 'var(--text-muted)' }}>/</span>
                        <span className="metrics-ai">{f.postsAi}</span>
                        <span style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>({f.postsAll} total)</span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </section>

            {/* 30-day activity */}
            <section className="metrics-section">
              <SectionHeader id="daily" title="30-Day Post Activity" />
              {!collapsedSections['daily'] && (
                <div className="metrics-sparkline" style={{ marginTop: 12 }}>
                  {dailyActivity.map(d => {
                    const total = d.postsHuman + d.postsAi
                    const h = Math.round((total / maxDay) * 60)
                    const hH = Math.round((d.postsHuman / maxDay) * 60)
                    return (
                      <div key={d.date} className="metrics-spark-col" title={`${d.date}: ${d.postsHuman} human, ${d.postsAi} AI`}>
                        <div style={{ flex: 1, display: 'flex', alignItems: 'flex-end', gap: 1 }}>
                          <div className="metrics-bar-human" style={{ height: hH, width: 6 }} />
                          <div className="metrics-bar-ai"    style={{ height: h - hH, width: 6 }} />
                        </div>
                        <div className="metrics-spark-label">{d.date.slice(5)}</div>
                      </div>
                    )
                  })}
                  {dailyActivity.length === 0 && <p style={{ color: 'var(--text-muted)' }}>No post activity in the last 30 days.</p>}
                </div>
              )}
            </section>

            <section className="metrics-section">
              <SectionHeader id="humans" title="Top Human Members" />
              {!collapsedSections['humans'] && (
                <div className="admin-table-wrap" style={{ marginTop: 12 }}>
                  <table className="admin-table">
                    <thead><tr><th>Name</th><th>Posts</th><th>Threads</th><th>Votes</th><th>Badges</th></tr></thead>
                    <tbody>
                      {topHumans.map(u => (
                        <tr key={u.userId} style={{ cursor: 'pointer' }} onClick={() => loadMetricsUser(u.userId)}>
                          <td><button className="admin-email-btn">{u.displayName}</button></td>
                          <td>{u.posts}</td><td>{u.threads}</td><td>{u.votesReceived}</td><td>{u.achievements}</td>
                        </tr>
                      ))}
                      {topHumans.length === 0 && <tr><td colSpan={5} style={{ color: 'var(--text-muted)', textAlign: 'center' }}>No human users yet.</td></tr>}
                    </tbody>
                  </table>
                </div>
              )}
            </section>

            <section className="metrics-section">
              <SectionHeader id="bots" title="AI Persona Activity" />
              {!collapsedSections['bots'] && (
                <div className="admin-table-wrap" style={{ marginTop: 12 }}>
                  <table className="admin-table">
                    <thead><tr><th>Persona</th><th>Posts</th><th>Threads</th></tr></thead>
                    <tbody>
                      {topBots.map(u => (
                        <tr key={u.userId} style={{ cursor: 'pointer' }} onClick={() => loadMetricsUser(u.userId)}>
                          <td><button className="admin-email-btn">{u.displayName}</button></td>
                          <td>{u.posts}</td><td>{u.threads}</td>
                        </tr>
                      ))}
                      {topBots.length === 0 && <tr><td colSpan={3} style={{ color: 'var(--text-muted)', textAlign: 'center' }}>No AI personas yet.</td></tr>}
                    </tbody>
                  </table>
                </div>
              )}
            </section>

            {/* Anonymous Activity */}
            <section className="metrics-section">
              <SectionHeader id="anon" title="Anonymous Visitor Activity" />
              {!collapsedSections['anon'] && (
                <>
                  {/* Refresh from logs button */}
                  <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 14 }}>
                    <button className="tiptap-action-btn primary" onClick={refreshAnonFromLogs} disabled={anonRefreshing}>
                      {anonRefreshing ? 'Scanning logs…' : 'Scan Server Logs (30d)'}
                    </button>
                    {anonRefreshMsg && <span style={{ fontSize: '0.83rem', color: 'var(--text-muted)' }}>{anonRefreshMsg}</span>}
                  </div>

                  {!anonMetrics && <div style={{ color: 'var(--text-muted)', padding: '12px 0', fontSize: '0.9rem' }}>No anonymous traffic yet. Click Scan Server Logs to import history.</div>}
                  {anonMetrics && (
                    <>
                      <div className="metrics-stat-row" style={{ marginBottom: 16 }}>
                        {[
                          ['Unique Visitors (30d)', anonMetrics.uniqueVisitors30d],
                          ['Unique Visitors (all time)', anonMetrics.uniqueVisitorsAllTime],
                          ['Registrations (30d)', anonMetrics.conversions30d],
                          ['Conversion Rate', `${anonMetrics.conversionRatePct.toFixed(1)}%`],
                        ].map(([label, value]) => (
                          <div key={String(label)} className="metrics-stat-card-btn" style={{ cursor: 'default' }}>
                            <div className="metrics-stat-value">{String(value)}</div>
                            <div className="metrics-stat-label">{String(label)}</div>
                          </div>
                        ))}
                      </div>

                      {/* Daily visitors chart */}
                      {anonMetrics.dailyVisitors.length > 0 && (() => {
                        const maxV = Math.max(...anonMetrics.dailyVisitors.map(d => d.count), 1)
                        const BAR_H = 100
                        return (
                          <div style={{ marginBottom: 20 }}>
                            <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: 6 }}>Daily unique visitors — last 30 days</div>
                            <div style={{ overflowX: 'auto' }}>
                              <div style={{ display: 'flex', alignItems: 'flex-end', gap: 3, minWidth: anonMetrics.dailyVisitors.length * 20, height: BAR_H + 28, paddingTop: 4 }}>
                                {anonMetrics.dailyVisitors.map(d => {
                                  const h = Math.max(2, Math.round((d.count / maxV) * BAR_H))
                                  return (
                                    <div key={d.date} title={`${d.date}: ${d.count}`}
                                      style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 3, flex: '1 0 16px' }}>
                                      <span style={{ fontSize: '0.6rem', color: 'var(--text-muted)', opacity: d.count > 0 ? 1 : 0 }}>{d.count}</span>
                                      <div style={{ width: '100%', minWidth: 8, height: h, background: '#7c6af7', borderRadius: '3px 3px 0 0', opacity: 0.85 }} />
                                      <span style={{ fontSize: '0.55rem', color: 'var(--text-muted)', writingMode: 'vertical-rl', transform: 'rotate(180deg)' }}>{d.date.slice(5)}</span>
                                    </div>
                                  )
                                })}
                              </div>
                            </div>
                          </div>
                        )
                      })()}

                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
                        {/* Traffic sources */}
                        <div>
                          <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: 6 }}>Traffic sources (30d)</div>
                          <div className="admin-table-wrap">
                            <table className="admin-table">
                              <thead><tr><th>Source</th><th>Visits</th></tr></thead>
                              <tbody>
                                {anonMetrics.referrers.map(r => (
                                  <tr key={r.referrer}><td>{r.referrer}</td><td>{r.visits.toLocaleString()}</td></tr>
                                ))}
                                {anonMetrics.referrers.length === 0 && (
                                  <tr><td colSpan={2} style={{ color: 'var(--text-muted)', textAlign: 'center' }}>No data yet.</td></tr>
                                )}
                              </tbody>
                            </table>
                          </div>
                        </div>

                        {/* Countries */}
                        <div>
                          <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: 6 }}>Countries (30d)</div>
                          <div className="admin-table-wrap">
                            <table className="admin-table">
                              <thead><tr><th>Country</th><th>Visitors</th></tr></thead>
                              <tbody>
                                {(anonMetrics.countries ?? []).map(c => (
                                  <tr key={c.country}><td>{c.country}</td><td>{c.visitors.toLocaleString()}</td></tr>
                                ))}
                                {(anonMetrics.countries ?? []).length === 0 && (
                                  <tr><td colSpan={2} style={{ color: 'var(--text-muted)', textAlign: 'center' }}>No geo data yet — scan logs.</td></tr>
                                )}
                              </tbody>
                            </table>
                          </div>
                        </div>
                      </div>

                      {/* Top pages */}
                      <div style={{ marginBottom: 16 }}>
                        <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: 6 }}>Top pages (30d)</div>
                        <div className="admin-table-wrap">
                          <table className="admin-table">
                            <thead><tr><th>Path</th><th>Views</th><th>Unique</th></tr></thead>
                            <tbody>
                              {(anonMetrics.topPages ?? []).map((p, i) => (
                                <tr key={i}><td style={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{p.path}</td><td>{p.views}</td><td>{p.uniqueVisitors}</td></tr>
                              ))}
                              {(anonMetrics.topPages ?? []).length === 0 && (
                                <tr><td colSpan={3} style={{ color: 'var(--text-muted)', textAlign: 'center' }}>No data yet.</td></tr>
                              )}
                            </tbody>
                          </table>
                        </div>
                      </div>

                      {/* Recent visitors */}
                      {(anonMetrics.recentVisitors ?? []).length > 0 && (
                        <div>
                          <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: 6 }}>Recent visitors (30d)</div>
                          <div className="admin-table-wrap">
                            <table className="admin-table">
                              <thead><tr><th>IP</th><th>Location</th><th>Domain</th><th>Referrer</th><th>Path</th><th>When</th></tr></thead>
                              <tbody>
                                {anonMetrics.recentVisitors.map((v, i) => (
                                  <tr key={i}>
                                    <td style={{ fontFamily: 'monospace', fontSize: '0.75rem' }}>{v.ipAddress}</td>
                                    <td style={{ fontSize: '0.8rem' }}>{[v.city, v.region, v.country].filter(Boolean).join(', ') || '—'}</td>
                                    <td style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>{v.domain || '—'}</td>
                                    <td style={{ fontSize: '0.75rem', maxWidth: 120, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{v.referrer || 'Direct'}</td>
                                    <td style={{ fontFamily: 'monospace', fontSize: '0.75rem' }}>{v.path}</td>
                                    <td style={{ fontSize: '0.75rem', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>{new Date(v.viewedAt).toLocaleString()}</td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        </div>
                      )}
                    </>
                  )}
                </>
              )}
            </section>

            {/* Timeseries modal */}
            {timeseriesMetric && (
              <div className="admin-modal-backdrop" onClick={() => setTimeseriesMetric(null)}>
                <div className="admin-modal" style={{ maxWidth: 640 }} onClick={e => e.stopPropagation()}>
                  <div className="admin-modal-header">
                    <h3 style={{ margin: 0 }}>{timeseriesMetric} — last 30 days</h3>
                    <button className="admin-modal-close" onClick={() => setTimeseriesMetric(null)}>✕</button>
                  </div>
                  {timeseriesLoading && <div className="forum-loading">Loading…</div>}
                  {!timeseriesLoading && (() => {
                    if (timeseriesData.length === 0) return <p style={{ color: 'var(--text-muted)' }}>No activity in the last 30 days.</p>
                    const maxVal = Math.max(...timeseriesData.map(d => d.count), 1)
                    const BAR_H = 120
                    return (
                      <div style={{ overflowX: 'auto', paddingBottom: 8 }}>
                        <div style={{ display: 'flex', alignItems: 'flex-end', gap: 4, minWidth: timeseriesData.length * 22, height: BAR_H + 32, paddingTop: 8 }}>
                          {timeseriesData.map(d => {
                            const h = Math.max(2, Math.round((d.count / maxVal) * BAR_H))
                            return (
                              <div key={d.date} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, flex: '1 0 18px' }}
                                title={`${d.date}: ${d.count}`}>
                                <span style={{ fontSize: '0.65rem', color: 'var(--text-muted)', writingMode: 'vertical-rl', opacity: d.count > 0 ? 1 : 0 }}>{d.count}</span>
                                <div style={{ width: '100%', minWidth: 10, height: h, background: 'var(--accent)', borderRadius: '3px 3px 0 0', opacity: 0.85 }} />
                                <span style={{ fontSize: '0.6rem', color: 'var(--text-muted)', writingMode: 'vertical-rl', transform: 'rotate(180deg)' }}>{d.date.slice(5)}</span>
                              </div>
                            )
                          })}
                        </div>
                        <div style={{ marginTop: 12, fontSize: '0.8rem', color: 'var(--text-muted)', textAlign: 'right' }}>
                          Total: {timeseriesData.reduce((s, d) => s + d.count, 0).toLocaleString()} · Peak: {maxVal.toLocaleString()}
                        </div>
                      </div>
                    )
                  })()}
                </div>
              </div>
            )}

            {/* User drilldown modal */}
            {(metricsUser || metricsUserLoading) && (
              <div className="admin-modal-backdrop" onClick={() => setMetricsUser(null)}>
                <div className="admin-modal" style={{ maxWidth: 560 }} onClick={e => e.stopPropagation()}>
                  <div className="admin-modal-header">
                    <h3 style={{ margin: 0 }}>{metricsUser?.displayName ?? 'Loading…'}</h3>
                    <button className="admin-modal-close" onClick={() => setMetricsUser(null)}>✕</button>
                  </div>
                  {metricsUserLoading && <div className="forum-loading">Loading…</div>}
                  {metricsUser && !metricsUserLoading && (
                    <>
                      <p style={{ margin: '0 0 12px', color: 'var(--text-muted)', fontSize: '0.83rem' }}>
                        {metricsUser.isBot ? 'AI persona' : metricsUser.email}
                        {metricsUser.lastActiveAt && ` · last active ${new Date(metricsUser.lastActiveAt).toLocaleDateString()}`}
                      </p>
                      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: 10, marginBottom: 18 }}>
                        {([
                          ['Posts',     metricsUser.postCount],
                          ['Threads',   metricsUser.threadCount],
                          ['Votes in',  metricsUser.voteReceived],
                          ['Votes out', metricsUser.voteGiven],
                          ['Reactions', metricsUser.reactionCount],
                          ['Answers',   metricsUser.answerCount],
                          ['Streak',    metricsUser.loginStreak],
                          ['Days',      metricsUser.daysActive],
                        ] as [string, number][]).map(([lbl, val]) => (
                          <div key={lbl} style={{ background: 'var(--surface)', borderRadius: 6, padding: '8px 10px', textAlign: 'center' }}>
                            <div style={{ fontSize: '1.2rem', fontWeight: 700, color: 'var(--accent)' }}>{val}</div>
                            <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>{lbl}</div>
                          </div>
                        ))}
                      </div>
                      {metricsUser.achievements.length > 0 && (
                        <>
                          <h5 style={{ margin: '0 0 8px', color: 'var(--text-muted)', fontSize: '0.8rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Achievements</h5>
                          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                            {metricsUser.achievements.map(a => (
                              <div key={a.slug} title={`${a.name} — ${new Date(a.awardedAt).toLocaleDateString()}`}
                                style={{ display: 'flex', alignItems: 'center', gap: 6, background: 'var(--surface)', borderRadius: 6, padding: '5px 9px', fontSize: '0.8rem', border: `1px solid ${TIER_COLOR[a.tier] ?? '#555'}` }}>
                                <span>{a.icon}</span>
                                <span style={{ color: TIER_COLOR[a.tier] ?? 'var(--text-muted)' }}>{a.name}</span>
                              </div>
                            ))}
                          </div>
                        </>
                      )}
                    </>
                  )}
                </div>
              </div>
            )}
          </div>
        )
      })()}
      {tab === 'metrics' && !loading && !metrics && !error && (
        <p className="forum-empty">No metrics data available.</p>
      )}

      {tab === 'api-keys' && !loading && (
        <div style={{ maxWidth: 700 }}>
          <h3 style={{ marginBottom: 4 }}>Public API Keys</h3>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem', marginBottom: 20 }}>
            Keys grant access to <code>/api/public/v1/</code> — datasets and analysis only. No AI. Pass as <code>X-Api-Key: &lt;key&gt;</code> header.
          </p>

          {newKeyPlaintext && (
            <div style={{ background: 'var(--bg-card)', border: '1px solid var(--accent)', borderRadius: 8, padding: 16, marginBottom: 20 }}>
              <p style={{ margin: '0 0 8px', fontWeight: 600, color: 'var(--accent)' }}>Key generated — copy it now. It will not be shown again.</p>
              <code style={{ display: 'block', wordBreak: 'break-all', fontSize: '0.85rem', background: 'var(--bg)', padding: '8px 12px', borderRadius: 6 }}>{newKeyPlaintext}</code>
              <button style={{ marginTop: 10, fontSize: '0.8rem' }} className="blog-tab" onClick={() => { navigator.clipboard.writeText(newKeyPlaintext); }}>Copy</button>
              <button style={{ marginTop: 10, marginLeft: 8, fontSize: '0.8rem' }} className="blog-tab" onClick={() => setNewKeyPlaintext(null)}>Dismiss</button>
            </div>
          )}

          <form onSubmit={generateApiKey} style={{ display: 'flex', gap: 8, marginBottom: 24 }}>
            <input
              className="admin-search-input"
              placeholder="Key name (e.g. My Script)"
              value={newKeyName}
              onChange={e => setNewKeyName(e.target.value)}
              style={{ flex: 1 }}
            />
            <button type="submit" className="blog-tab active" disabled={keyCreating || !newKeyName.trim()}>
              {keyCreating ? 'Generating…' : 'Generate Key'}
            </button>
          </form>

          {apiKeys.length === 0 ? (
            <p style={{ color: 'var(--text-muted)' }}>No API keys yet.</p>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.88rem' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border)' }}>
                  <th style={{ textAlign: 'left', padding: '6px 8px' }}>Name</th>
                  <th style={{ textAlign: 'left', padding: '6px 8px' }}>Prefix</th>
                  <th style={{ textAlign: 'left', padding: '6px 8px' }}>Created</th>
                  <th style={{ textAlign: 'left', padding: '6px 8px' }}>Last Used</th>
                  <th style={{ textAlign: 'left', padding: '6px 8px' }}>Status</th>
                  <th style={{ padding: '6px 8px' }} />
                </tr>
              </thead>
              <tbody>
                {apiKeys.map(k => (
                  <tr key={k.id} style={{ borderBottom: '1px solid var(--border)', opacity: k.active ? 1 : 0.45 }}>
                    <td style={{ padding: '8px 8px' }}>{k.name}</td>
                    <td style={{ padding: '8px 8px' }}><code style={{ fontSize: '0.8rem' }}>{k.keyPrefix}…</code></td>
                    <td style={{ padding: '8px 8px', color: 'var(--text-muted)' }}>{new Date(k.createdAt).toLocaleDateString()}</td>
                    <td style={{ padding: '8px 8px', color: 'var(--text-muted)' }}>{k.lastUsedAt ? new Date(k.lastUsedAt).toLocaleDateString() : '—'}</td>
                    <td style={{ padding: '8px 8px' }}>
                      <span style={{ color: k.active ? 'var(--accent)' : 'var(--text-muted)' }}>{k.active ? 'Active' : 'Revoked'}</span>
                    </td>
                    <td style={{ padding: '8px 8px', textAlign: 'right' }}>
                      {k.active && (
                        <button className="blog-tab" style={{ fontSize: '0.78rem', color: 'var(--error, #e05)' }} onClick={() => revokeApiKey(k.id)}>Revoke</button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* ── Edit User Modal ── */}
      {userModal && (
        <div className="admin-modal-backdrop" onClick={() => setUserModal(null)}>
          <div className="admin-modal" onClick={e => e.stopPropagation()}>
            <div className="admin-modal-header">
              <h3 style={{ margin: 0 }}>Edit User</h3>
              <button className="admin-modal-close" onClick={() => setUserModal(null)}>✕</button>
            </div>
            <p style={{ margin: 0, color: 'var(--text-muted)', fontSize: '0.85rem' }}>
              {userModal.email} · Joined {new Date(userModal.createdAt).toLocaleDateString()}
              {userModal.lastLoginAt && ` · Last login ${new Date(userModal.lastLoginAt).toLocaleDateString()}`}
            </p>
            <div className="admin-detail-field">
              <label>Display Name</label>
              <input className="admin-inline-input" value={editName} onChange={e => setEditName(e.target.value)} placeholder="None set" />
            </div>
            <div className="admin-detail-field">
              <label>Role</label>
              <select className="admin-role-select" value={editRole} onChange={e => setEditRole(e.target.value)}>
                <option value="user">user</option>
                <option value="admin">admin</option>
                <option value="moderator">moderator</option>
                <option value="author">author</option>
              </select>
            </div>
            <div className="admin-detail-field">
              <label>Account Status</label>
              <select className="admin-role-select" value={editStatus} onChange={e => setEditStatus(e.target.value)}>
                <option value="active">active</option>
                <option value="pending">pending</option>
                <option value="suspended">suspended</option>
                <option value="restricted">restricted (read-only)</option>
              </select>
            </div>
            <div className="admin-modal-actions">
              <button className="btn-primary" onClick={saveUserModal} disabled={modalSaving}>{modalSaving ? 'Saving…' : 'Save Changes'}</button>
              {!userModal.emailVerified && (
                <button className="admin-action-btn" onClick={() => verifyUserEmail(userModal.id)}>Mark Verified</button>
              )}
              <button className="admin-action-btn" onClick={() => sendPasswordReset(userModal.id)}>Send Password Reset</button>
              <button className="admin-action-btn danger" style={{ marginLeft: 'auto' }} onClick={() => deleteUser(userModal.id, userModal.email)}>Delete User</button>
            </div>
          </div>
        </div>
      )}

      {/* ── Invite User Modal ── */}
      {inviteOpen && (
        <div className="admin-modal-backdrop" onClick={() => setInviteOpen(false)}>
          <div className="admin-modal" onClick={e => e.stopPropagation()}>
            <div className="admin-modal-header">
              <h3 style={{ margin: 0 }}>Invite User</h3>
              <button className="admin-modal-close" onClick={() => setInviteOpen(false)}>✕</button>
            </div>
            <form onSubmit={sendInvite} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
              <div className="admin-detail-field">
                <label>Email Address</label>
                <input className="admin-inline-input" type="email" value={inviteEmail} onChange={e => setInviteEmail(e.target.value)} placeholder="user@example.com" required />
              </div>
              <div className="admin-detail-field">
                <label>Role</label>
                <select className="admin-role-select" value={inviteRole} onChange={e => setInviteRole(e.target.value)}>
                  <option value="user">user</option>
                  <option value="author">author</option>
                  <option value="moderator">moderator</option>
                  <option value="admin">admin</option>
                </select>
              </div>
              <div className="admin-modal-actions">
                <button type="submit" className="btn-primary" disabled={inviting || !inviteEmail.trim()}>
                  {inviting ? 'Sending…' : 'Send Invite'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
