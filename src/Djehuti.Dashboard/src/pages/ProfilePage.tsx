import { useEffect, useState } from 'react'
import { profileApi } from '../components/profile/profileApi'
import type { UserProfile, UpdateProfileInput } from '../components/profile/profileApi'
import { useAuth } from '../contexts/AuthContext'

export default function ProfilePage() {
  const { user } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [editing, setEditing] = useState(false)
  const [form, setForm] = useState<UpdateProfileInput>({
    displayName: '', bio: '', avatarUrl: '', website: '', location: '',
  })
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
          website: p.website ?? '',
          location: p.location ?? '',
        })
      })
      .catch(() => {
        // no profile yet — start blank
        setProfile(null)
      })
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

  if (!user) return <p className="forum-login-prompt">Sign in to view your profile.</p>

  return (
    <div className="profile-page">
      <div className="profile-header">
        <div className="profile-avatar-wrap">
          {(profile?.avatarUrl || form.avatarUrl) ? (
            <img
              src={editing ? form.avatarUrl : (profile?.avatarUrl ?? '')}
              alt="Avatar"
              className="profile-avatar"
            />
          ) : (
            <div className="profile-avatar-placeholder">
              {(profile?.displayName ?? user.email)[0]?.toUpperCase()}
            </div>
          )}
        </div>
        <div className="profile-info">
          <h2 className="profile-display-name">{profile?.displayName ?? user.email}</h2>
          {profile?.location && <p className="profile-meta">{profile.location}</p>}
          {profile?.website && (
            <a className="profile-website" href={profile.website} target="_blank" rel="noreferrer">
              {profile.website}
            </a>
          )}
        </div>
        {!editing && (
          <button className="btn-primary" onClick={() => setEditing(true)}>Edit Profile</button>
        )}
      </div>

      {profile?.bio && !editing && (
        <p className="profile-bio">{profile.bio}</p>
      )}

      {editing && (
        <form className="profile-form" onSubmit={handleSave}>
          {error && <p className="forum-error">{error}</p>}

          <label className="blog-editor-label">
            Display Name
            <input
              value={form.displayName}
              onChange={e => setForm(f => ({ ...f, displayName: e.target.value }))}
              maxLength={80}
              placeholder="How you appear to others"
            />
          </label>

          <label className="blog-editor-label">
            Bio
            <textarea
              value={form.bio}
              onChange={e => setForm(f => ({ ...f, bio: e.target.value }))}
              rows={4}
              maxLength={500}
              placeholder="A short bio…"
            />
          </label>

          <label className="blog-editor-label">
            Avatar URL
            <input
              value={form.avatarUrl}
              onChange={e => setForm(f => ({ ...f, avatarUrl: e.target.value }))}
              placeholder="https://…"
            />
          </label>

          <label className="blog-editor-label">
            Website
            <input
              value={form.website}
              onChange={e => setForm(f => ({ ...f, website: e.target.value }))}
              placeholder="https://…"
            />
          </label>

          <label className="blog-editor-label">
            Location
            <input
              value={form.location}
              onChange={e => setForm(f => ({ ...f, location: e.target.value }))}
              maxLength={100}
              placeholder="City, Country"
            />
          </label>

          <div style={{ display: 'flex', gap: '8px' }}>
            <button type="submit" className="btn-primary" disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </button>
            <button type="button" onClick={() => setEditing(false)}>Cancel</button>
          </div>
        </form>
      )}
    </div>
  )
}
