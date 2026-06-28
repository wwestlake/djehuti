import { useState } from 'react'
import { useAuth } from '../../../contexts/AuthContext'
import { useTheme, THEMES } from '../../../contexts/ThemeContext'
import type { UserPrefs } from '../../../api/preferencesApi'
import ImageUpload from '../../media/ImageUpload'

const API = '/djehuti/api'

interface Props {
  onSave?: (updates: Partial<UserPrefs>) => Promise<void>
}

export default function GeneralSection({ }: Props) {
  const { user } = useAuth()
  const { theme, setTheme } = useTheme()
  const [displayName, setDisplayName] = useState(user?.displayName ?? '')
  const [bio, setBio] = useState('')
  const [avatarUrl, setAvatarUrl] = useState('')
  const [pronouns, setPronouns] = useState('')
  const [location, setLocation] = useState('')
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const [loaded, setLoaded] = useState(false)

  // Load current profile on first render
  if (!loaded) {
    setLoaded(true)
    fetch(`${API}/users/me/profile`, { credentials: 'include' })
      .then(r => r.ok ? r.json() : null)
      .then(p => {
        if (!p) return
        setDisplayName(p.displayName ?? '')
        setBio(p.bio ?? '')
        setAvatarUrl(p.avatarUrl ?? '')
        setPronouns(p.pronouns ?? '')
        setLocation(p.location ?? '')
      })
  }

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      await fetch(`${API}/users/me/profile`, {
        method: 'PATCH', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName, bio, avatarUrl, pronouns, location }),
      })
      setSaved(true)
      setTimeout(() => setSaved(false), 2000)
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="settings-form" onSubmit={handleSave}>
      <div className="settings-field">
        <label>Theme</label>
        <select
          className="settings-input"
          value={theme}
          onChange={e => setTheme(e.target.value as typeof theme)}
        >
          {THEMES.map(t => (
            <option key={t.id} value={t.id}>{t.label}</option>
          ))}
        </select>
      </div>
      <div className="settings-field">
        <label>Display Name</label>
        <input maxLength={80} value={displayName} onChange={e => setDisplayName(e.target.value)} className="settings-input" />
      </div>
      <div className="settings-field">
        <label>Avatar</label>
        <ImageUpload
          module="avatar"
          currentUrl={avatarUrl || undefined}
          onUploaded={setAvatarUrl}
          previewShape="circle"
          label="Upload avatar"
        />
      </div>
      <div className="settings-field">
        <label>Bio</label>
        <textarea maxLength={500} value={bio} onChange={e => setBio(e.target.value)} className="settings-textarea" rows={3} />
        <span className="settings-char-count">{bio.length}/500</span>
      </div>
      <div className="settings-field">
        <label>Pronouns</label>
        <input maxLength={40} value={pronouns} onChange={e => setPronouns(e.target.value)} className="settings-input" placeholder="e.g. they/them" />
      </div>
      <div className="settings-field">
        <label>Location</label>
        <input maxLength={100} value={location} onChange={e => setLocation(e.target.value)} className="settings-input" />
      </div>
      <div className="settings-save-row">
        <button type="submit" className="primary-action" disabled={saving}>
          {saving ? 'Saving…' : saved ? 'Saved ✓' : 'Save'}
        </button>
      </div>
    </form>
  )
}
