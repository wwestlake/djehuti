import { useEffect, useState } from 'react'

interface Sponsor {
  id: string
  name: string
  logoUrl: string | null
  websiteUrl: string | null
  tier: 'gold' | 'silver' | 'bronze'
  blurb: string | null
  position: number
}

const TIER_LABELS: Record<string, string> = {
  gold: 'Gold Sponsors',
  silver: 'Silver Sponsors',
  bronze: 'Bronze Sponsors',
}

const TIER_ORDER = ['gold', 'silver', 'bronze']

export default function SponsorsPage() {
  const [sponsors, setSponsors] = useState<Sponsor[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch('/djehuti/api/sponsors')
      .then(r => r.json())
      .then(setSponsors)
      .finally(() => setLoading(false))
  }, [])

  const byTier = (tier: string) => sponsors.filter(s => s.tier === tier)

  return (
    <div className="sponsors-page">
      <div className="sponsors-hero">
        <h1>Our Sponsors</h1>
        <p>
          These companies and individuals make the Djehuti research project possible —
          through financial support, server infrastructure, AI compute time, or other
          in-kind contributions. Open science runs on generosity. We are grateful.
        </p>
      </div>

      {loading && <div className="forum-loading">Loading…</div>}

      {!loading && sponsors.length === 0 && (
        <p className="forum-empty" style={{ textAlign: 'center', marginTop: '3rem' }}>
          No sponsors yet. Interested in sponsoring? <a href="mailto:contact@lagdaemon.com">Get in touch.</a>
        </p>
      )}

      {!loading && TIER_ORDER.map(tier => {
        const members = byTier(tier)
        if (members.length === 0) return null
        return (
          <section key={tier} className={`sponsors-tier sponsors-tier-${tier}`}>
            <h2 className="sponsors-tier-heading">{TIER_LABELS[tier]}</h2>
            <div className={`sponsors-grid sponsors-grid-${tier}`}>
              {members.map(s => (
                <a
                  key={s.id}
                  className="sponsor-card"
                  href={s.websiteUrl ?? '#'}
                  target={s.websiteUrl ? '_blank' : undefined}
                  rel="noopener noreferrer"
                >
                  {s.logoUrl ? (
                    <img className="sponsor-logo" src={s.logoUrl} alt={s.name} />
                  ) : (
                    <div className="sponsor-logo-placeholder">{s.name[0]}</div>
                  )}
                  <div className="sponsor-info">
                    <span className="sponsor-name">{s.name}</span>
                    {s.blurb && <span className="sponsor-blurb">{s.blurb}</span>}
                  </div>
                </a>
              ))}
            </div>
          </section>
        )
      })}

      <div className="sponsors-cta">
        <h3>Become a Sponsor</h3>
        <p>
          Support open research into LLM behavior and information space dynamics.
          Financial contributions, server infrastructure, AI compute time, and other
          in-kind support are all welcome.
        </p>
        <a className="btn-primary" href="mailto:contact@lagdaemon.com">Get in Touch</a>
      </div>
    </div>
  )
}
