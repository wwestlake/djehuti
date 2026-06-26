import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { blogApi } from '../../api/blogApi'
import type { BlogArticle, BlogTag, BlogAuthor, SiteConfigEntry } from '../../api/blogApi'

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

type Tab = 'users' | 'blog-queue' | 'blog-all' | 'blog-authors' | 'tags' | 'config' | 'roles' | 'announcements'

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
      config: () => blogApi.getConfig().then(entries => {
        setConfig(entries)
        const map: Record<string, string> = {}
        entries.forEach(e => { map[`${e.scope}:${e.key}`] = e.value.replace(/^"|"$/g, '') })
        setEditingConfig(map)
      }),
      roles: () => apiFetch(`${BASE}/api/admin/context-roles`).then(setRoles),
      announcements: () => apiFetch(`${BASE}/api/admin/announcements`).then(setAnnouncements),
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

  const setUserRole = async (id: string, role: string) => {
    try {
      await apiFetch(`${BASE}/api/admin/users/${id}/role`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ role }) })
      setUsers(prev => prev.map(u => u.id === id ? { ...u, role } : u))
    } catch { setError('Failed to update role.') }
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
        await fetch(`${BASE}/api/admin/users/${userModal.id}/suspend`, {
          method: 'PATCH', credentials: 'include', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ suspend: editStatus === 'suspended' }),
        })
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

  const TAB_LABELS: Record<Tab, string> = {
    users: 'Users', 'blog-queue': 'Review Queue', 'blog-all': 'All Articles', 'blog-authors': 'Authors', tags: 'Tags', config: 'Config', roles: 'Roles', announcements: 'Announcements',
  }

  return (
    <div className="community-page admin-page">
      <h2 className="community-page-title">Admin Console</h2>

      <div className="blog-section-tabs">
        {(Object.keys(TAB_LABELS) as Tab[]).map(t => (
          <button key={t} className={`blog-tab${tab === t ? ' active' : ''}`} onClick={() => setTab(t)}>
            {TAB_LABELS[t]}
            {t === 'blog-queue' && queue.length > 0 && <span className="admin-badge">{queue.length}</span>}
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
