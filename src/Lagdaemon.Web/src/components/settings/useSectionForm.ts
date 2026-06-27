import { useState } from 'react'
import type { UserPrefs } from '../../api/preferencesApi'

export function useSectionForm(prefs: UserPrefs, keys: string[]) {
  const initial = () => Object.fromEntries(keys.map(k => [k, prefs[k]]))
  const [draft, setDraft] = useState<Partial<UserPrefs>>(initial)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  // sync when prefs change (e.g. initial load)
  const reset = () => setDraft(initial())

  const set = (key: string, value: string | boolean | number) => {
    setDraft(prev => ({ ...prev, [key]: value }))
    setSaved(false)
  }

  const save = async (onSave: (updates: Partial<UserPrefs>) => Promise<void>) => {
    setSaving(true)
    try {
      await onSave(draft)
      setSaved(true)
      setTimeout(() => setSaved(false), 2000)
    } finally {
      setSaving(false)
    }
  }

  return { draft, set, save, saving, saved, reset }
}
