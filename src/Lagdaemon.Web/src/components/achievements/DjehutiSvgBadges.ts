// SVG artwork for Djehuti interaction achievement badges
export const DJEHUTI_SVG_BADGES: Record<string, string> = {
  'djehuti-initiate': `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
    <defs>
      <radialGradient id="ig1" cx="50%" cy="50%" r="50%">
        <stop offset="0%" stop-color="#4fc3f7"/>
        <stop offset="100%" stop-color="#0277bd"/>
      </radialGradient>
    </defs>
    <circle cx="50" cy="50" r="32" fill="url(#ig1)" opacity="0.95"/>
    <circle cx="50" cy="50" r="40" fill="none" stroke="#4fc3f7" stroke-width="2" stroke-dasharray="6 4" opacity="0.7"/>
    <circle cx="50" cy="50" r="47" fill="none" stroke="#4fc3f7" stroke-width="1" stroke-dasharray="3 6" opacity="0.35"/>
    <circle cx="50" cy="50" r="10" fill="#e3f2fd"/>
    <circle cx="50" cy="50" r="5" fill="#fff"/>
    <line x1="50" y1="18" x2="50" y2="82" stroke="#b3e5fc" stroke-width="0.8" opacity="0.4"/>
    <line x1="18" y1="50" x2="82" y2="50" stroke="#b3e5fc" stroke-width="0.8" opacity="0.4"/>
  </svg>`,

  'djehuti-prompt-engineer': `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
    <defs>
      <linearGradient id="pg1" x1="0%" y1="0%" x2="100%" y2="100%">
        <stop offset="0%" stop-color="#ffa726"/>
        <stop offset="100%" stop-color="#e65100"/>
      </linearGradient>
    </defs>
    <rect x="20" y="38" width="20" height="24" rx="5" fill="url(#pg1)"/>
    <rect x="40" y="38" width="20" height="24" rx="5" fill="url(#pg1)" opacity="0.85"/>
    <rect x="60" y="38" width="20" height="24" rx="5" fill="url(#pg1)" opacity="0.7"/>
    <path d="M30 38 Q35 28 40 38" fill="none" stroke="#ffa726" stroke-width="3" stroke-linecap="round"/>
    <path d="M50 38 Q55 28 60 38" fill="none" stroke="#ffa726" stroke-width="3" stroke-linecap="round" opacity="0.85"/>
    <path d="M30 62 Q35 72 40 62" fill="none" stroke="#ff7043" stroke-width="3" stroke-linecap="round"/>
    <path d="M50 62 Q55 72 60 62" fill="none" stroke="#ff7043" stroke-width="3" stroke-linecap="round" opacity="0.85"/>
    <circle cx="50" cy="50" r="6" fill="#fff3e0" stroke="#ffa726" stroke-width="1.5"/>
    <circle cx="50" cy="50" r="2.5" fill="#ffa726"/>
  </svg>`,

  'djehuti-code-collab': `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
    <defs>
      <linearGradient id="cg1" x1="0%" y1="0%" x2="100%" y2="100%">
        <stop offset="0%" stop-color="#66bb6a"/>
        <stop offset="100%" stop-color="#1b5e20"/>
      </linearGradient>
    </defs>
    <rect x="12" y="22" width="76" height="56" rx="8" fill="#1b2a1b" stroke="#66bb6a" stroke-width="2"/>
    <text x="26" y="46" font-family="monospace" font-size="18" fill="#66bb6a" font-weight="bold">&lt;</text>
    <text x="70" y="46" font-family="monospace" font-size="18" fill="#66bb6a" font-weight="bold">&gt;</text>
    <line x1="45" y1="36" x2="55" y2="64" stroke="#a5d6a7" stroke-width="3" stroke-linecap="round"/>
    <circle cx="50" cy="68" r="6" fill="url(#cg1)"/>
    <path d="M47 68 L50 64 L53 68" fill="#a5d6a7"/>
    <circle cx="50" cy="72" r="2" fill="#a5d6a7"/>
  </svg>`,

  'djehuti-deep-diver': `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
    <defs>
      <radialGradient id="dg1" cx="50%" cy="50%" r="50%">
        <stop offset="0%" stop-color="#ce93d8"/>
        <stop offset="100%" stop-color="#4a148c"/>
      </radialGradient>
    </defs>
    <circle cx="50" cy="50" r="44" fill="none" stroke="#9c27b0" stroke-width="1.5" opacity="0.3"/>
    <circle cx="50" cy="50" r="36" fill="none" stroke="#9c27b0" stroke-width="1.5" opacity="0.45"/>
    <circle cx="50" cy="50" r="27" fill="none" stroke="#ab47bc" stroke-width="1.5" opacity="0.6"/>
    <circle cx="50" cy="50" r="18" fill="none" stroke="#ce93d8" stroke-width="2" opacity="0.8"/>
    <circle cx="50" cy="50" r="10" fill="url(#dg1)"/>
    <polygon points="50,40 56,55 50,52 44,55" fill="#f3e5f5"/>
    <polygon points="50,60 56,45 50,48 44,45" fill="#ce93d8" opacity="0.6"/>
  </svg>`,

  'djehuti-daily-sync': `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
    <defs>
      <linearGradient id="yg1" x1="0%" y1="0%" x2="100%" y2="100%">
        <stop offset="0%" stop-color="#fff176"/>
        <stop offset="100%" stop-color="#f57f17"/>
      </linearGradient>
    </defs>
    <circle cx="50" cy="50" r="22" fill="url(#yg1)" opacity="0.9"/>
    <path d="M50 15 A35 35 0 1 1 15 50" fill="none" stroke="#fdd835" stroke-width="4" stroke-linecap="round"/>
    <polygon points="50,10 45,22 55,22" fill="#fdd835"/>
    <line x1="50" y1="28" x2="50" y2="72" stroke="#fff" stroke-width="2" opacity="0.6"/>
    <line x1="50" y1="28" x2="62" y2="42" stroke="#fff" stroke-width="2.5" stroke-linecap="round"/>
    <circle cx="50" cy="50" r="4" fill="#fff"/>
    <line x1="28" y1="50" x2="22" y2="44" stroke="#fdd835" stroke-width="2" opacity="0.7"/>
    <line x1="28" y1="50" x2="22" y2="56" stroke="#fdd835" stroke-width="2" opacity="0.7"/>
    <line x1="72" y1="50" x2="78" y2="44" stroke="#fdd835" stroke-width="2" opacity="0.7"/>
    <line x1="72" y1="50" x2="78" y2="56" stroke="#fdd835" stroke-width="2" opacity="0.7"/>
  </svg>`,
}
