import { createContext, useContext, useEffect, useState } from 'react'

export type Theme = 'dark' | 'light' | 'midnight' | 'solarized'

export const THEMES: { id: Theme; label: string }[] = [
  { id: 'dark',      label: 'Dark'      },
  { id: 'light',     label: 'Light'     },
  { id: 'midnight',  label: 'Midnight'  },
  { id: 'solarized', label: 'Solarized' },
]

const STORAGE_KEY = 'djehuti-theme'

interface ThemeContextValue {
  theme: Theme
  setTheme: (t: Theme) => void
}

const ThemeContext = createContext<ThemeContextValue>({
  theme: 'dark',
  setTheme: () => {},
})

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(() => {
    const stored = localStorage.getItem(STORAGE_KEY) as Theme | null
    return stored ?? 'dark'
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
