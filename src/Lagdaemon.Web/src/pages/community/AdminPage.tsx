import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'

const BASE = '/djehuti'

interface AdminUser {
  id: string; email: string; role: string; displayName: string | null; createdAt: string; emailVerified: boolean
}
interface BlogArticleSummary {
  id: string; title: string; slug: string; status: string; createdAt: string
}
interface ContextRole {
  id: string; userId: string; module: string; role: string; scopeId: string | null; grantedAt: string
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
  const [grantForm, setGrantForm] = useState({ userId: '', module: 'forum', role: 'moderator', scopeId: '' })
  const [granting, setGranting] = useState(false)

  useEffect(() => {
    if (!user || user.role !== 'admin') return
    setLoading(true)
    setError(null)
    const loads: Promise<void>[] = []
    if (tab === 'users') {
      loads.push(apiFetch(`${BASE}/api/admin/users`).then(setUsers).catch(() => setError('Failed to load users')))
    } else if (tab === 'blog-queue') {
      loads.push(apiFetch(`${BASE}/api/admin/blog/queue`).then(setQueue).catch(() => setError('Failed to load queue')))
    } else if (tab === 'roles') {
      loads.push(apiFetch(`${BASE}/api/admin/context-roles`).then(setRoles).catch(() => setError('Failed to load roles')))
    }
    Promise.all(loads).finally(() => setLoading(false))
  }, [tab, user])

  const setUserRole = async (id: string, role: string) => {
    try {
      await apiFetch(`${BASE}/api/admin/users/${id}/role`, {
        method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ role }),
      })
      setUsers(prev => prev.map(u => u.id === id ? { ...u, role } : u))
    } catch { setError('Failed to update role.') }
  }

  const setArticleStatus = async (id: string, status: string) => {
    try {
      await apiFetch(`${BASE}/api/blog/articles/${id}/status`, {
        method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status }),
      })
      setQueue(prev => prev.filter(a => a.id !== id))
    } catch { setError('Failed to update article.') }
  }

  const grantRole = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!grantForm.userId.trim()) return
    setGranting(true)
    try {
      await apiFetch(`${BASE}/api/roles/grant`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId: grantForm.userId, module: grantForm.module, role: grantForm.role, scopeId: grantForm.scopeId || null }),
      })
      const updated = await apiFetch(`${BASE}/api/admin/context-roles`)
      setRoles(updated)
      setGrantForm(f => ({ ...f, userId: '', scopeId: '' }))
    } catch { setError('Failed to grant role.') } finally { setGranting(false) }
  }

  const revokeRole = async (id: string) => {
    try {
      await apiFetch(`${BASE}/api/admin/context-roles/${id}`, { method: 'DELETE' })
      setRoles(prev => prev.filter(r => r.id !== id))
    } catch { setError('Failed to revoke role.') }
  }

  if (!user || user.role !== 'admin') return <p className="forum-login-prompt">Admin access required.</p>

  return (
    <div className="community-page admin-page">
      <h2 className="community-page-title">Admin Console</h2>

      <div className="blog-section-tabs">
        {(['users', 'blog-queue', 'roles'] as Tab[]).map(t => (
          <button key={t} className={`blog-tab${tab === t ? ' active' : ''}`} onClick={() => setTab(t)}>
            {t === 'users' ? 'Users' : t === 'blog-queue' ? 'Blog Queue' : 'Context Roles'}
          </button>
        ))}
      </div>

      {error && <p className="auth-error">{error}</p>}
      {loading && <div className="forum-loading">Loading…</div>}

      {tab === 'users' && !loading && (
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead><tr><th>Email</th><th>Display Name</th><th>Role</th><th>Verified</th><th>Joined</th></tr></thead>
            <tbody>
              {users.map(u => (
                <tr key={u.id}>
                  <td>{u.email}</td>
                  <td>{u.displayName ?? '—'}</td>
                  <td>
                    <select value={u.role} onChange={e => setUserRole(u.id, e.target.value)} className="admin-role-select">
                      <option value="user">user</option>
                      <option value="admin">admin</option>
                    </select>
                  </td>
                  <td>{u.emailVerified ? '✓' : '✗'}</td>
                  <td>{new Date(u.createdAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'blog-queue' && !loading && (
        <div className="admin-table-wrap">
          {queue.length === 0 ? <p className="forum-empty">No articles pending review.</p> : (
            <table className="admin-table">
              <thead><tr><th>Title</th><th>Submitted</th><th>Actions</th></tr></thead>
              <tbody>
                {queue.map(a => (
                  <tr key={a.id}>
                    <td>{a.title}</td>
                    <td>{new Date(a.createdAt).toLocaleDateString()}</td>
                    <td>
                      <button className="primary-action" onClick={() => setArticleStatus(a.id, 'published')}>Publish</button>
                      {' '}
                      <button onClick={() => setArticleStatus(a.id, 'rejected')}>Reject</button>
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
                <option value="viewer">viewer</option>
              </select>
              <input placeholder="Scope ID (optional)" value={grantForm.scopeId}
                onChange={e => setGrantForm(f => ({ ...f, scopeId: e.target.value }))} className="papers-new-input" />
              <button type="submit" className="primary-action" disabled={granting || !grantForm.userId.trim()}>
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
    </div>
  )
}
