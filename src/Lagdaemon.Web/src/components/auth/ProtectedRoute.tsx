import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'

interface Props {
  children: React.ReactNode
  requiredRole?: string
  // Admin or an active beta tester (any product) -- used to gate Downloads,
  // which is no longer public. Checked against /api/beta/my-access rather
  // than anything in the AuthContext user object, since "active beta
  // tester" isn't a role, it's a beta_testers row that can lapse.
  requireAdminOrBetaTester?: boolean
}

export function ProtectedRoute({ children, requiredRole, requireAdminOrBetaTester }: Props) {
  const { user, isLoading } = useAuth()
  const [access, setAccess] = useState<{ isAdmin: boolean; isActiveBetaTester: boolean } | null>(null)
  const [accessLoading, setAccessLoading] = useState(!!requireAdminOrBetaTester)

  useEffect(() => {
    if (!requireAdminOrBetaTester || !user) { setAccessLoading(false); return }
    setAccessLoading(true)
    fetch('/djehuti/api/beta/my-access', { credentials: 'include' })
      .then(r => r.ok ? r.json() : { isAdmin: false, isActiveBetaTester: false })
      .then(setAccess)
      .catch(() => setAccess({ isAdmin: false, isActiveBetaTester: false }))
      .finally(() => setAccessLoading(false))
  }, [requireAdminOrBetaTester, user])

  if (isLoading || accessLoading) return <div className="forum-loading">Loading…</div>
  if (!user) return <Navigate to="/" replace />
  if (requiredRole && user.role !== requiredRole) return <Navigate to="/" replace />
  if (requireAdminOrBetaTester && !(access?.isAdmin || access?.isActiveBetaTester)) return <Navigate to="/beta" replace />
  return <>{children}</>
}
