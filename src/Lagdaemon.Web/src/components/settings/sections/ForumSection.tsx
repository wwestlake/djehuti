import { useSectionForm } from '../useSectionForm'
import type { UserPrefs } from '../../../api/preferencesApi'

const KEYS = [
  'forum_default_subscription', 'forum_show_bot_posts', 'forum_show_achievements_on_profile',
  'forum_compact_view', 'forum_signature',
]

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

export default function ForumSection({ prefs, onSave }: Props) {
  const { draft, set, save, saving, saved } = useSectionForm(prefs, KEYS)

  return (
    <div className="settings-form">
      <div className="settings-field">
        <label>Default watch level when I post</label>
        <select className="settings-select" value={String(draft.forum_default_subscription ?? 'tracking')}
          onChange={e => set('forum_default_subscription', e.target.value)}>
          <option value="watching">Watching — notify on every reply</option>
          <option value="tracking">Tracking — notify on @mention only</option>
          <option value="muted">Muted — no notifications</option>
        </select>
      </div>
      <Toggle label="Show AI Persona posts in threads" value={!!draft.forum_show_bot_posts} onChange={v => set('forum_show_bot_posts', v)} />
      <Toggle label="Display earned badges on my public profile" value={!!draft.forum_show_achievements_on_profile} onChange={v => set('forum_show_achievements_on_profile', v)} />
      <Toggle label="Compact thread list (no previews)" value={!!draft.forum_compact_view} onChange={v => set('forum_compact_view', v)} />
      <div className="settings-field">
        <label>Post signature <span className="settings-label-hint">(appended to all replies)</span></label>
        <textarea maxLength={200} rows={2} className="settings-textarea"
          value={String(draft.forum_signature ?? '')} onChange={e => set('forum_signature', e.target.value)} />
        <span className="settings-char-count">{String(draft.forum_signature ?? '').length}/200</span>
      </div>
      <div className="settings-save-row">
        <button className="primary-action" onClick={() => save(onSave)} disabled={saving}>
          {saving ? 'Saving…' : saved ? 'Saved ✓' : 'Save'}
        </button>
      </div>
    </div>
  )
}
