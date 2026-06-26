import { useEffect, useState } from 'react'
import { useAuth } from '../contexts/AuthContext'

interface AdminUser {
  id: string
  email: string
  role: string
  displayName: string | null
  createdAt: string
  emailVerified: boolean
  status: string
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

export default function AdminPage() {
  const { user } = useAuth()
  const [tab, setTab] = useState<Tab>('users')
  const [users, setUsers] = useState<AdminUser[]>([])
  const [queue, setQueue] = useState<BlogArticleSummary[]>([])
  const [roles, setRoles] = useState<ContextRole[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [selectedUserId, setSelectedUserId] = useState<string | null>(null)
  const [editingDisplayName, setEditingDisplayName] = useState('')

  const [grantForm, setGrantForm] = useState({ userId: '', module: 'forum', role: 'moderator', scopeId: '' })
  const [granting, setGranting] = useState(false)

  useEffect(() => {
    if (!user || user.role !== 'admin') return
    setLoading(true)
    setError(null)
    const loads: Promise<void>[] = []
    if (tab === 'users') {
      loads.push(apiFetch('/api/admin/users').then(setUsers).catch(() => setError('Failed to load users')))
    } else if (tab === 'blog-queue') {
      loads.push(apiFetch('/api/admin/blog/queue').then(setQueue).catch(() => setError('Failed to load queue')))
    } else if (tab === 'roles') {
      loads.push(apiFetch('/api/admin/context-roles').then(setRoles).catch(() => setError('Failed to load roles')))
    }
    Promise.all(loads).finally(() => setLoading(false))
  }, [tab, user])

  const setUserRole = async (id: string, role: string) => {
    try {
      await fetch(`/api/admin/users/${id}/role`, {
        method: 'PATCH',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ role }),
      })
      setUsers(prev => prev.map(u => u.id === id ? { ...u, role } : u))
    } catch {
      setError('Failed to update role.')
    }
  }

  const saveDisplayName = async (id: string, displayName: string) => {
    try {
      await fetch(`/api/admin/users/${id}/display-name`, {
        method: 'PATCH',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName }),
      })
      setUsers(prev => prev.map(u => u.id === id ? { ...u, displayName: displayName || null } : u))
    } catch {
      setError('Failed to update display name.')
    }
  }

  const verifyUser = async (id: string) => {
    try {
      await fetch(`/api/admin/users/${id}/verify`, { method: 'PATCH', credentials: 'include' })
      setUsers(prev => prev.map(u => u.id === id ? { ...u, emailVerified: true } : u))
    } catch {
      setError('Failed to verify user.')
    }
  }

  const toggleSuspend = async (id: string, suspend: boolean) => {
    try {
      await fetch(`/api/admin/users/${id}/suspend`, {
        method: 'PATCH',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ suspend }),
      })
      setUsers(prev => prev.map(u => u.id === id ? { ...u, status: suspend ? 'suspended' : 'active' } : u))
    } catch {
      setError('Failed to update suspension.')
    }
  }

  const sendPasswordReset = async (id: string) => {
    try {
      await fetch(`/api/admin/users/${id}/reset-password`, { method: 'POST', credentials: 'include' })
      alert('Password reset email sent.')
    } catch {
      setError('Failed to send reset email.')
    }
  }

  const publishArticle = async (id: string) => {
    try {
      await fetch(`/api/blog/articles/${id}/status`, {
        method: 'PATCH',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: 'published' }),
      })
      setQueue(prev => prev.filter(a => a.id !== id))
    } catch {
      setError('Failed to publish.')
    }
  }

  const rejectArticle = async (id: string) => {
    try {
      await fetch(`/api/blog/articles/${id}/status`, {
        method: 'PATCH',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: 'rejected' }),
      })
      setQueue(prev => prev.filter(a => a.id !== id))
    } catch {
      setError('Failed to reject.')
    }
  }

  const grantRole = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!grantForm.userId.trim()) return
    setGranting(true)
    try {
      await fetch('/api/roles/grant', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userId: grantForm.userId,
          module: grantForm.module,
          role: grantForm.role,
          scopeId: grantForm.scopeId || null,
        }),
      })
      const updated = await apiFetch('/api/admin/context-roles')
      setRoles(updated)
      setGrantForm(f => ({ ...f, userId: '', scopeId: '' }))
    } catch {
      setError('Failed to grant role.')
    } finally {
      setGranting(false)
    }
  }

  const revokeRole = async (id: string) => {
    try {
      await fetch(`/api/admin/context-roles/${id}`, { method: 'DELETE', credentials: 'include' })
      setRoles(prev => prev.filter(r => r.id !== id))
    } catch {
      setError('Failed to revoke role.')
    }
  }

  if (!user || user.role !== 'admin') {
    return <p className="forum-login-prompt">Admin access required.</p>
  }

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

      {error && <p className="forum-error">{error}</p>}
      {loading && <div className="forum-loading">Loading…</div>}

      {tab === 'users' && !loading && (() => {
        const sel = users.find(u => u.id === selectedUserId) ?? null
        return (
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-start' }}>
            <div className="admin-user-list">
              {users.map(u => (
                <button
                  key={u.id}
                  className={`admin-user-row${selectedUserId === u.id ? ' selected' : ''}`}
                  onClick={() => { setSelectedUserId(u.id); setEditingDisplayName(u.displayName ?? '') }}
                >
                  <span className="admin-user-email">{u.email}</span>
                  <span className="admin-user-meta">
                    <span style={{ color: u.status === 'suspended' ? '#f85149' : u.status === 'active' ? '#3fb950' : '#8b949e' }}>{u.status}</span>
                    {' · '}{u.role}
                  </span>
                </button>
              ))}
            </div>

            {sel && (
              <div className="admin-user-detail">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <h4 style={{ margin: 0 }}>{sel.email}</h4>
                  <button onClick={() => setSelectedUserId(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: '1.2rem' }}>✕</button>
                </div>
                <p style={{ margin: '4px 0 12px', fontSize: '0.85rem', color: 'var(--text-muted)' }}>
                  Joined {new Date(sel.createdAt).toLocaleDateString()} · {sel.emailVerified ? 'Email verified' : 'Email not verified'}
                </p>

                <div className="admin-detail-field">
                  <label>Display Name</label>
                  <div style={{ display: 'flex', gap: 8 }}>
                    <input
                      className="admin-inline-input"
                      style={{ flex: 1, width: 'auto' }}
                      value={editingDisplayName}
                      onChange={e => setEditingDisplayName(e.target.value)}
                      placeholder="None set"
                    />
                    <button className="admin-action-btn" onClick={() => saveDisplayName(sel.id, editingDisplayName)}>Save</button>
                  </div>
                </div>

                <div className="admin-detail-field">
                  <label>Role</label>
                  <select
                    value={sel.role}
                    onChange={e => setUserRole(sel.id, e.target.value)}
                    className="admin-role-select"
                  >
                    <option value="user">user</option>
                    <option value="admin">admin</option>
                  </select>
                </div>

                <div className="admin-detail-actions">
                  {!sel.emailVerified && (
                    <button className="admin-action-btn" onClick={() => verifyUser(sel.id)}>Mark Email Verified</button>
                  )}
                  {sel.status === 'suspended' ? (
                    <button className="admin-action-btn" onClick={() => toggleSuspend(sel.id, false)}>Unsuspend</button>
                  ) : (
                    <button className="admin-action-btn admin-action-danger" onClick={() => toggleSuspend(sel.id, true)}>Suspend</button>
                  )}
                  <button className="admin-action-btn" onClick={() => sendPasswordReset(sel.id)}>Send Password Reset</button>
                </div>
              </div>
            )}
          </div>
        )
      })()}

      {tab === 'blog-queue' && !loading && (
        <div className="admin-table-wrap">
          {queue.length === 0 ? (
            <p className="forum-empty">No articles pending review.</p>
          ) : (
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Title</th>
                  <th>Submitted</th>
                  <th>Actions</th>
                </tr>
              </thead>
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

      {tab === 'roles' && !loading && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <form className="admin-grant-form" onSubmit={grantRole}>
            <h4 style={{ margin: 0 }}>Grant Context Role</h4>
            <div className="admin-grant-fields">
              <input
                placeholder="User ID"
                value={grantForm.userId}
                onChange={e => setGrantForm(f => ({ ...f, userId: e.target.value }))}
                className="papers-new-input"
              />
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
              <input
                placeholder="Scope ID (optional)"
                value={grantForm.scopeId}
                onChange={e => setGrantForm(f => ({ ...f, scopeId: e.target.value }))}
                className="papers-new-input"
              />
              <button type="submit" className="btn-primary" disabled={granting || !grantForm.userId.trim()}>
                {granting ? 'Granting…' : 'Grant'}
              </button>
            </div>
          </form>

          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>User ID</th>
                  <th>Module</th>
                  <th>Role</th>
                  <th>Scope</th>
                  <th>Granted</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {roles.map(r => (
                  <tr key={r.id}>
                    <td className="admin-id-cell">{r.userId.slice(0, 8)}…</td>
                    <td>{r.module}</td>
                    <td>{r.role}</td>
                    <td>{r.scopeId ? r.scopeId.slice(0, 8) + '…' : '—'}</td>
                    <td>{new Date(r.grantedAt).toLocaleDateString()}</td>
                    <td>
                      <button className="post-action post-action-delete" onClick={() => revokeRole(r.id)}>Revoke</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  )
}
