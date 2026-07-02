import { createContext, useContext, useEffect, useState } from 'react'

export type Theme = 'obsidian' | 'parchment' | 'starship' | 'terminal'

export const THEMES: { id: Theme; label: string }[] = [
  { id: 'obsidian',  label: 'Obsidian'  },
  { id: 'parchment', label: 'Parchment' },
  { id: 'starship',  label: 'Starship'  },
  { id: 'terminal',  label: 'Terminal'  },
]

const STORAGE_KEY = 'lagdaemon-mud-theme'

interface ThemeContextValue {
  theme: Theme
  setTheme: (t: Theme) => void
}

const DEFAULT_THEME: Theme = 'obsidian'
const ThemeContext = createContext<ThemeContextValue>({ theme: DEFAULT_THEME, setTheme: () => {} })

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(() => {
    const stored = localStorage.getItem(STORAGE_KEY) as Theme | null
    const resolved = stored && THEMES.some(option => option.id === stored) ? stored : DEFAULT_THEME
    document.documentElement.setAttribute('data-theme', resolved)
    return resolved
  })

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme)
  }, [theme])

  const setTheme = (t: Theme) => {
    localStorage.setItem(STORAGE_KEY, t)
    setThemeState(t)
  }

  return (
    <ThemeContext.Provider value={{ theme, setTheme }}>
      {children}
    </ThemeContext.Provider>
  )
}

export function useTheme() {
  return useContext(ThemeContext)
}
