import { useSectionForm } from '../useSectionForm'
import type { UserPrefs } from '../../../api/preferencesApi'

const KEYS = [
  'email_notify_replies', 'email_notify_mentions', 'email_notify_achievements',
  'email_notify_announcements', 'email_notify_blog_comments', 'email_notify_paper_collaborators',
  'inapp_notify_replies', 'inapp_notify_mentions', 'inapp_notify_achievements', 'inapp_notify_announcements',
]

interface Props { prefs: UserPrefs; onSave: (u: Partial<UserPrefs>) => Promise<void> }

function Toggle({ label, value, onChange }: { label: string; value: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="settings-toggle-row">
      <span className="settings-toggle-label">{label}</span>
      <button
        type="button"
        role="switch"
        aria-checked={value}
        className={`settings-toggle${value ? ' on' : ''}`}
        onClick={() => onChange(!value)}
      >
        <span className="settings-toggle-thumb" />
      </button>
    </label>
  )
}

export default function NotificationsSection({ prefs, onSave }: Props) {
  const { draft, set, save, saving, saved } = useSectionForm(prefs, KEYS)

  return (
    <div className="settings-form">
      <p className="settings-group-label">Email Notifications</p>
      <Toggle label="Thread replies (watched threads)" value={!!draft.email_notify_replies} onChange={v => set('email_notify_replies', v)} />
      <Toggle label="@Mentions" value={!!draft.email_notify_mentions} onChange={v => set('email_notify_mentions', v)} />
      <Toggle label="Achievement unlocks" value={!!draft.email_notify_achievements} onChange={v => set('email_notify_achievements', v)} />
      <Toggle label="Platform announcements" value={!!draft.email_notify_announcements} onChange={v => set('email_notify_announcements', v)} />
      <Toggle label="Blog post comments" value={!!draft.email_notify_blog_comments} onChange={v => set('email_notify_blog_comments', v)} />
      <Toggle label="Paper collaborator edits" value={!!draft.email_notify_paper_collaborators} onChange={v => set('email_notify_paper_collaborators', v)} />

      <p className="settings-group-label" style={{ marginTop: 20 }}>In-App Notifications</p>
      <Toggle label="Thread replies" value={!!draft.inapp_notify_replies} onChange={v => set('inapp_notify_replies', v)} />
      <Toggle label="@Mentions" value={!!draft.inapp_notify_mentions} onChange={v => set('inapp_notify_mentions', v)} />
      <Toggle label="Achievement unlocks" value={!!draft.inapp_notify_achievements} onChange={v => set('inapp_notify_achievements', v)} />
      <Toggle label="Platform announcements" value={!!draft.inapp_notify_announcements} onChange={v => set('inapp_notify_announcements', v)} />

      <div className="settings-save-row">
        <button className="primary-action" onClick={() => save(onSave)} disabled={saving}>
          {saving ? 'Saving…' : saved ? 'Saved ✓' : 'Save'}
        </button>
      </div>
    </div>
  )
}
