import { useSectionForm } from '../useSectionForm'
import type { UserPrefs } from '../../../api/preferencesApi'

const KEYS = ['papers_default_visibility', 'papers_collab_notify', 'papers_comment_notify', 'papers_show_word_count', 'papers_citation_style']

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

export default function PapersSection({ prefs, onSave }: Props) {
  const { draft, set, save, saving, saved } = useSectionForm(prefs, KEYS)

  return (
    <div className="settings-form">
      <div className="settings-field">
        <label>New paper visibility</label>
        <select className="settings-select" value={String(draft.papers_default_visibility ?? 'private')}
          onChange={e => set('papers_default_visibility', e.target.value)}>
          <option value="public">Public</option>
          <option value="collaborators-only">Collaborators only</option>
          <option value="private">Private</option>
        </select>
      </div>
      <div className="settings-field">
        <label>Default citation style</label>
        <select className="settings-select" value={String(draft.papers_citation_style ?? 'APA')}
          onChange={e => set('papers_citation_style', e.target.value)}>
          <option value="APA">APA</option>
          <option value="MLA">MLA</option>
          <option value="Chicago">Chicago</option>
          <option value="IEEE">IEEE</option>
        </select>
      </div>
      <Toggle label="Notify me when a collaborator makes edits" value={!!draft.papers_collab_notify} onChange={v => set('papers_collab_notify', v)} />
      <Toggle label="Notify me of inline comments on my papers" value={!!draft.papers_comment_notify} onChange={v => set('papers_comment_notify', v)} />
      <Toggle label="Show live word count in editor" value={!!draft.papers_show_word_count} onChange={v => set('papers_show_word_count', v)} />
      <div className="settings-save-row">
        <button className="primary-action" onClick={() => save(onSave)} disabled={saving}>
          {saving ? 'Saving…' : saved ? 'Saved ✓' : 'Save'}
        </button>
      </div>
    </div>
  )
}
