import { createContext, useContext, useEffect, useState } from 'react'

export type UserProfile = {
  id: string
  email: string
  displayName: string | null
  avatarUrl: string | null
  bio: string | null
  pronouns: string | null
  location: string | null
  role: string
  status: string
  createdAt: string
  roles: string[]
}

export type AuthContextType = {
  user: UserProfile | null
  isLoading: boolean
  isAuthenticated: boolean
  error: string | null
  login: (email: string, password: string) => Promise<void>
  signup: (email: string, password: string, hcaptchaToken: string) => Promise<void>
  logout: () => Promise<void>
  clearError: () => void
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

const API_BASE = '/djehuti'

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    checkAuth()
  }, [])

  const checkAuth = async () => {
    try {
      const response = await fetch(`${API_BASE}/api/auth/me`, { credentials: 'include' })
      if (response.ok) {
        const data = (await response.json()) as UserProfile
        setUser(data)
      }
    } catch (err) {
      console.error('Auth check failed:', err)
    } finally {
      setIsLoading(false)
    }
  }

  const login = async (email: string, password: string) => {
    setIsLoading(true)
    setError(null)
    try {
      const response = await fetch(`${API_BASE}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ email, password }),
      })
      if (!response.ok) {
        const text = await response.text()
        throw new Error(text || `Login failed with ${response.status}`)
      }
      const data = (await response.json()) as UserProfile
      setUser(data)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Login failed'
      setError(message)
      throw err
    } finally {
      setIsLoading(false)
    }
  }

  const signup = async (email: string, password: string, hcaptchaToken: string) => {
    setIsLoading(true)
    setError(null)
    try {
      const response = await fetch(`${API_BASE}/api/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password, hcaptchaToken }),
      })
      if (!response.ok) {
        const text = await response.text()
        throw new Error(text || `Signup failed with ${response.status}`)
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Signup failed'
      setError(message)
      throw err
    } finally {
      setIsLoading(false)
    }
  }

  const logout = async () => {
    setIsLoading(true)
    try {
      await fetch(`${API_BASE}/api/auth/logout`, { method: 'POST', credentials: 'include' })
      setUser(null)
    } catch (err) {
      console.error('Logout failed:', err)
    } finally {
      setIsLoading(false)
    }
  }

  const clearError = () => setError(null)

  return (
    <AuthContext.Provider value={{ user, isLoading, isAuthenticated: !!user, error, login, signup, logout, clearError }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) throw new Error('useAuth must be used within AuthProvider')
  return context
}
