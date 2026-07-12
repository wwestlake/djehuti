import { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { profileApi } from '../../api/profileApi'
import type { PublicProfile } from '../../api/profileApi'
import { patreonApi } from '../../api/patreonApi'
import type { PatreonTier } from '../../api/patreonApi'
import { useAuth } from '../../contexts/AuthContext'
import TierBadge from '../../components/TierBadge'
import ActivityFeed from '../../components/profile/ActivityFeed'
import AchievementsPreview from '../../components/profile/AchievementsPreview'
import SocialLinksDisplay from '../../components/profile/SocialLinksDisplay'

export default function PublicProfilePage() {
  const { userId } = useParams<{ userId: string }>()
  const { user: currentUser } = useAuth()
  const [profile, setProfile] = useState<PublicProfile | null>(null)
  const [tier, setTier] = useState<PatreonTier | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!userId) return
    setLoading(true)
    setError(null)
    profileApi.getPublicProfile(userId)
      .then(setProfile)
      .catch(() => setError('Could not load this profile.'))
      .finally(() => setLoading(false))
  }, [userId])

  useEffect(() => {
    if (!profile?.patreonTierId) { setTier(null); return }
    patreonApi.getTiers()
      .then(tiers => setTier(tiers.find(t => t.tierId === profile.patreonTierId) ?? null))
      .catch(() => setTier(null))
  }, [profile?.patreonTierId])

  if (loading) return <div style={{ maxWidth: 720, margin: '2rem auto', padding: '0 1rem' }}><p style={{ color: 'var(--text-muted)' }}>Loading…</p></div>
  if (error || !profile) return (
    <div style={{ maxWidth: 720, margin: '2rem auto', padding: '0 1rem' }}>
      <p>{error ?? 'Profile not found.'}</p>
    </div>
  )

  const isSelf = currentUser?.id === profile.id

  return (
    <div className="community-page profile-page" style={{ maxWidth: 720, margin: '0 auto' }}>
      <div style={{
        height: 120, borderRadius: 'var(--radius) var(--radius) 0 0', marginTop: '1.5rem',
        background: 'linear-gradient(135deg, color-mix(in srgb, var(--accent) 55%, transparent), color-mix(in srgb, var(--accent) 15%, transparent) 70%)',
      }} />

      <div className="profile-header" style={{ marginTop: -48, paddingLeft: 8 }}>
        <div className="profile-avatar-wrap" style={{ border: '4px solid var(--bg)', borderRadius: '50%' }}>
          {profile.avatarUrl ? (
            <img src={profile.avatarUrl} alt="Avatar" className="profile-avatar" />
          ) : (
            <div className="profile-avatar-placeholder">{profile.displayName[0]?.toUpperCase()}</div>
          )}
        </div>
        <div className="profile-info">
          <h2 className="profile-display-name" style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            {profile.displayName}
            {profile.pronouns && <span style={{ fontSize: '0.85rem', fontWeight: 400, color: 'var(--text-muted)' }}>({profile.pronouns})</span>}
            {tier && <TierBadge tierId={tier.tierId} tierName={tier.tierName} badgeColor={tier.badgeColor} badgeLabel={tier.badgeLabel} size="sm" />}
          </h2>
          <p className="profile-meta">
            {profile.location && <>{profile.location} · </>}
            Member since {new Date(profile.createdAt).toLocaleDateString(undefined, { year: 'numeric', month: 'long' })}
          </p>
        </div>
        {isSelf && <Link className="primary-action" to="/profile" style={{ textDecoration: 'none' }}>Edit your profile</Link>}
      </div>

      {profile.bio && <p className="profile-bio">{profile.bio}</p>}
      {profile.externalLinks.length > 0 && (
        <div style={{ margin: '12px 0' }}>
          <SocialLinksDisplay links={profile.externalLinks} />
        </div>
      )}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 24, marginTop: 24 }}>
        <AchievementsPreview userId={profile.id} />
        <div>
          <h3 style={{ margin: '0 0 10px', fontSize: '0.95rem' }}>Recent activity</h3>
          <ActivityFeed userId={profile.id} />
        </div>
      </div>
    </div>
  )
}
