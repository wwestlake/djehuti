import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Trophy } from 'lucide-react'
import { profileApi } from '../../api/profileApi'
import type { Achievement } from '../../api/profileApi'

export default function AchievementsPreview({ userId, limit = 6 }: { userId: string; limit?: number }) {
  const [achievements, setAchievements] = useState<Achievement[] | null>(null)

  useEffect(() => {
    let cancelled = false
    profileApi.getAchievements(userId).then(list => { if (!cancelled) setAchievements(list) }).catch(() => setAchievements([]))
    return () => { cancelled = true }
  }, [userId])

  if (achievements === null || achievements.length === 0) return null

  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
        <h3 style={{ margin: 0, fontSize: '0.95rem', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Trophy size={16} style={{ color: 'var(--accent)' }} /> Achievements
          <span style={{ color: 'var(--text-muted)', fontWeight: 400 }}>({achievements.length})</span>
        </h3>
        <Link to="/achievements" style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>See all →</Link>
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10 }}>
        {achievements.slice(0, limit).map(a => (
          <div key={a.id} title={`${a.name} — ${a.description}`} style={{
            display: 'flex', alignItems: 'center', gap: 6, padding: '6px 10px', borderRadius: 20,
            background: 'var(--surface)', border: '1px solid var(--border)', fontSize: '0.8rem',
          }}>
            <span>{a.icon}</span>
            <span>{a.name}</span>
          </div>
        ))}
      </div>
    </div>
  )
}
