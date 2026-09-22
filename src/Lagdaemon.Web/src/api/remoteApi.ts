const API = '/djehuti'

// ── Types ─────────────────────────────────────────────────────────────────────

export type HostSession = {
  hostSessionId: string
  productSlug: string
  deviceName: string | null
  agentAvailable: boolean
  controlPanelAvailable: boolean
  presenceState: string
}

export type PairApproveResult = {
  grantToken: string
  hostSession: HostSession
}

export type GrantValidateResult = {
  hostSession: HostSession
}

// ── Grant token storage ───────────────────────────────────────────────────────

const STORAGE_KEY = 'djehuti_remote_grant'

export function loadStoredGrant(): string | null {
  try { return localStorage.getItem(STORAGE_KEY) } catch { return null }
}

export function saveGrant(token: string): void {
  try { localStorage.setItem(STORAGE_KEY, token) } catch { /* ignore */ }
}

export function clearGrant(): void {
  try { localStorage.removeItem(STORAGE_KEY) } catch { /* ignore */ }
}

// ── API calls ─────────────────────────────────────────────────────────────────

// Phone submits pairing code after the user types/scans it.
// Server confirms the same logged-in user owns the desktop session,
// then issues a 30-day grant token.
export async function approvePairing(pairingCode: string): Promise<PairApproveResult> {
  const code = pairingCode.trim().toUpperCase()
  const res = await fetch(`${API}/api/remote/pairings/${encodeURIComponent(code)}/approve`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ remoteDeviceName: 'Phone Browser', remoteDeviceType: 'web' }),
  })
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `Pairing failed (${res.status})`)
  }
  // Server returns { grantToken, hostSessionId, expiresAt } — reshape to our type
  const raw = await res.json() as { grantToken: string; hostSessionId: string; expiresAt: string }
  // Fetch full session info via validate (which also sets the rolling 30d expiry)
  const validated = await validateGrant(raw.grantToken)
  if (!validated) throw new Error('Grant issued but validation failed immediately')
  return { grantToken: raw.grantToken, hostSession: validated.hostSession }
}

// Called on every page load. Validates the stored grant token and rolls
// its expiry forward 30 days. Returns null if invalid/expired.
export async function validateGrant(token: string): Promise<GrantValidateResult | null> {
  const res = await fetch(`${API}/api/remote/grant/validate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ grantToken: token }),
  })
  if (res.status === 401 || res.status === 403) return null
  if (!res.ok) return null
  return res.json() as Promise<GrantValidateResult>
}

// Revoke the current grant (forget this device).
export async function revokeGrant(token: string): Promise<void> {
  await fetch(`${API}/api/remote/devices/${encodeURIComponent(token)}`, {
    method: 'DELETE',
    credentials: 'include',
  })
}

// ── WebSocket helpers ─────────────────────────────────────────────────────────

// Returns the WebSocket URL for the signaling relay (phone role).
export function signalingUrl(hostSessionId: string): string {
  const proto = window.location.protocol === 'https:' ? 'wss' : 'ws'
  const host = window.location.host
  return `${proto}://${host}/djehuti/ws/remote/signaling?hostSessionId=${hostSessionId}&role=phone`
}
