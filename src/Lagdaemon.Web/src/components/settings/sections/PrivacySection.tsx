import { useState } from 'react'
import { useSectionForm } from '../useSectionForm'
import type { UserPrefs } from '../../../api/preferencesApi'

const KEYS = ['privacy_show_online_status', 'privacy_show_profile_public', 'privacy_index_posts']
const API = '/djehuti/api'

interface Props { prefs: UserPrefs; onSave: (u: Partial<UserPrefs>) => Promise<void> }

function Toggle({ label, value, onChange }: { label: string; value: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="settings-toggle-row">
      <span className="settings-toggle-label">{label}</span>
      <button type="button" role="switch" aria-checked={value}
        className={`settings-toggle${value ? ' on' : ''}`} onClick={() => onChange(!value)}>
        <span className="settings-toggle-thumb" />
      </button>
    </label>
  )
}

export default function PrivacySection({ prefs, onSave }: Props) {
  const { draft, set, save, saving, saved } = useSectionForm(prefs, KEYS)
  const [showPwForm, setShowPwForm] = useState(false)
  const [currentPw, setCurrentPw] = useState('')
  const [newPw, setNewPw] = useState('')
  const [pwMsg, setPwMsg] = useState('')
  const [pwSaving, setPwSaving] = useState(false)
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false)
  const [deletePw, setDeletePw] = useState('')

  const changePassword = async (e: React.FormEvent) => {
    e.preventDefault()
    setPwSaving(true); setPwMsg('')
    try {
      const r = await fetch(`${API}/auth/change-password`, {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ currentPassword: currentPw, newPassword: newPw }),
      })
      setPwMsg(r.ok ? 'Password updated.' : 'Failed — check your current password.')
      if (r.ok) { setCurrentPw(''); setNewPw(''); setShowPwForm(false) }
    } finally { setPwSaving(false) }
  }

  return (
    <div className="settings-form">
      <Toggle label="Show my online status to other members" value={!!draft.privacy_show_online_status} onChange={v => set('privacy_show_online_status', v)} />
      <Toggle label="Allow non-members to view my profile" value={!!draft.privacy_show_profile_public} onChange={v => set('privacy_show_profile_public', v)} />
      <Toggle label="Allow my posts to appear in search results" value={!!draft.privacy_index_posts} onChange={v => set('privacy_index_posts', v)} />

      <div className="settings-save-row">
        <button className="primary-action" onClick={() => save(onSave)} disabled={saving}>
          {saving ? 'Saving…' : saved ? 'Saved ✓' : 'Save'}
        </button>
      </div>

      <hr className="settings-divider" />

      <div className="settings-action-row">
        <button className="settings-action-btn" onClick={() => setShowPwForm(v => !v)}>
          Change Password
        </button>
      </div>
      {showPwForm && (
        <form className="settings-pw-form" onSubmit={changePassword}>
          <input className="settings-input" type="password" placeholder="Current password"
            value={currentPw} onChange={e => setCurrentPw(e.target.value)} required />
          <input className="settings-input" type="password" placeholder="New password (min 8 chars)"
            value={newPw} onChange={e => setNewPw(e.target.value)} minLength={8} required />
          <button className="primary-action" type="submit" disabled={pwSaving}>
            {pwSaving ? 'Updating…' : 'Update Password'}
          </button>
          {pwMsg && <p className="settings-msg">{pwMsg}</p>}
        </form>
      )}

      <hr className="settings-divider" />

      <div className="settings-action-row">
        <button className="settings-action-btn settings-danger-btn" onClick={() => setShowDeleteConfirm(v => !v)}>
          Delete Account
        </button>
      </div>
      {showDeleteConfirm && (
        <div className="settings-delete-confirm">
          <p className="settings-danger-text">This is permanent and cannot be undone. Enter your password to confirm.</p>
          <input className="settings-input" type="password" placeholder="Your password"
            value={deletePw} onChange={e => setDeletePw(e.target.value)} />
          <button className="settings-action-btn settings-danger-btn" disabled={!deletePw}
            onClick={async () => {
              await fetch(`${API}/auth/delete-account`, {
                method: 'DELETE', credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ password: deletePw }),
              })
              window.location.href = '/'
            }}>
            Permanently Delete My Account
          </button>
        </div>
      )}
    </div>
  )
}
