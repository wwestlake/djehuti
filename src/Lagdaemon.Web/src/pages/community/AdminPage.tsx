import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { blogApi } from '../../api/blogApi'
import type { BlogArticle, BlogTag, BlogAuthor, SiteConfigEntry } from '../../api/blogApi'

const BASE = '/djehuti'

interface AdminUser {
  id: string; email: string; role: string; displayName: string | null; createdAt: string; emailVerified: boolean
}
interface ContextRole {
  id: string; userId: string; module: string; role: string; scopeId: string | null; grantedAt: string
}

type Tab = 'users' | 'blog-queue' | 'blog-authors' | 'tags' | 'config' | 'roles'

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

  // Users
  const [users, setUsers] = useState<AdminUser[]>([])

  // Blog queue
  const [queue, setQueue] = useState<BlogArticle[]>([])
  const [reviewingId, setReviewingId] = useState<string | null>(null)
  const [modNote, setModNote] = useState('')

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

  useEffect(() => {
    if (!user || user.role !== 'admin') return
    setLoading(true); setError(null)
    const loaders: Record<Tab, () => Promise<void>> = {
      users: () => apiFetch(`${BASE}/api/admin/users`).then(setUsers),
      'blog-queue': () => apiFetch(`${BASE}/api/admin/blog/queue`).then(setQueue),
      'blog-authors': () => blogApi.getAuthors().then(setAuthors),
      tags: () => blogApi.getTags().then(setTags),
      config: () => blogApi.getConfig().then(entries => {
        setConfig(entries)
        const map: Record<string, string> = {}
        entries.forEach(e => { map[`${e.scope}:${e.key}`] = e.value.replace(/^"|"$/g, '') })
        setEditingConfig(map)
      }),
      roles: () => apiFetch(`${BASE}/api/admin/context-roles`).then(setRoles),
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
      const updated = await blogApi.upsertAuthor(a.userId, { ...a, trusted: !a.Trusted })
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

  if (!user || user.role !== 'admin') return <p className="forum-login-prompt">Admin access required.</p>

  const TAB_LABELS: Record<Tab, string> = {
    users: 'Users', 'blog-queue': 'Review Queue', 'blog-authors': 'Authors', tags: 'Tags', config: 'Config', roles: 'Roles',
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
    </div>
  )
}
