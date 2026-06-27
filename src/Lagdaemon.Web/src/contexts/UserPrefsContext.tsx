import { createContext, useContext, useState, useEffect, useCallback } from 'react'
import { preferencesApi, PREF_DEFAULTS } from '../api/preferencesApi'
import type { UserPrefs } from '../api/preferencesApi'
import { useAuth } from './AuthContext'

interface UserPrefsContextValue {
  prefs: UserPrefs
  loading: boolean
  patch: (updates: Partial<UserPrefs>) => Promise<void>
  reload: () => Promise<void>
}

const UserPrefsContext = createContext<UserPrefsContextValue>({
  prefs: { ...PREF_DEFAULTS },
  loading: false,
  patch: async () => {},
  reload: async () => {},
})

export function UserPrefsProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth()
  const [prefs, setPrefs] = useState<UserPrefs>({ ...PREF_DEFAULTS })
  const [loading, setLoading] = useState(false)

  const reload = useCallback(async () => {
    if (!user) { setPrefs({ ...PREF_DEFAULTS }); return }
    setLoading(true)
    try {
      const p = await preferencesApi.getPreferences()
      setPrefs({ ...PREF_DEFAULTS, ...p })
    } finally {
      setLoading(false)
    }
  }, [user])

  useEffect(() => { reload() }, [reload])

  const patch = useCallback(async (updates: Partial<UserPrefs>) => {
    const updated = await preferencesApi.patchPreferences(updates)
    setPrefs({ ...PREF_DEFAULTS, ...updated })
  }, [])

  return (
    <UserPrefsContext.Provider value={{ prefs, loading, patch, reload }}>
      {children}
    </UserPrefsContext.Provider>
  )
}

export const useUserPrefs = () => useContext(UserPrefsContext)
