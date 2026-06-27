import { useState } from 'react'
import { LogIn, LogOut, User, ChevronDown, Settings } from 'lucide-react'
import { useAuth } from '../../contexts/AuthContext'
import type { SettingsSection } from '../settings/SettingsPanel'

type UserMenuProps = {
  onOpenLogin: () => void
  onOpenSettings?: (section?: SettingsSection) => void
}

export function UserMenu({ onOpenLogin, onOpenSettings }: UserMenuProps) {
  const { user, isAuthenticated, logout } = useAuth()
  const [isOpen, setIsOpen] = useState(false)

  if (!isAuthenticated) {
    return (
      <button className="auth-button-login" onClick={onOpenLogin} title="Sign in to Djehuti">
        <LogIn size={16} />
        <span>Sign in</span>
      </button>
    )
  }

  return (
    <div className="user-menu-container">
      <button className="user-menu-trigger" onClick={() => setIsOpen(!isOpen)} title="User menu">
        {user?.avatarUrl ? (
          <img src={user.avatarUrl} alt={user.displayName || user.email} className="user-avatar" />
        ) : (
          <div className="user-avatar-placeholder"><User size={16} /></div>
        )}
        <span className="user-display-name">{user?.displayName || user?.email}</span>
        <ChevronDown size={14} className={`chevron ${isOpen ? 'open' : ''}`} />
      </button>

      {isOpen && (
        <div className="user-menu-dropdown">
          <div className="user-menu-header">
            <strong>{user?.displayName || user?.email}</strong>
            {user?.role === 'admin' && <span className="admin-badge">Admin</span>}
          </div>
          <div className="user-menu-divider" />
          <a className="user-menu-item" href="/djehuti/" onClick={() => setIsOpen(false)}>
            <User size={14} />
            <span>Open Djehuti</span>
          </a>
          {onOpenSettings && (
            <button className="user-menu-item" onClick={() => { setIsOpen(false); onOpenSettings('general') }}>
              <Settings size={14} />
              <span>Settings</span>
            </button>
          )}
          <div className="user-menu-divider" />
          <button className="user-menu-item danger" onClick={async () => { setIsOpen(false); await logout() }}>
            <LogOut size={14} />
            <span>Sign out</span>
          </button>
        </div>
      )}

      {isOpen && (
        <button className="user-menu-backdrop" onClick={() => setIsOpen(false)} aria-label="Close menu" />
      )}
    </div>
  )
}
