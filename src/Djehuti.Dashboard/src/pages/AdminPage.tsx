import { useEffect, useState } from 'react'
import { useAuth } from '../contexts/AuthContext'

interface AdminUser {
  id: string
  email: string
  displayName: string | null
  role: string
  status: string
  emailVerified: boolean
  createdAt: string
  lastLoginAt: string | null
}

interface PagedResponse {
  data: AdminUser[]
  total: number
  page: number
  pageSize: number
}

interface BlogArticleSummary {
  id: string
  title: string
  slug: string
  status: string
  createdAt: string
}

interface ContextRole {
  id: string
  userId: string
  module: string
  role: string
  scopeId: string | null
  grantedAt: string
}

type Tab = 'users' | 'blog-queue' | 'roles'

async function apiFetch(url: string, opts?: RequestInit) {
  const res = await fetch(url, { credentials: 'include', ...opts })
  if (!res.ok) throw new Error(res.statusText)
  return res.json()
}

const PAGE_SIZE = 25

export default function AdminPage() {
  const { user } = useAuth()
  const [tab, setTab] = useState<Tab>('users')
  const [queue, setQueue] = useState<BlogArticleSummary[]>([])
  const [roles, setRoles] = useState<ContextRole[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Users list state
  const [users, setUsers] = useState<AdminUser[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [filterRole, setFilterRole] = useState('')
  const [filterStatus, setFilterStatus] = useState('')
  const [searchInput, setSearchInput] = useState('')

  // Selected user modal
  const [modal, setModal] = useState<AdminUser | null>(null)
  const [editName, setEditName] = useState('')
  const [editRole, setEditRole] = useState('')
  const [editStatus, setEditStatus] = useState('')
  const [saving, setSaving] = useState(false)

  // Invite modal
  const [inviteOpen, setInviteOpen] = useState(false)
  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteRole, setInviteRole] = useState('user')
  const [inviting, setInviting] = useState(false)

  // Roles tab
  const [grantForm, setGrantForm] = useState({ userId: '', module: 'forum', role: 'moderator', scopeId: '' })
  const [granting, setGranting] = useState(false)

  const loadUsers = async (p = page, s = search, r = filterRole, st = filterStatus) => {
    setLoading(true)
    setError(null)
    try {
      const params = new URLSearchParams({ page: String(p), pageSize: String(PAGE_SIZE) })
      if (s) params.set('search', s)
      if (r) params.set('role', r)
      if (st) params.set('status', st)
      const res: PagedResponse = await apiFetch(`/api/admin/users?${params}`)
      setUsers(res.data)
      setTotal(res.total)
    } catch {
      setError('Failed to load users')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (!user || user.role !== 'admin') return
    if (tab === 'users') loadUsers(1, search, filterRole, filterStatus)
    else if (tab === 'blog-queue') {
      setLoading(true)
      apiFetch('/api/admin/blog/queue').then(setQueue).catch(() => setError('Failed to load queue')).finally(() => setLoading(false))
    } else if (tab === 'roles') {
      setLoading(true)
      apiFetch('/api/admin/context-roles').then(setRoles).catch(() => setError('Failed to load roles')).finally(() => setLoading(false))
    }
  }, [tab, user])

  const applySearch = () => {
    setSearch(searchInput)
    setPage(1)
    loadUsers(1, searchInput, filterRole, filterStatus)
  }

  const applyFilter = (r: string, st: string) => {
    setFilterRole(r)
    setFilterStatus(st)
    setPage(1)
    loadUsers(1, search, r, st)
  }

  const goPage = (p: number) => {
    setPage(p)
    loadUsers(p)
  }

  const openModal = (u: AdminUser) => {
    setModal(u)
    setEditName(u.displayName ?? '')
    setEditRole(u.role)
    setEditStatus(u.status)
  }

  const saveModal = async () => {
    if (!modal) return
    setSaving(true)
    try {
      await fetch(`/api/admin/users/${modal.id}/display-name`, {
        method: 'PATCH', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: editName }),
      })
      await fetch(`/api/admin/users/${modal.id}/role`, {
        method: 'PATCH', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ role: editRole }),
      })
      if (editStatus !== modal.status) {
        await fetch(`/api/admin/users/${modal.id}/suspend`, {
          method: 'PATCH', credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ suspend: editStatus === 'suspended' }),
        })
      }
      setUsers(prev => prev.map(u => u.id === modal.id
        ? { ...u, displayName: editName || null, role: editRole, status: editStatus }
        : u))
      setModal(null)
    } catch {
      setError('Failed to save changes.')
    } finally {
      setSaving(false)
    }
  }

  const verifyUser = async (id: string) => {
    try {
      await fetch(`/api/admin/users/${id}/verify`, { method: 'PATCH', credentials: 'include' })
      setUsers(prev => prev.map(u => u.id === id ? { ...u, emailVerified: true } : u))
      setModal(prev => prev && prev.id === id ? { ...prev, emailVerified: true } : prev)
    } catch { setError('Failed to verify user.') }
  }

  const sendPasswordReset = async (id: string) => {
    try {
      await fetch(`/api/admin/users/${id}/reset-password`, { method: 'POST', credentials: 'include' })
      alert('Password reset email sent.')
    } catch { setError('Failed to send reset email.') }
  }

  const deleteUser = async (id: string, email: string) => {
    if (!confirm(`Permanently delete ${email}? This cannot be undone.`)) return
    try {
      await fetch(`/api/admin/users/${id}`, { method: 'DELETE', credentials: 'include' })
      setUsers(prev => prev.filter(u => u.id !== id))
      setTotal(t => t - 1)
      setModal(null)
    } catch { setError('Failed to delete user.') }
  }

  const sendInvite = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!inviteEmail.trim()) return
    setInviting(true)
    try {
      await apiFetch('/api/admin/users/invite', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: inviteEmail.trim(), role: inviteRole }),
      })
      alert(`Invite sent to ${inviteEmail}`)
      setInviteEmail('')
      setInviteOpen(false)
      loadUsers(1)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to send invite.')
    } finally {
      setInviting(false)
    }
  }

  const publishArticle = async (id: string) => {
    try {
      await fetch(`/api/blog/articles/${id}/status`, {
        method: 'PATCH', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: 'published' }),
      })
      setQueue(prev => prev.filter(a => a.id !== id))
    } catch { setError('Failed to publish.') }
  }

  const rejectArticle = async (id: string) => {
    try {
      await fetch(`/api/blog/articles/${id}/status`, {
        method: 'PATCH', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: 'rejected' }),
      })
      setQueue(prev => prev.filter(a => a.id !== id))
    } catch { setError('Failed to reject.') }
  }

  const grantRole = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!grantForm.userId.trim()) return
    setGranting(true)
    try {
      await fetch('/api/roles/grant', {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId: grantForm.userId, module: grantForm.module, role: grantForm.role, scopeId: grantForm.scopeId || null }),
      })
      const updated = await apiFetch('/api/admin/context-roles')
      setRoles(updated)
      setGrantForm(f => ({ ...f, userId: '', scopeId: '' }))
    } catch { setError('Failed to grant role.') }
    finally { setGranting(false) }
  }

  const revokeRole = async (id: string) => {
    try {
      await fetch(`/api/admin/context-roles/${id}`, { method: 'DELETE', credentials: 'include' })
      setRoles(prev => prev.filter(r => r.id !== id))
    } catch { setError('Failed to revoke role.') }
  }

  if (!user || user.role !== 'admin') {
    return <p className="forum-login-prompt">Admin access required.</p>
  }

  const totalPages = Math.ceil(total / PAGE_SIZE)

  return (
    <div className="admin-page">
      <h2 className="admin-title">Admin Console</h2>

      <div className="blog-section-tabs">
        {(['users', 'blog-queue', 'roles'] as Tab[]).map(t => (
          <button key={t} className={`blog-tab${tab === t ? ' active' : ''}`} onClick={() => setTab(t)}>
            {t === 'users' ? 'Users' : t === 'blog-queue' ? 'Blog Queue' : 'Context Roles'}
          </button>
        ))}
      </div>

      {error && <p className="forum-error">{error} <button onClick={() => setError(null)}>✕</button></p>}
      {loading && <div className="forum-loading">Loading…</div>}

      {/* ── Users ── */}
      {tab === 'users' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {/* Toolbar */}
          <div className="admin-toolbar">
            <div className="admin-search-row">
              <input
                className="admin-search-input"
                placeholder="Search email or display name…"
                value={searchInput}
                onChange={e => setSearchInput(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && applySearch()}
              />
              <button className="admin-action-btn" onClick={applySearch}>Search</button>
            </div>
            <div className="admin-filter-row">
              <select className="admin-role-select" value={filterRole} onChange={e => applyFilter(e.target.value, filterStatus)}>
                <option value="">All roles</option>
                <option value="user">user</option>
                <option value="admin">admin</option>
                <option value="moderator">moderator</option>
                <option value="author">author</option>
              </select>
              <select className="admin-role-select" value={filterStatus} onChange={e => applyFilter(filterRole, e.target.value)}>
                <option value="">All statuses</option>
                <option value="active">active</option>
                <option value="pending">pending</option>
                <option value="suspended">suspended</option>
              </select>
              <button className="btn-primary" style={{ marginLeft: 'auto' }} onClick={() => setInviteOpen(true)}>+ Invite User</button>
            </div>
          </div>

          {/* Table */}
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
                      <button className="admin-email-btn" onClick={() => openModal(u)}>
                        {u.email}
                      </button>
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
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="admin-pagination">
              <button className="admin-action-btn" disabled={page <= 1} onClick={() => goPage(page - 1)}>← Prev</button>
              <span style={{ fontSize: '0.85rem' }}>Page {page} of {totalPages} ({total} users)</span>
              <button className="admin-action-btn" disabled={page >= totalPages} onClick={() => goPage(page + 1)}>Next →</button>
            </div>
          )}
        </div>
      )}

      {/* ── Blog Queue ── */}
      {tab === 'blog-queue' && !loading && (
        <div className="admin-table-wrap">
          {queue.length === 0 ? (
            <p className="forum-empty">No articles pending review.</p>
          ) : (
            <table className="admin-table">
              <thead><tr><th>Title</th><th>Submitted</th><th>Actions</th></tr></thead>
              <tbody>
                {queue.map(a => (
                  <tr key={a.id}>
                    <td>{a.title}</td>
                    <td>{new Date(a.createdAt).toLocaleDateString()}</td>
                    <td>
                      <button className="btn-primary" onClick={() => publishArticle(a.id)}>Publish</button>
                      {' '}
                      <button onClick={() => rejectArticle(a.id)}>Reject</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* ── Roles ── */}
      {tab === 'roles' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <form className="admin-grant-form" onSubmit={grantRole}>
            <h4 style={{ margin: 0 }}>Grant Context Role</h4>
            <div className="admin-grant-fields">
              <input placeholder="User ID" value={grantForm.userId} onChange={e => setGrantForm(f => ({ ...f, userId: e.target.value }))} className="papers-new-input" />
              <select className="admin-role-select" value={grantForm.module} onChange={e => setGrantForm(f => ({ ...f, module: e.target.value }))}>
                <option value="forum">forum</option>
                <option value="blog">blog</option>
                <option value="papers">papers</option>
              </select>
              <select className="admin-role-select" value={grantForm.role} onChange={e => setGrantForm(f => ({ ...f, role: e.target.value }))}>
                <option value="moderator">moderator</option>
                <option value="author">author</option>
                <option value="editor">editor</option>
                <option value="contributor">contributor</option>
                <option value="viewer">viewer</option>
              </select>
              <input placeholder="Scope ID (optional)" value={grantForm.scopeId} onChange={e => setGrantForm(f => ({ ...f, scopeId: e.target.value }))} className="papers-new-input" />
              <button type="submit" className="btn-primary" disabled={granting || !grantForm.userId.trim()}>
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
      {modal && (
        <div className="admin-modal-backdrop" onClick={() => setModal(null)}>
          <div className="admin-modal" onClick={e => e.stopPropagation()}>
            <div className="admin-modal-header">
              <h3 style={{ margin: 0 }}>Edit User</h3>
              <button className="admin-modal-close" onClick={() => setModal(null)}>✕</button>
            </div>
            <p style={{ margin: '4px 0 16px', color: 'var(--text-muted)', fontSize: '0.85rem' }}>
              {modal.email} · Joined {new Date(modal.createdAt).toLocaleDateString()}
              {modal.lastLoginAt && ` · Last login ${new Date(modal.lastLoginAt).toLocaleDateString()}`}
            </p>

            <div className="admin-detail-field">
              <label>Display Name</label>
              <input className="admin-inline-input" style={{ width: '100%' }} value={editName} onChange={e => setEditName(e.target.value)} placeholder="None set" />
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
              <button className="btn-primary" onClick={saveModal} disabled={saving}>{saving ? 'Saving…' : 'Save Changes'}</button>
              {!modal.emailVerified && (
                <button className="admin-action-btn" onClick={() => verifyUser(modal.id)}>Mark Email Verified</button>
              )}
              <button className="admin-action-btn" onClick={() => sendPasswordReset(modal.id)}>Send Password Reset</button>
              <button className="admin-action-btn admin-action-danger" style={{ marginLeft: 'auto' }} onClick={() => deleteUser(modal.id, modal.email)}>Delete User</button>
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
                <input className="admin-inline-input" style={{ width: '100%' }} type="email" value={inviteEmail} onChange={e => setInviteEmail(e.target.value)} placeholder="user@example.com" required />
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
