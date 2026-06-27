import { useSectionForm } from '../useSectionForm'
import type { UserPrefs } from '../../../api/preferencesApi'

const KEYS = ['blog_default_visibility', 'blog_autosave_interval', 'blog_allow_comments', 'blog_comment_notify', 'blog_editor_mode']

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

export default function BlogSection({ prefs, onSave }: Props) {
  const { draft, set, save, saving, saved } = useSectionForm(prefs, KEYS)

  return (
    <div className="settings-form">
      <div className="settings-field">
        <label>New post visibility</label>
        <select className="settings-select" value={String(draft.blog_default_visibility ?? 'public')}
          onChange={e => set('blog_default_visibility', e.target.value)}>
          <option value="public">Public</option>
          <option value="members-only">Members only</option>
          <option value="draft">Save as draft</option>
        </select>
      </div>
      <div className="settings-field">
        <label>Draft auto-save interval</label>
        <select className="settings-select" value={String(draft.blog_autosave_interval ?? '1min')}
          onChange={e => set('blog_autosave_interval', e.target.value)}>
          <option value="30s">Every 30 seconds</option>
          <option value="1min">Every minute</option>
          <option value="5min">Every 5 minutes</option>
          <option value="off">Off</option>
        </select>
      </div>
      <div className="settings-field">
        <label>Preferred editor</label>
        <select className="settings-select" value={String(draft.blog_editor_mode ?? 'rich')}
          onChange={e => set('blog_editor_mode', e.target.value)}>
          <option value="rich">Rich text</option>
          <option value="markdown">Markdown</option>
        </select>
      </div>
      <Toggle label="Allow comments on new posts by default" value={!!draft.blog_allow_comments} onChange={v => set('blog_allow_comments', v)} />
      <Toggle label="Notify me of new comments (bell icon)" value={!!draft.blog_comment_notify} onChange={v => set('blog_comment_notify', v)} />
      <div className="settings-save-row">
        <button className="primary-action" onClick={() => save(onSave)} disabled={saving}>
          {saving ? 'Saving…' : saved ? 'Saved ✓' : 'Save'}
        </button>
      </div>
    </div>
  )
}
