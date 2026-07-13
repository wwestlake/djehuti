import { useEffect, useState } from 'react'
import { Plus, Trash2 } from 'lucide-react'
import { profileApi } from '../../api/profileApi'
import type { UserProfile, UpdateProfileInput, ExternalLink } from '../../api/profileApi'
import { useAuth } from '../../contexts/AuthContext'
import ActivityFeed from '../../components/profile/ActivityFeed'
import AchievementsPreview from '../../components/profile/AchievementsPreview'
import SocialLinksDisplay from '../../components/profile/SocialLinksDisplay'

const emptyForm: UpdateProfileInput = {
  displayName: '', bio: '', avatarUrl: '', pronouns: '', location: '', externalLinks: [],
}

export default function ProfilePage() {
  const { user } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [editing, setEditing] = useState(false)
  const [form, setForm] = useState<UpdateProfileInput>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    profileApi.getMe()
      .then(p => {
        setProfile(p)
        setForm({
          displayName: p.displayName ?? '',
          bio: p.bio ?? '',
          avatarUrl: p.avatarUrl ?? '',
          pronouns: p.pronouns ?? '',
          location: p.location ?? '',
          externalLinks: p.externalLinks ?? [],
        })
      })
      .catch(() => setProfile(null))
  }, [])

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const updated = await profileApi.updateMe(form)
      setProfile(updated)
      setEditing(false)
    } catch {
      setError('Failed to save profile.')
    } finally {
      setSaving(false)
    }
  }

  const updateLink = (index: number, patch: Partial<ExternalLink>) => {
    setForm(f => ({ ...f, externalLinks: f.externalLinks.map((l, i) => i === index ? { ...l, ...patch } : l) }))
  }
  const addLink = () => setForm(f => ({ ...f, externalLinks: [...f.externalLinks, { platform: '', url: '' }] }))
  const removeLink = (index: number) => setForm(f => ({ ...f, externalLinks: f.externalLinks.filter((_, i) => i !== index) }))

  if (!user) return <p className="forum-login-prompt">Sign in to view your profile.</p>

  return (
    <div className="community-page profile-page" style={{ maxWidth: 720, margin: '0 auto' }}>
      <div style={{
        height: 120, borderRadius: 'var(--radius) var(--radius) 0 0', marginTop: '1.5rem',
        background: 'linear-gradient(135deg, color-mix(in srgb, var(--accent) 55%, transparent), color-mix(in srgb, var(--accent) 15%, transparent) 70%)',
      }} />

      <div className="profile-header" style={{ marginTop: -48, paddingLeft: 8 }}>
        <div className="profile-avatar-wrap" style={{ border: '4px solid var(--bg)', borderRadius: '50%' }}>
          {(profile?.avatarUrl || form.avatarUrl) ? (
            <img src={editing ? form.avatarUrl ?? '' : (profile?.avatarUrl ?? '')} alt="Avatar" className="profile-avatar" />
          ) : (
            <div className="profile-avatar-placeholder">
              {(profile?.displayName ?? user.email)[0]?.toUpperCase()}
            </div>
          )}
        </div>
        <div className="profile-info">
          <h2 className="profile-display-name" style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
            {profile?.displayName ?? user.email}
            {profile?.pronouns && <span style={{ fontSize: '0.85rem', fontWeight: 400, color: 'var(--text-muted)' }}>({profile.pronouns})</span>}
          </h2>
          {profile?.location && <p className="profile-meta">{profile.location}</p>}
        </div>
        {!editing && <button className="primary-action" onClick={() => setEditing(true)}>Edit Profile</button>}
      </div>

      {profile?.bio && !editing && <p className="profile-bio">{profile.bio}</p>}
      {profile?.externalLinks && profile.externalLinks.length > 0 && !editing && (
        <div style={{ margin: '12px 0' }}>
          <SocialLinksDisplay links={profile.externalLinks} />
        </div>
      )}

      {editing && (
        <form className="profile-form" onSubmit={handleSave}>
          {error && <p className="auth-error">{error}</p>}
          <label className="blog-editor-label">
            Display Name
            <input value={form.displayName} onChange={e => setForm(f => ({ ...f, displayName: e.target.value }))}
              maxLength={80} placeholder="How you appear to others" />
          </label>
          <label className="blog-editor-label">
            Pronouns
            <input value={form.pronouns} onChange={e => setForm(f => ({ ...f, pronouns: e.target.value }))}
              maxLength={40} placeholder="e.g. she/her, they/them" />
          </label>
          <label className="blog-editor-label">
            Bio
            <textarea value={form.bio} onChange={e => setForm(f => ({ ...f, bio: e.target.value }))}
              rows={4} maxLength={500} placeholder="A short bio…" />
          </label>
          <label className="blog-editor-label">
            Avatar URL
            <input value={form.avatarUrl} onChange={e => setForm(f => ({ ...f, avatarUrl: e.target.value }))}
              placeholder="https://…" />
          </label>
          <label className="blog-editor-label">
            Location
            <input value={form.location} onChange={e => setForm(f => ({ ...f, location: e.target.value }))}
              maxLength={100} placeholder="City, Country" />
          </label>

          <div className="blog-editor-label" style={{ display: 'block' }}>
            <span>Social links</span>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 6 }}>
              {form.externalLinks.map((link, i) => (
                <div key={i} style={{ display: 'flex', gap: 8 }}>
                  <input value={link.platform} onChange={e => updateLink(i, { platform: e.target.value })}
                    placeholder="Platform (e.g. GitHub)" style={{ flex: '0 0 160px' }} />
                  <input value={link.url} onChange={e => updateLink(i, { url: e.target.value })}
                    placeholder="https://…" style={{ flex: 1 }} />
                  <button type="button" className="post-action post-action-delete" onClick={() => removeLink(i)} aria-label="Remove link">
                    <Trash2 size={14} />
                  </button>
                </div>
              ))}
              <button type="button" className="blog-tab" onClick={addLink} style={{ alignSelf: 'flex-start', display: 'flex', alignItems: 'center', gap: 6 }}>
                <Plus size={14} /> Add link
              </button>
            </div>
          </div>

          <div style={{ display: 'flex', gap: '8px' }}>
            <button type="submit" className="primary-action" disabled={saving}>{saving ? 'Saving…' : 'Save'}</button>
            <button type="button" onClick={() => setEditing(false)}>Cancel</button>
          </div>
        </form>
      )}

      {!editing && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24, marginTop: 24 }}>
          <AchievementsPreview userId={user.id} />
          <div>
            <h3 style={{ margin: '0 0 10px', fontSize: '0.95rem' }}>Recent activity</h3>
            <ActivityFeed userId={user.id} />
          </div>
        </div>
      )}
    </div>
  )
}
