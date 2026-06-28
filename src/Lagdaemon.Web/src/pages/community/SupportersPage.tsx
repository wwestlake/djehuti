import { useEffect, useState } from 'react'
import { patreonApi, PatreonTier, SupporterEntry } from '../../api/patreonApi'
import TierBadge from '../../components/TierBadge'

export default function SupportersPage() {
  const [tiers, setTiers] = useState<PatreonTier[]>([])
  const [supporters, setSupporters] = useState<SupporterEntry[]>([])

  useEffect(() => {
    patreonApi.getTiers().then(setTiers)
    patreonApi.getSupporters().then(setSupporters)
  }, [])

  const byTier = (tierId: string) => supporters.filter(s => s.tierId === tierId)

  return (
    <div className="supporters-page">
      <div className="supporters-hero">
        <h1>Wall of Supporters</h1>
        <p>
          These people believe that science, technology, and learning should be open to
          everyone — including kids who haven't had access to it yet. This research exists
          because of them. Thank you.
        </p>
        <a
          className="supporters-patreon-btn"
          href="https://www.patreon.com/lagdaemon"
          target="_blank"
          rel="noopener noreferrer"
        >
          Join them on Patreon →
        </a>
      </div>

      <div className="supporters-tiers">
        {[...tiers].reverse().map(tier => {
          const members = byTier(tier.tierId)
          return (
            <div key={tier.tierId} className="supporters-tier-section">
              <div className="supporters-tier-header">
                <TierBadge tierName={tier.tierName} badgeColor={tier.badgeColor} badgeLabel={tier.badgeLabel} size="md" />
                <div className="supporters-tier-info">
                  <span className="supporters-tier-name">{tier.tierName}</span>
                  <span className="supporters-tier-amount">${(tier.amountCents / 100).toFixed(0)}/mo</span>
                </div>
                {tier.description && <p className="supporters-tier-desc">{tier.description}</p>}
              </div>
              {members.length > 0 ? (
                <div className="supporters-names">
                  {members.map((s, i) => (
                    <span key={i} className="supporter-name">{s.displayName}</span>
                  ))}
                </div>
              ) : (
                <p className="supporters-empty">Be the first at this level.</p>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
