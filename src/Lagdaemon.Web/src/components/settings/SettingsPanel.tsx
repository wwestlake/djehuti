import { useState, useEffect, useRef } from 'react'
import { useUserPrefs } from '../../contexts/UserPrefsContext'
import { useAuth } from '../../contexts/AuthContext'
import type { UserPrefs } from '../../api/preferencesApi'
import GeneralSection from './sections/GeneralSection'
import NotificationsSection from './sections/NotificationsSection'
import ForumSection from './sections/ForumSection'
import BlogSection from './sections/BlogSection'
import PapersSection from './sections/PapersSection'
import PrivacySection from './sections/PrivacySection'
import PatreonSection from './sections/PatreonSection'

export type SettingsSection = 'general' | 'notifications' | 'forum' | 'blog' | 'papers' | 'privacy' | 'patreon'

interface Props {
  open: boolean
  initialSection?: SettingsSection
  onClose: () => void
}

const SECTIONS: { id: SettingsSection; label: string }[] = [
  { id: 'general',       label: 'General' },
  { id: 'notifications', label: 'Notifications' },
  { id: 'forum',         label: 'Forum' },
  { id: 'blog',          label: 'Blog' },
  { id: 'papers',        label: 'Papers' },
  { id: 'patreon',       label: 'Patreon' },
  { id: 'privacy',       label: 'Privacy & Account' },
]

export default function SettingsPanel({ open, initialSection = 'general', onClose }: Props) {
  const { prefs, patch } = useUserPrefs()
  const { user } = useAuth()
  const [expanded, setExpanded] = useState<SettingsSection>(initialSection)
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (open) setExpanded(initialSection)
  }, [open, initialSection])

  useEffect(() => {
    if (!open) return
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [open, onClose])

  const toggle = (id: SettingsSection) =>
    setExpanded(prev => prev === id ? ('__none__' as SettingsSection) : id)

  const saveSection = async (updates: Partial<UserPrefs>) => {
    await patch(updates)
  }

  if (!user) return null

  return (
    <>
      {open && <div className="settings-backdrop" onClick={onClose} />}
      <div className={`settings-panel${open ? ' settings-panel-open' : ''}`} ref={panelRef} aria-hidden={!open}>
        <div className="settings-header">
          <span className="settings-title">Settings</span>
          <button className="settings-close" onClick={onClose} aria-label="Close settings">✕</button>
        </div>

        <div className="settings-body">
          {SECTIONS.map(s => (
            <div key={s.id} className="settings-section">
              <button
                className={`settings-section-toggle${expanded === s.id ? ' active' : ''}`}
                onClick={() => toggle(s.id)}
              >
                <span>{s.label}</span>
                <span className="settings-chevron">{expanded === s.id ? '▲' : '▼'}</span>
              </button>

              {expanded === s.id && (
                <div className="settings-section-body">
                  {s.id === 'general'       && <GeneralSection onSave={saveSection} />}
                  {s.id === 'notifications' && <NotificationsSection prefs={prefs} onSave={saveSection} />}
                  {s.id === 'forum'         && <ForumSection prefs={prefs} onSave={saveSection} />}
                  {s.id === 'blog'          && <BlogSection prefs={prefs} onSave={saveSection} />}
                  {s.id === 'papers'        && <PapersSection prefs={prefs} onSave={saveSection} />}
                  {s.id === 'patreon'       && <PatreonSection />}
                  {s.id === 'privacy'       && <PrivacySection prefs={prefs} onSave={saveSection} />}
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    </>
  )
}
