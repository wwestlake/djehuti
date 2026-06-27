import { useState } from 'react'
import { profileApi } from '../../../api/profileApi'

export default function PatreonSection() {
  const [memberId, setMemberId] = useState('')
  const [linked, setLinked] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleLink = async () => {
    if (!memberId.trim()) {
      setError('Please enter a Patreon member ID')
      return
    }

    setLoading(true)
    setError(null)

    try {
      await profileApi.linkPatreonAccount(memberId.trim())
      setLinked(true)
      setMemberId('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to link account')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="settings-section">
      <h2>Patreon Account</h2>

      <div className="settings-item">
        <div className="settings-item-label">
          <span>Link Patreon Account</span>
          <p>Connect your Patreon membership to unlock premium features</p>
        </div>

        {linked ? (
          <div className="settings-success">✓ Account linked</div>
        ) : (
          <div className="settings-input-group">
            <input
              type="text"
              placeholder="Paste your Patreon Member ID"
              value={memberId}
              onChange={e => setMemberId(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && handleLink()}
              disabled={loading}
              className="settings-input"
            />
            <button
              onClick={handleLink}
              disabled={loading || !memberId.trim()}
              className="settings-button"
            >
              {loading ? 'Linking...' : 'Link Account'}
            </button>
          </div>
        )}

        {error && <div className="settings-error">{error}</div>}

        <p className="settings-hint">
          Find your Member ID in your Patreon account URL or settings page
        </p>
      </div>
    </div>
  )
}
