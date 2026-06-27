import { useEffect, useState } from 'react'
import { profileApi } from '../../api/profileApi'
import type { PublicProfile, ActivityItem } from '../../api/profileApi'

interface PublicProfilePageProps {
  userId: string
}

export default function PublicProfilePage({ userId }: PublicProfilePageProps) {
  const [profile, setProfile] = useState<PublicProfile | null>(null)
  const [activity, setActivity] = useState<ActivityItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    Promise.all([
      profileApi.getPublicProfile(userId),
      profileApi.getActivity(userId),
    ])
      .then(([p, feed]) => {
        setProfile(p)
        setActivity(feed.activity)
      })
      .catch(err => {
        setError(err.message || 'Failed to load profile')
      })
      .finally(() => setLoading(false))
  }, [userId])

  if (loading) {
    return (
      <div className="public-profile-page">
        <p>Loading profile…</p>
      </div>
    )
  }

  if (error || !profile) {
    return (
      <div className="public-profile-page">
        <p className="error">{error || 'Profile not found'}</p>
      </div>
    )
  }

  const joinDate = new Date(profile.createdAt).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })

  return (
    <div className="public-profile-page">
      <div className="profile-header">
        <div className="profile-avatar">
          {profile.avatarUrl ? (
            <img src={profile.avatarUrl} alt={profile.displayName} />
          ) : (
            <div className="avatar-placeholder">{profile.displayName.charAt(0).toUpperCase()}</div>
          )}
        </div>

        <div className="profile-info">
          <h1 className="profile-name">{profile.displayName}</h1>
          {profile.pronouns && <span className="profile-pronouns">{profile.pronouns}</span>}

          {profile.bio && <p className="profile-bio">{profile.bio}</p>}

          <div className="profile-meta">
            {profile.location && (
              <div className="meta-item">
                <span className="meta-icon">📍</span>
                <span>{profile.location}</span>
              </div>
            )}
            <div className="meta-item">
              <span className="meta-icon">📅</span>
              <span>Joined {joinDate}</span>
            </div>
          </div>
        </div>
      </div>

      <div className="profile-activity">
        <h2>Recent Activity</h2>

        {activity.length === 0 ? (
          <p className="activity-empty">No activity yet</p>
        ) : (
          <div className="activity-list">
            {activity.map((item, idx) => (
              <div key={`${item.type_}-${item.id}-${idx}`} className="activity-item">
                <span className="activity-type-badge" data-type={item.type_}>
                  {item.type_ === 'post' ? '💬' : item.type_ === 'thread' ? '📌' : '📝'}
                </span>
                <div className="activity-content">
                  <span className="activity-title">{item.title}</span>
                  <span className="activity-date">
                    {new Date(item.createdAt).toLocaleDateString('en-US', {
                      month: 'short',
                      day: 'numeric',
                      year: new Date(item.createdAt).getFullYear() !== new Date().getFullYear() ? 'numeric' : undefined,
                    })}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
